using System;

namespace BlackHole.Core
{
    // 이 판의 블랙홀: Level과 누적 EXP, 이 판이 닿은 이정표.
    // 판은 성장도의 시작 Level(그 Level에 닿는 누적 EXP)에서 시작하고, 목표 Level(다음 이정표)에 닿으면 이정표에 닿는다.
    // Level은 목표 Level을 넘지 않는다 — 이정표에 닿은 판은 그 Step에서 끝나고, 다음 판은 이정표 Level에서 시작한다.
    public sealed class Hq
    {
        public HqGrowthDefinition Growth { get; }

        public int Stage { get; } // 성장도(판을 시작할 때 도달해 있던 이정표의 수).

        public float GrowthTime { get; } // Level업마다 더하는 보너스 시간.

        public int StartLevel { get; } // 이 판이 시작한 Level.

        public int GoalLevel { get; } // 이 판의 목표 Level(다음 이정표의 Level). 0이면 목표가 없다(마지막 이정표 뒤).

        // 이 판의 전장 배율(성장도가 정하고 판 동안 같다). 출현 띠와 화면(카메라·화면 크기가 고정인 표시)이 이 배율로 넓어진다.
        public float FieldScale { get; }

        public long Exp { get; private set; } // 누적 EXP(시작 Level에 닿는 EXP부터 센다).

        public int Level { get; private set; }

        public bool IsMaxLevel => Level >= Growth.MaxLevel;

        public long? NextLevelExp => Growth.ExpToReach(Level + 1); // 다음 Level까지 필요한 누적 EXP 양.

        // 이 판이 닿은 이정표. 없으면 null이다. 있으면 판은 그 Step에서 끝났다.
        public HqMilestone Milestone { get; private set; }

        public bool ReachedMilestone => Milestone != null;

        // 결산 뒤의 성장도: 이정표에 닿았으면 +1.
        public int NextStage => ReachedMilestone ? Stage + 1 : Stage;

        // 지금 Level의 임계값에서 다음 임계값까지 몇 %인가(0 ~ 1). 마지막 Level이거나 이정표에 닿았으면 1이다.
        // EXP는 사망 순간에 들고 Level은 Step의 5 자리에서 오르므로, 그 사이에는 1에서 멈춘다.
        public float Progress => ReachedMilestone ? 1 : Growth.ProgressAt(Level, Exp);

        // stage: 판을 시작할 때의 성장도(진행 상태의 것).
        internal Hq(
            HqGrowthDefinition growth,
            float growthTime,
            int stage = HqGrowthDefinition.StartStage)
        {
            Growth = growth;

            if (float.IsNaN(growthTime) || float.IsInfinity(growthTime) || growthTime < 0)
                throw new ArgumentOutOfRangeException(nameof(growthTime), "0 이상의 유한한 값이 필요하다.");

            GrowthTime = growthTime;
            Stage = stage;
            StartLevel = growth.StartLevelAt(stage);
            GoalLevel = growth.GoalLevelAt(stage);
            FieldScale = growth.FieldScaleAt(stage);
            Level = StartLevel;
            Exp = growth.ExpToReach(StartLevel) ?? 0;
        }

        internal void AddExp(long exp) => Exp = checked(Exp + exp);

        internal int RaiseLevels()
        {
            int raised = 0;

            while (!AtGoal && NextLevelExp is long next && Exp >= next)
            {
                Level++;
                raised++;
            }

            if (Milestone == null && AtGoal)
                Milestone = Growth.NextMilestoneAt(Stage);

            return raised;
        }

        private bool AtGoal => GoalLevel != HqGrowthDefinition.NoGoal && Level >= GoalLevel;
    }
}
