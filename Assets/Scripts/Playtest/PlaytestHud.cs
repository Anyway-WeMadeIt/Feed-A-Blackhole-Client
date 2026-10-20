#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 개발 HUD의 숫자: 판의 남은·흐른 시간, Level·EXP, 최근 5초(판 시간)의 처치·Gold·EXP·피해 속도, 종류별 살아 있는 적, Breaker 수치.
    // 판 시간이 흐른 프레임에만 표본을 넣는다(정지·전환 중에는 그대로). 판이 바뀌면 처음부터 센다.
    internal sealed class PlaytestHud
    {
        private const float Window = 5f;
        // 글을 다시 만드는 간격(실제 시간, 초). 매 프레임 문자열을 만들지 않는다.
        private const float RefreshInterval = 0.25f;

        private readonly struct Sample
        {
            public readonly float Elapsed;
            public readonly long Kills;
            public readonly long Gold;
            public readonly long Exp;
            public readonly double Damage;

            public Sample(float elapsed, long kills, long gold, long exp, double damage)
            {
                Elapsed = elapsed;
                Kills = kills;
                Gold = gold;
                Exp = exp;
                Damage = damage;
            }
        }

        private readonly Queue<Sample> _samples = new();
        private readonly Dictionary<EnemyType, int> _alive = new();
        private readonly StringBuilder _text = new();
        private GameSession _session;
        private float _lastElapsed;
        private long _kills;
        private double _damage;
        private float _sinceRefresh = RefreshInterval;

        public string Text { get; private set; } = "";

        // LateUpdate마다 부른다(판이 이번 프레임에 Step을 마친 뒤). setupName은 마지막으로 넣은 시나리오·세팅(없으면 null),
        // fingerprint는 이번 실행의 수치 지문, liveStatus는 수치 실시간 반영 상태 한 줄(없으면 null).
        // unscaledDelta는 글을 다시 만드는 간격에만 쓴다.
        public void Update(
            GameSession session,
            ContentTag tag,
            string setupName,
            string fingerprint,
            string liveStatus,
            float timeScale,
            float unscaledDelta)
        {
            if (session == null)
            {
                _session = null;
                Text = "";
                return;
            }

            if (session != _session)
                Reset(session);

            if (session.Elapsed > _lastElapsed)
            {
                Accumulate(session.World);
                _lastElapsed = session.Elapsed;
                _samples.Enqueue(new Sample(session.Elapsed, _kills, session.World.EarnedGold, session.World.Hq.Exp, _damage));

                while (_samples.Count > 1 && _samples.Peek().Elapsed < session.Elapsed - Window)
                    _samples.Dequeue();
            }

            _sinceRefresh += unscaledDelta;

            if (_sinceRefresh < RefreshInterval)
                return;

            _sinceRefresh = 0;
            Text = Build(session, tag, setupName, fingerprint, liveStatus, timeScale);
        }

        private void Reset(GameSession session)
        {
            _session = session;
            _samples.Clear();
            _lastElapsed = session.Elapsed;
            _kills = 0;
            _damage = 0;
            _sinceRefresh = RefreshInterval;
            _samples.Enqueue(new Sample(session.Elapsed, 0, session.World.EarnedGold, session.World.Hq.Exp, 0));
        }

        // 이번 Step의 맞힘·사망 기록(World는 Step마다 새로 채운다). 픽업(혜성)은 뺀다.
        private void Accumulate(World world)
        {
            foreach (HitRecord hit in world.Hits)
            {
                if (!hit.IsPickup)
                    _damage += hit.Amount;
            }

            foreach (DeathRecord death in world.Deaths)
            {
                if (!death.IsPickup)
                    _kills++;
            }
        }

        // 지금 판의 숫자 묶음. Update가 따라가는 판이 아니면(아직 표본이 없으면) 속도 없이 만든다.
        public HudSnapshot Capture(GameSession session)
        {
            World world = session.World;
            Hq hq = world.Hq;
            bool tracked = session == _session && _samples.Count > 0;
            var snapshot = new HudSnapshot
            {
                Elapsed = session.Elapsed,
                Remaining = session.Remaining,
                Stage = hq.Stage,
                Level = hq.Level,
                GoalLevel = hq.GoalLevel,
                Exp = hq.Exp,
                Kills = tracked ? _kills : 0,
                Gold = world.EarnedGold,
                Damage = tracked ? _damage : 0,
            };

            if (tracked)
            {
                Sample first = _samples.Peek();
                float span = session.Elapsed - first.Elapsed;

                if (span > 0.5f)
                {
                    snapshot.RateSpan = span;
                    snapshot.KillsPerSecond = (_kills - first.Kills) / span;
                    snapshot.GoldPerSecond = (world.EarnedGold - first.Gold) / span;
                    snapshot.ExpPerSecond = (hq.Exp - first.Exp) / span;
                    snapshot.DamagePerSecond = (_damage - first.Damage) / span;
                }
            }

            _alive.Clear();
            foreach (Enemy enemy in world.Enemies)
            {
                _alive.TryGetValue(enemy.Definition.Type, out int count);
                _alive[enemy.Definition.Type] = count + 1;
            }

            foreach (KeyValuePair<EnemyType, int> pair in _alive)
                snapshot.Alive.Add((pair.Key, pair.Value));

            return snapshot;
        }

        private string Build(GameSession session, ContentTag tag, string setupName, string fingerprint, string liveStatus, float timeScale)
        {
            World world = session.World;
            Hq hq = world.Hq;
            _text.Clear();

            if (!string.IsNullOrEmpty(setupName))
                _text.Append($"세팅 {setupName} · ");

            string version = tag.ContentVersion.Length > 0 ? tag.ContentVersion : "원본";
            _text.Append(version);
            if (!string.IsNullOrEmpty(fingerprint))
                _text.Append(" · ").Append(fingerprint);
            if (timeScale != 1f)
                _text.Append($" · ×{timeScale:0.##}");
            if (BattleCheats.IsTimeFrozen(session))
                _text.Append(" · 시간 고정");
            if (BattleCheats.EnemyMoveScaleOf(session) != 1f)
                _text.Append($" · 적 이동 ×{BattleCheats.EnemyMoveScaleOf(session):0.##}");
            _text.AppendLine();

            if (!string.IsNullOrEmpty(liveStatus))
                _text.AppendLine(liveStatus);

            HudSnapshot snapshot = Capture(session);
            _text.Append($"남은 {snapshot.Remaining:0.0}s · 흐른 {snapshot.Elapsed:0.0}s · Lv {snapshot.Level}");
            if (snapshot.GoalLevel != HqGrowthDefinition.NoGoal)
                _text.Append($"/{snapshot.GoalLevel}");
            _text.Append($" (성장도 {snapshot.Stage})");
            _text.AppendLine();

            _text.Append($"EXP {snapshot.Exp:N0}");
            if (hq.NextLevelExp is long next)
                _text.Append($" / {next:N0} (남은 {next - snapshot.Exp:N0})");
            _text.AppendLine();

            if (snapshot.RateSpan > 0)
            {
                _text.Append($"{Window:0}초: 처치 {snapshot.KillsPerSecond:0.0}/s · Gold {snapshot.GoldPerSecond:N0}/s");
                _text.Append($" · EXP {snapshot.ExpPerSecond:N0}/s · 피해 {snapshot.DamagePerSecond:N0}/s");
            }
            else
            {
                _text.Append($"{Window:0}초: 재는 중");
            }
            _text.AppendLine();

            _text.Append($"이 판: 처치 {snapshot.Kills:N0} · Gold {snapshot.Gold:N0}");
            _text.AppendLine();

            _text.Append($"적 {world.Enemies.Count}:");
            foreach ((EnemyType type, int count) in snapshot.Alive)
                _text.Append($" {PlaytestNames.Of(type)} {count}");
            _text.AppendLine();

            BreakerSkill breaker = world.Breaker;
            BreakerDefinition definition = breaker.Definition;
            _text.Append($"Breaker: 피해 {definition.Damage:0.#} · 주기 {breaker.CurrentInterval:0.00}s · 반경 {breaker.CurrentRadius:0.00}");
            _text.Append($" · 치명 {definition.CritChance:P0} ×{breaker.CurrentCritDamage:0.##}");

            return _text.ToString();
        }
    }

    // 판 상태 숫자 묶음. HUD 글과 느낌 메모(M3)가 같은 숫자를 쓴다.
    // 속도(…PerSecond)는 최근 5초(판 시간)의 평균이고, 잴 표본이 모자라면 RateSpan이 0이다.
    internal sealed class HudSnapshot
    {
        public float Elapsed;
        public float Remaining;
        public int Stage;
        public int Level;
        public int GoalLevel;
        public long Exp;
        public long Kills;
        public long Gold;
        public double Damage;
        public float RateSpan;
        public double KillsPerSecond;
        public double GoldPerSecond;
        public double ExpPerSecond;
        public double DamagePerSecond;
        public List<(EnemyType Type, int Count)> Alive = new List<(EnemyType Type, int Count)>();
    }

    // 개발 도구에 보이는 한국어 이름.
    internal static class PlaytestNames
    {
        public static string Of(EnemyType type) => type switch
        {
            EnemyType.Asteroid => "소행성",
            EnemyType.Planet => "행성",
            EnemyType.Star => "별",
            EnemyType.Comet => "혜성",
            EnemyType.Galaxy => "은하",
            _ => type.ToString(),
        };

        public static string Of(EnemyTraitType type) => type switch
        {
            EnemyTraitType.Golden => "황금",
            EnemyTraitType.Electric => "번개",
            EnemyTraitType.Moon => "달",
            EnemyTraitType.Laser => "레이저",
            EnemyTraitType.Supernova => "초신성",
            EnemyTraitType.Comet => "혜성",
            EnemyTraitType.Gravity => "중력장",
            _ => type.ToString(),
        };
    }
}
#endif
