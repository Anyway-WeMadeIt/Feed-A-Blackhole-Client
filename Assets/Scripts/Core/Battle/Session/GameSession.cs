using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 흐름: 시작(Begin) -> 진행(Advance) -> 종료(End) -> 결산(Settle).
    // - 시작: 판 조립(GameSessionFactory) 직후 한 번 부름.
    // - 진행: 시간을 흘려 World를 한 Step씩 처리하고, 제한 시간(TimeLimitRule)을 늘리거나 종료를 판정.
    // - 종료: 이정표에 닿았거나 제한 시간이 다 됐을 때, 또는 End를 부를 때.
    // - 결산: 판이 번 Gold를 진행 상태(ProgressState)에 더하고 성장도를 올린다.
    public sealed class GameSession
    {
        private readonly TimeLimitRule _timeLimit;
        // 이 판에서 파괴로 더한 시간의 합(초). 판 설정의 상한(MaxKillTimeBonus)까지만 쌓인다.
        private float _killTimeAdded;
        private readonly int _seed;
        private readonly ProgressState _progress;
        private readonly IReadOnlyList<SupplyRequest> _startSupply;

        private long _settledGold; // 결산할 Gold.
        private bool _ended;       // 판 종료 여부
        private bool _settled;     // 결산 여부

        public World World { get; }

        public bool IsEnded => _ended;
        public float Elapsed { get; private set; }
        public float Remaining => _timeLimit.Remaining(Elapsed);
        // 이 판에서 제한 시간에 더해진 초의 합(블랙홀 성장 + 파괴 때 시간 추가). 테스트 도구로 더한 시간은 들지 않는다.
        // 화면은 프레임 사이의 차이로 "방금 늘어난 시간"을 알린다.
        public float AddedSeconds => _timeLimit.ExtendedSeconds;

        // 적의 공전을 켜고 끈다(플레이어 설정 Object Rotation). 판 규칙이 아니라 표시에 가까운 설정이라 판 중에도 바꿀 수 있다.
        public void SetOrbitEnabled(bool enabled) => World.OrbitEnabled = enabled;

        // 시간 고정. 게임에서는 늘 false이고, 테스트 도구(BattleCheats)만 켠다. 켜 두면 흐른 만큼 제한 시간을 되돌려 남은 시간이 줄지 않는다.
        internal bool TimeFrozen { get; set; }

        internal GameSession(
            World world,
            TimeLimitRule timeLimit,
            int seed,
            ProgressState progress,
            IReadOnlyList<SupplyRequest> startSupply)
        {
            World = world;
            _timeLimit = timeLimit;
            _seed = seed;
            _progress = progress;
            _startSupply = startSupply;
        }

        // Breaker가 칠 조준점. 없으면 null(조준하지 않음).
        public void SetAimPoint(Point2? aimPoint) => World.SetAimPoint(aimPoint);

        public void Begin()
        {
            foreach (SupplyRequest request in _startSupply)
                World.RequestSpawn(request);

            World.ProcessSpawnRequests();
        }

        public AdvanceResult Advance(float delta)
        {
            DefinitionGuard.Delta(delta);

            if (_ended || delta == 0)
                return default;

            float step = _timeLimit.LimitStep(Elapsed, delta);
            int raised = World.Step(step);
            Elapsed += step;

            if (TimeFrozen)
                _timeLimit.ExtendUncounted(step);

            // 이정표에 닿았으면 남은 시간과 관계없이 이 Step에서 판 종료.
            if (World.Hq.ReachedMilestone)
            {
                End();
                return new AdvanceResult(0, true);
            }

            ExtendTimeLimit(raised);

            if (_timeLimit.HasExpired(Elapsed))
                End();

            return new AdvanceResult(raised, _ended);
        }

        public void End()
        {
            if (_ended)
                return;

            _ended = true;

            Hq hq = World.Hq;
            _settledGold = hq.ReachedMilestone ? hq.Milestone.RewardFor(_progress.Gold) : World.EarnedGold;
        }

        // 테스트 도구(BattleCheats)가 부른다. 제한 시간을 늘리되 판 통계의 더해진 시간에는 세지 않는다.
        internal void ExtendTimeUncounted(float seconds) => _timeLimit.ExtendUncounted(seconds);

        // 끝난 판에 남은 적과 처리되지 않은 생성 요청·사망 효과를 치운다. 처치가 아니다.
        public void ClearRemainingEnemies()
        {
            World.ClearRemainingEnemies();
        }

        // 끝난 판의 결과 스냅샷. 판을 버린 뒤에도 남는 기록이다.
        public BattleRawData CreateRawData()
        {
            Hq hq = World.Hq;
            BattleStats stats = World.CreateStats(_timeLimit.ExtendedSeconds);

            return new BattleRawData(
                _seed,
                Elapsed,
                World.GetKillCounts(),
                World.GetKillBuckets(),
                World.GoldenCritCount,
                World.GoldenCritBonusGold,
                World.TimeBonusCount,
                World.EarnedGold,
                hq.Level,
                hq.Exp,
                hq.Stage,
                hq.NextStage,
                hq.Milestone,
                _settledGold,
                stats);
        }

        // 결산: 끝난 판의 Gold를 진행 상태에 더하고, 이 판이 이정표에 닿았으면 성장도를 1 올린다(Hq.NextStage).
        public void Settle()
        {
            if (_settled)
                return;

            _progress.EarnGold(_settledGold);
            _progress.KeepGrowthStage(World.Hq.NextStage);
            _settled = true;
        }

        // - growth: 오른 Level마다 블랙홀의 성장 시간,
        // - 파괴 때 시간 추가가 성공한 만큼의 초(World가 사망 순간에 판정해 모아 둔다. 종류별 값이 없으면 판 설정의 추가 시간).
        private void ExtendTimeLimit(int raised)
        {
            float growth = raised * World.Hq.GrowthTime;
            float kills = World.TakeTimeBonusSeconds();
            float cap = _timeLimit.Definition.MaxKillTimeBonus;

            if (cap > 0)
                kills = Math.Min(kills, Math.Max(0, cap - _killTimeAdded));

            _killTimeAdded += kills;
            _timeLimit.Extend(growth + kills);
        }
    }
}
