#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 사람처럼 조준하는 탐욕 봇: 지금 보이는 적 중 "한 번 칠 때 가장 많이 버는 자리"로 마우스를 옮기고, 그 무리를 따라다닌다.
    //
    // 판단(목표 고르기):
    // - 후보 자리: 모든 적의 위치. 그 자리에 Breaker 원을 두면 닿는 적들의 값어치를 더한다.
    //   적 하나의 값어치 = Gold × min(1, 한 번 치는 피해 ÷ 남은 HP) — 한 번에 죽으면 Gold 전부, 아니면 깎는 비율만큼.
    //   피해는 Breaker 피해 + 종류 보너스(행성·별·은하)에 치명타 기대값(혜성 버프면 확정 치명타)을 곱한다.
    // - 점수 = 값어치 ÷ (1 + 옮겨 가는 시간 ÷ 공격 주기). 멀면 가는 동안 못 치므로 깎는다.
    // - 점수 상위 RefineCount개는 닿는 적들의 값어치 가중 가운데로 한 번 옮겨 다시 매긴다(무리 한가운데를 노린다).
    // - 지금 목표보다 SwitchMargin 이상 좋을 때만 바꾼다. 혜성(픽업)은 가장 좋은 일반 목표 한 번의 PickupWorth배로 보고, 가까우면 먼저 줍는다.
    //
    // 언제 고르나:
    // - 평소에는 DecisionSeconds(±DecisionJitter)마다. 고른 목표는 ReactionSeconds 뒤에야 손에 옮겨진다(반응 지연).
    // - 따라가던 적이 공격 말고 다른 일(레이저·폭발 등)로 죽으면 그때 다시 고른다(역시 반응 지연).
    // - 공격이 터진 직후(직전 고르기에서 PulseRetargetSeconds 이상 지났으면) 다시 고르고, PulseMoveSeconds 뒤에 옮긴다.
    //   사람은 공격 리듬을 알고 다음 무리를 미리 봐 두었다가 터지자마자 손을 옮긴다. 다만 이 간격보다 자주 손을 꺾지는 못한다.
    //
    // 손(매 프레임):
    // - 목표는 "기준 적 + 어긋남"이다. 적이 공전해도 그 적을 따라간다(사람이 움직이는 무리를 쫓는 것처럼).
    // - 커서는 최대 속도(MaxSpeedScreens)와 목표 근처의 감속(ApproachGain)으로 움직인다. 목표를 고를 때마다 조준 오차(AimErrorScreens)를 새로 뽑는다.
    //
    // 터치 프로필(LiftWhileMoving): 목표로 옮기는 동안 조준점을 내지 않는다(손을 뗀 상태). 닿으면 누른다 — 누른 Step에 Breaker가 곧바로 친다.
    //
    // 판의 모든 적이 보인다(카메라가 전장 전체를 보여 준다). 남은 HP·Gold도 안다고 본다(화면의 색 등급·크기로 사람이 어림하는 값).
    internal sealed class GreedyAimBot : IAimBot
    {
        private const float Epsilon = 1e-5f;

        private readonly AimBotProfile _profile;
        private readonly List<int> _neighbors = new List<int>();
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly Stack<List<int>> _spareLists = new Stack<List<int>>();
        private readonly List<Candidate> _top = new List<Candidate>();
        private float[] _worth = Array.Empty<float>();

        private Random _random;
        private Point2 _cursor;
        private Target _current;
        private Target _pending;
        private float _pendingDelay;
        private float _untilDecision;
        private float _sinceDecision;

        // 목표: 기준 적(없으면 고정점)과 어긋남.
        private struct Target
        {
            public bool Exists;
            public Enemy Anchor;
            public float OffsetX;
            public float OffsetY;
            public Point2 Fixed;

            public Point2 PointNow => Anchor != null && Anchor.IsAlive
                ? new Point2(Anchor.Position.X + OffsetX, Anchor.Position.Y + OffsetY)
                : Fixed;
        }

        private struct Candidate
        {
            public Point2 Point;
            public float Value;
            public float Score;
        }

        public GreedyAimBot(AimBotProfile profile = null)
        {
            _profile = profile ?? AimBotProfile.Default;
            _profile.Validate();
        }

        public string Name => _profile.Name;

        public AimBotProfile Profile => _profile;

        public void Begin(int seed)
        {
            _random = new Random(seed);
            _cursor = BattleSpace.Origin;
            _current = default;
            _pending = default;
            _pendingDelay = 0;
            _untilDecision = 0;
            _sinceDecision = 0;
        }

        public Point2? Aim(World world, float delta)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            if (_random == null)
                Begin(0);

            if (_pending.Exists)
            {
                _pendingDelay -= delta;

                if (_pendingDelay <= 0)
                {
                    _current = _pending;
                    _pending = default;
                }
            }

            _untilDecision -= delta;
            _sinceDecision += delta;

            // 따라가던 적이 죽었으면 다시 고른다.
            if (_current.Exists && _current.Anchor != null && !_current.Anchor.IsAlive && !_pending.Exists)
                _untilDecision = Math.Min(_untilDecision, 0);

            // 지난 진행에서 공격이 터졌으면(Ticks는 지난 Step의 기록) 리듬에 맞춰 바로 다음 목표로.
            bool pulse = world.Breaker.Ticks.Count > 0 && _sinceDecision >= _profile.PulseRetargetSeconds;

            if (_untilDecision <= 0 || pulse)
            {
                Decide(world, pulse ? _profile.PulseMoveSeconds : _profile.ReactionSeconds);
                float jitter = _profile.DecisionJitter * (float)(2 * _random.NextDouble() - 1);
                _untilDecision = _profile.DecisionSeconds + jitter;
                _sinceDecision = 0;
            }

            if (!_current.Exists)
                return _profile.LiftWhileMoving ? null : _cursor;

            float screenHeight = ScreenHeight(world);
            Point2 target = _current.PointNow;
            MoveCursor(target, screenHeight, delta);

            // 손가락: 목표에 닿을 때까지는 떼고 있다.
            if (_profile.LiftWhileMoving)
            {
                float land = _profile.LandDistanceScreens * screenHeight;

                if (_cursor.DistanceSquared(target) > land * land)
                    return null;
            }

            return _cursor;
        }

        private void MoveCursor(Point2 target, float screenHeight, float delta)
        {
            float dx = target.X - _cursor.X;
            float dy = target.Y - _cursor.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance < Epsilon || delta <= 0)
                return;

            float speed = Math.Min(_profile.MaxSpeedScreens * screenHeight, _profile.ApproachGain * distance);
            float step = Math.Min(distance, speed * delta);
            _cursor = new Point2(_cursor.X + dx / distance * step, _cursor.Y + dy / distance * step);
        }

        // 목표를 고른다. delay: 고른 목표가 손에 옮겨지기까지(초).
        private void Decide(World world, float delay)
        {
            IReadOnlyList<Enemy> enemies = world.Enemies;
            BreakerSkill breaker = world.Breaker;
            float reach = breaker.CurrentRadius;
            float tickSeconds = breaker.CurrentInterval;
            float maxSpeed = _profile.MaxSpeedScreens * ScreenHeight(world);
            float critFactor = breaker.IsGuaranteedCritical
                ? 1 + breaker.CurrentCritDamage
                : 1 + breaker.Definition.CritChance * breaker.CurrentCritDamage;

            float cellSize = BuildGrid(enemies, breaker, critFactor, reach);

            // 일반 적 위치마다 점수를 매겨 상위 몇 개만 남긴다.
            _top.Clear();
            int keep = Math.Max(1, _profile.RefineCount);

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Definition.IsPickup)
                    continue;

                Point2 point = enemies[i].Position;
                float value = ValueAt(point, enemies, reach, cellSize);
                Offer(new Candidate { Point = point, Value = value, Score = ScoreOf(point, value, maxSpeed, tickSeconds) }, keep);
            }

            // 상위 후보를 무리의 가운데로 한 번 옮겨 본다.
            Candidate best = default;
            bool found = false;

            for (int k = 0; k < _top.Count; k++)
            {
                Candidate candidate = _top[k];

                if (_profile.RefineCount > 0 && TryCentroid(candidate.Point, enemies, reach, cellSize, out Point2 middle))
                {
                    float value = ValueAt(middle, enemies, reach, cellSize);
                    float score = ScoreOf(middle, value, maxSpeed, tickSeconds);

                    if (score > candidate.Score)
                        candidate = new Candidate { Point = middle, Value = value, Score = score };
                }

                if (!found || candidate.Score > best.Score)
                {
                    best = candidate;
                    found = true;
                }
            }

            // 혜성(픽업): 가장 좋은 일반 목표 한 번의 PickupWorth배.
            float bestValue = found ? best.Value : 0;
            Enemy pickup = null;
            float pickupScore = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                if (!enemy.Definition.IsPickup)
                    continue;

                float value = Math.Max(bestValue, Epsilon) * _profile.PickupWorth;
                float score = ScoreOf(enemy.Position, value, maxSpeed, tickSeconds);

                if (score > pickupScore)
                {
                    pickup = enemy;
                    pickupScore = score;
                }
            }

            if (pickup != null && (!found || pickupScore > best.Score))
            {
                Choose(pickup, pickup.Position, world, delay);
                return;
            }

            if (!found || best.Value <= 0)
                return;

            // 지금 목표가 아직 괜찮으면 그대로 둔다(작은 이득으로는 옮기지 않는다).
            if (_current.Exists && !_pending.Exists && (_current.Anchor == null || _current.Anchor.IsAlive))
            {
                Point2 now = _current.PointNow;
                float stay = ScoreOf(now, ValueAt(now, enemies, reach, cellSize), maxSpeed, tickSeconds);

                if (stay > 0 && best.Score <= stay * (1 + _profile.SwitchMargin))
                    return;
            }

            Choose(NearestTo(best.Point, enemies, reach), best.Point, world, delay);
        }

        // 목표를 정하고 delay 뒤에 손에 옮긴다. 기준 적이 있으면 그 적을 따라간다. 조준 오차를 새로 뽑는다.
        // 기다리는 동안 새로 고르면 마지막 것만 남는다.
        private void Choose(Enemy anchor, Point2 point, World world, float delay)
        {
            float sigma = _profile.AimErrorScreens * ScreenHeight(world);
            float errorX = sigma * Gaussian();
            float errorY = sigma * Gaussian();

            var target = new Target { Exists = true, Anchor = anchor, Fixed = new Point2(point.X + errorX, point.Y + errorY) };

            if (anchor != null)
            {
                target.OffsetX = point.X - anchor.Position.X + errorX;
                target.OffsetY = point.Y - anchor.Position.Y + errorY;
            }

            _pending = target;
            _pendingDelay = delay;
        }

        private float ScoreOf(Point2 point, float value, float maxSpeed, float tickSeconds)
        {
            float dx = point.X - _cursor.X;
            float dy = point.Y - _cursor.Y;
            float travel = (float)Math.Sqrt(dx * dx + dy * dy) / maxSpeed;
            return value / (1 + travel / Math.Max(tickSeconds, Epsilon));
        }

        private void Offer(Candidate candidate, int keep)
        {
            if (_top.Count < keep)
            {
                _top.Add(candidate);
                return;
            }

            int worst = 0;

            for (int k = 1; k < _top.Count; k++)
            {
                if (_top[k].Score < _top[worst].Score)
                    worst = k;
            }

            if (candidate.Score > _top[worst].Score)
                _top[worst] = candidate;
        }

        // ── 값어치와 격자 ─────────────────────────────────────────────

        // 적마다 한 번 치기의 값어치를 구하고, 일반 적의 위치를 격자에 넣는다(판단마다 새로). 격자 칸의 크기를 돌려준다.
        private float BuildGrid(IReadOnlyList<Enemy> enemies, BreakerSkill breaker, float critFactor, float reach)
        {
            if (_worth.Length < enemies.Count)
                _worth = new float[Math.Max(enemies.Count, _worth.Length * 2)];

            float largest = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                _worth[i] = WorthOf(enemies[i], breaker, critFactor);
                largest = Math.Max(largest, enemies[i].Stats.Radius);
            }

            float cellSize = Math.Max(reach + largest, Epsilon);

            foreach (List<int> list in _cells.Values)
            {
                list.Clear();
                _spareLists.Push(list);
            }

            _cells.Clear();

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Definition.IsPickup)
                    continue;

                long key = CellKey(enemies[i].Position, cellSize);

                if (!_cells.TryGetValue(key, out List<int> list))
                {
                    list = _spareLists.Count > 0 ? _spareLists.Pop() : new List<int>();
                    _cells.Add(key, list);
                }

                list.Add(i);
            }

            return cellSize;
        }

        private static float WorthOf(Enemy enemy, BreakerSkill breaker, float critFactor)
        {
            if (enemy.Definition.IsPickup || enemy.Stats.Gold <= 0)
                return 0;

            float damage = (breaker.Definition.Damage + BonusAgainst(enemy, breaker.Definition)) * critFactor;
            float share = enemy.Health <= damage ? 1 : damage / enemy.Health;
            return enemy.Stats.Gold * share;
        }

        private static float BonusAgainst(Enemy enemy, BreakerDefinition breaker)
        {
            switch (enemy.Definition.Type)
            {
                case EnemyType.Planet: return breaker.PlanetBonusDamage;
                case EnemyType.Star: return breaker.StarBonusDamage;
                case EnemyType.Galaxy: return breaker.GalaxyBonusDamage;
                default: return 0;
            }
        }

        // 점 point에 Breaker 원을 두면 닿는 일반 적의 값어치 합.
        private float ValueAt(Point2 point, IReadOnlyList<Enemy> enemies, float reach, float cellSize)
        {
            CollectNeighbors(point, cellSize);
            float value = 0;

            for (int n = 0; n < _neighbors.Count; n++)
            {
                int i = _neighbors[n];
                Enemy enemy = enemies[i];
                float touch = reach + enemy.Stats.Radius;

                if (enemy.Position.DistanceSquared(point) <= touch * touch)
                    value += _worth[i];
            }

            return value;
        }

        // point에 닿는 적들의 값어치 가중 가운데. 값어치 있는 적이 둘 미만이면 false.
        private bool TryCentroid(Point2 point, IReadOnlyList<Enemy> enemies, float reach, float cellSize, out Point2 middle)
        {
            CollectNeighbors(point, cellSize);
            float sumX = 0, sumY = 0, sum = 0;
            int members = 0;

            for (int n = 0; n < _neighbors.Count; n++)
            {
                int i = _neighbors[n];
                Enemy enemy = enemies[i];
                float touch = reach + enemy.Stats.Radius;

                if (_worth[i] <= 0 || enemy.Position.DistanceSquared(point) > touch * touch)
                    continue;

                sumX += enemy.Position.X * _worth[i];
                sumY += enemy.Position.Y * _worth[i];
                sum += _worth[i];
                members++;
            }

            if (members < 2 || sum <= 0)
            {
                middle = point;
                return false;
            }

            middle = new Point2(sumX / sum, sumY / sum);
            return true;
        }

        private void CollectNeighbors(Point2 point, float cellSize)
        {
            _neighbors.Clear();
            int cx = (int)Math.Floor(point.X / cellSize);
            int cy = (int)Math.Floor(point.Y / cellSize);

            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    if (_cells.TryGetValue(Key(x, y), out List<int> list))
                        _neighbors.AddRange(list);
                }
            }
        }

        // 고른 자리에서 가장 가까운 일반 적(눈이 붙잡는 적). Breaker 원 + 그 적의 반지름 안에 없으면 null(고정점을 노린다).
        private static Enemy NearestTo(Point2 point, IReadOnlyList<Enemy> enemies, float reach)
        {
            Enemy nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                if (enemy.Definition.IsPickup)
                    continue;

                float distance = enemy.Position.DistanceSquared(point);
                float touch = reach + enemy.Stats.Radius;

                if (distance <= touch * touch && distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static long CellKey(Point2 point, float cellSize) =>
            Key((int)Math.Floor(point.X / cellSize), (int)Math.Floor(point.Y / cellSize));

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        private static float ScreenHeight(World world) => 2 * AimBotProfile.ScreenHalfHeight * world.Hq.FieldScale;

        // 표준정규분포(Box–Muller).
        private float Gaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
        }
    }
}
#endif
