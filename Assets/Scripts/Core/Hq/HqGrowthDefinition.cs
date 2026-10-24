using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장의 공유 정의:
    // 1. 모든 판이 함께 쓰는 Level 사다리 하나(누적 EXP)
    // 2. 이정표(Level이 커지는 순서). 판이 다음 이정표의 Level에 닿으면 그 Step에서 판이 끝나고 성장도가 1 오른다.
    //
    // 성장도 = 도달한 이정표의 수(0 ~ 이정표 수). 성장도가 정하는 것은 판의 시작 Level·목표 Level과 전장 배율이다:
    // - 시작 Level: 성장도 0이면 0, 아니면 마지막으로 도달한 이정표의 Level.
    // - 목표 Level: 다음 이정표의 Level. 마지막 이정표 뒤에는 목표가 없다(판은 시간으로만 끝난다).
    // - 전장 배율: 성장도 0이면 1, 아니면 마지막으로 도달한 이정표의 배율(HqMilestone.FieldScale).
    public sealed class HqGrowthDefinition
    {
        public const int StartStage = 0;
        public const int StartLevel = 0;
        public const int NoGoal = 0;
        public const float StartFieldScale = 1f;

        // LevelExp[i]는 Level (i + 1)에 닿는 누적 EXP(Level 0 = EXP 0에서 센다). 양수이고 앞 줄보다 크다.
        public IReadOnlyList<long> LevelExp { get; }

        public int MaxLevel => StartLevel + LevelExp.Count;

        // 이정표(Level이 커지는 순서). Milestones[s]가 성장도 s의 판이 노리는 이정표다.
        public IReadOnlyList<HqMilestone> Milestones { get; }

        public int MaxStage => StartStage + Milestones.Count;

        // 은하가 나오기 시작하는 성장도. 0이면 은하가 없다(GalaxyTierAt은 늘 NoGalaxy).
        public int GalaxyStage { get; }

        public HqGrowthDefinition(
            IReadOnlyList<long> levelExp,
            IReadOnlyList<HqMilestone> milestones = null,
            int galaxyStage = 0)
        {
            if (levelExp == null)
                throw new ArgumentNullException(nameof(levelExp));

            var exps = new long[levelExp.Count];

            for (int i = 0; i < exps.Length; i++)
            {
                long exp = levelExp[i];

                if (exp <= 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(levelExp), $"Level {i + 1}의 누적 EXP는 양수여야 한다. 받은 값: {exp}.");

                if (i > 0 && exp <= exps[i - 1])
                    throw new ArgumentOutOfRangeException(nameof(levelExp),
                        $"Level {i + 1}의 누적 EXP {exp}는 Level {i}의 {exps[i - 1]}보다 커야 한다.");

                exps[i] = exp;
            }

            LevelExp = Array.AsReadOnly(exps);

            var marks = milestones != null
                ? new HqMilestone[milestones.Count]
                : Array.Empty<HqMilestone>();

            for (int i = 0; i < marks.Length; i++)
            {
                HqMilestone mark =
                    milestones[i] ?? throw new ArgumentException(
                        $"이정표 {i}가 null이다.", nameof(milestones));

                if (mark.Level > MaxLevel)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 Level {mark.Level}은 Level 사다리 안(최대 {MaxLevel})이어야 한다.");

                if (i > 0 && mark.Level <= marks[i - 1].Level)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 Level {mark.Level}은 앞 이정표의 {marks[i - 1].Level}보다 커야 한다.");

                if (i > 0 && mark.TargetGold <= marks[i - 1].TargetGold)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 목표 잔액 {mark.TargetGold}는 앞 이정표의 {marks[i - 1].TargetGold}보다 커야 한다.");

                // 카메라는 이정표마다 넓어지기만 한다(원작). 같은 배율은 허용한다(그 이정표에서는 넓어지지 않음).
                if (i > 0 && mark.FieldScale < marks[i - 1].FieldScale)
                    throw new ArgumentOutOfRangeException(
                        nameof(milestones), $"이정표 {i}의 전장 배율 {mark.FieldScale}는 앞 이정표의 {marks[i - 1].FieldScale}보다 작을 수 없다.");

                marks[i] = mark;
            }

            Milestones = Array.AsReadOnly(marks);

            if (galaxyStage < 0 || galaxyStage > MaxStage)
                throw new ArgumentOutOfRangeException(nameof(galaxyStage), $"은하 시작 성장도는 0(없음)부터 {MaxStage}까지다. 받은 값: {galaxyStage}.");

            GalaxyStage = galaxyStage;
        }

        public const int NoGalaxy = -1;

        // 성장도 stage의 판에 나오는 은하의 색 등급(0 = 빨강, 이정표마다 +1). 은하가 없는 판이면 NoGalaxy.
        // 색 수를 넘으면 마지막 색(보라)이다 — 마지막 이정표 뒤의 판도 보라 은하가 나온다.
        public int GalaxyTierAt(int stage)
        {
            RequireStage(stage);
            return GalaxyStage > 0 && stage >= GalaxyStage ? stage - GalaxyStage : NoGalaxy;
        }

        // 성장도 stage의 판이 시작하는 Level.
        public int StartLevelAt(int stage)
        {
            RequireStage(stage);
            return stage == StartStage ? StartLevel : Milestones[stage - StartStage - 1].Level;
        }

        // 성장도 stage의 판이 노리는 이정표. 마지막 이정표 뒤면 null.
        public HqMilestone NextMilestoneAt(int stage)
        {
            RequireStage(stage);
            return stage < MaxStage ? Milestones[stage - StartStage] : null;
        }

        // 성장도 stage의 판의 목표 Level(다음 이정표의 Level). 없으면 NoGoal.
        public int GoalLevelAt(int stage) => NextMilestoneAt(stage)?.Level ?? NoGoal;

        // 성장도 stage의 판의 전장 배율: 성장도 0이면 StartFieldScale, 아니면 마지막으로 도달한 이정표의 배율.
        public float FieldScaleAt(int stage)
        {
            RequireStage(stage);
            return stage == StartStage ? StartFieldScale : Milestones[stage - StartStage - 1].FieldScale;
        }

        // 성장도 stage의 판의 공전 모양: 성장도 0이면 기본, 아니면 마지막으로 도달한 이정표의 모양(HqMilestone.Orbit).
        public OrbitShape OrbitAt(int stage)
        {
            RequireStage(stage);
            return stage == StartStage ? OrbitShape.Default : Milestones[stage - StartStage - 1].Orbit;
        }

        // 이 Level에 닿는 누적 EXP. Level 0은 0이다. 사다리 밖이면 null이다.
        public long? ExpToReach(int level)
        {
            if (level == StartLevel)
                return 0;

            int index = level - StartLevel - 1;
            return index >= 0 && index < LevelExp.Count ? LevelExp[index] : (long?)null;
        }

        // Level level에서 누적 EXP exp일 때, 그 Level의 임계값에서 다음 임계값까지 몇 %인가(0 ~ 1). 마지막 Level이면 1이다.
        public float ProgressAt(int level, long exp)
        {
            long? next = ExpToReach(level + 1);
            long? from = ExpToReach(level);

            if (!next.HasValue || !from.HasValue)
                return 1;

            return (float)Math.Max(0, Math.Min(1, (double)(exp - from.Value) / (next.Value - from.Value)));
        }

        // 성장도 stage의 판이 Level level·누적 EXP exp에 이르렀을 때, 시작 체크포인트에서 목표 이정표까지 간 몫(0 ~ 1):
        // 오른 Level 수 + 다음 Level까지의 몫을, 목표까지의 Level 수로 나눈다. 목표가 없으면(마지막 이정표 뒤) 1.
        // ProgressAt은 Level 하나 안의 몫이고, 이것은 판 하나(이정표 구간)의 몫이다.
        public float StageFractionAt(int stage, int level, long exp)
        {
            int start = StartLevelAt(stage);
            int goal = GoalLevelAt(stage);

            if (goal <= start)
                return 1;

            double gained = level - start + ProgressAt(level, exp);
            return (float)Math.Max(0, Math.Min(1, gained / (goal - start)));
        }

        private void RequireStage(int stage)
        {
            if (stage < StartStage || stage > MaxStage)
                throw new ArgumentOutOfRangeException(nameof(stage), $"성장도는 {StartStage}부터 {MaxStage}까지다. 받은 값: {stage}.");
        }
    }
}
