using System;

namespace BlackHole.Core
{
    // 이정표 하나: 판이 이 Level에 닿으면 그 Step에서 판이 끝나고 성장도가 1 오른다.
    // 보상은 고정 금액이 아니라 목표 잔액이다: 결산이 진행 상태의 Gold가 TargetGold가 되도록 차액을 준다(그 판에서 번 Gold는 버린다).
    // 소지금이 이미 더 많으면 깎지 않는다(차액 0). 금액은 기획자가 정한다 [사용자].
    // 전장 배율: 이 이정표에 닿은 뒤의 판이 쓰는 배율(성장도 0 = 1). 카메라가 보여 주는 범위와 적 출현 띠가 이 배율로 넓어진다.
    // 적·Breaker·블랙홀의 크기(월드)는 바꾸지 않으므로 화면에서는 그만큼 작아진다. 원작 실측: Level 10 → 1.68, 20 → 2.27, 30 → 2.83.
    public sealed class HqMilestone
    {
        public int Level { get; }
        public long TargetGold { get; }
        public float FieldScale { get; }

        public HqMilestone(int level, long targetGold, float fieldScale)
        {
            if (level <= HqGrowthDefinition.StartLevel)
                throw new ArgumentOutOfRangeException(nameof(level), $"이정표 Level은 {HqGrowthDefinition.StartLevel + 1} 이상이어야 한다. 받은 값: {level}.");

            if (float.IsNaN(fieldScale) || float.IsInfinity(fieldScale) || fieldScale < HqGrowthDefinition.StartFieldScale)
                throw new ArgumentOutOfRangeException(nameof(fieldScale),
                    $"전장 배율은 {HqGrowthDefinition.StartFieldScale} 이상의 유한한 값이어야 한다(성장도 0이 {HqGrowthDefinition.StartFieldScale}). 받은 값: {fieldScale}.");

            Level = level;
            TargetGold = DefinitionGuard.NotNegative(targetGold, nameof(targetGold));
            FieldScale = fieldScale;
        }

        // 진행 상태의 Gold가 gold일 때 결산이 더하는 Gold.
        public long RewardFor(long gold) => Math.Max(0, TargetGold - gold);
    }
}
