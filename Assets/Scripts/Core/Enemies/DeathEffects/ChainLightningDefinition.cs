using System;

namespace BlackHole.Core
{
    // 연쇄 번개: 죽은 자리에서 가장 가까운 적으로 번개가 옮겨 가며 피해를 준다(원작의 전기 천체, REFERENCE_ANALYSIS R3).
    // 한 번 옮겨 가는 거리(다음 적의 원 가장자리까지)는 Radius 이하, 한 줄기가 옮겨 가는 횟수는 MaxTargets 이하.
    // 갈래: 죽은 자리에서 줄기가 하나 나가고, BranchChance로 성공할 때마다 줄기가 하나 더 나간다(최대 MaxBranches).
    // 각 줄기가 MaxTargets만큼 연쇄하고, 한 번 맞힌 적은 어느 줄기도 다시 맞히지 않는다.
    // 치명타: 발동 한 번에 한 번 CritChance로 판정하고, 치명타면 그 발동의 모든 적중 피해에 CritMultiplier를 곱한다(Breaker와 별개).
    // 세부(가장자리까지 가장 가까운 순, 거리가 같으면 목록 순서, 갈래를 반복 판정)는 [임시]다(SYSTEM_CATALOG S06).
    // 전기 소행성과 전기 별은 서로 다른 성질이라 각자의 수치를 가진다.
    public sealed class ChainLightningDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.ChainLightning;

        // 무한 연쇄를 막는 상한은 MaxTargets 자체다. 이 값은 저작 실수(지나치게 큰 수)만 막는다.
        public const int MaxTargetsLimit = 64;
        // 한 번의 발동에서 나가는 줄기의 상한.
        public const int MaxBranches = 8;

        public float Damage { get; }
        public float Radius { get; }
        public int MaxTargets { get; }
        public float BranchChance { get; }
        public float CritChance { get; }
        public float CritMultiplier { get; }

        public ChainLightningDefinition(float damage, float radius, int maxTargets, float branchChance = 0, float critChance = 0, float critMultiplier = 1)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));

            if (maxTargets < 1 || maxTargets > MaxTargetsLimit)
                throw new ArgumentOutOfRangeException(nameof(maxTargets), $"1부터 {MaxTargetsLimit}까지의 정수가 필요하다.");

            MaxTargets = maxTargets;
            BranchChance = Chance(branchChance, nameof(branchChance));
            CritChance = Chance(critChance, nameof(critChance));
            CritMultiplier = DefinitionGuard.Positive(critMultiplier, nameof(critMultiplier));
        }

        private static float Chance(float value, string name)
        {
            if (float.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, "0부터 1까지의 값이 필요하다.");

            return value;
        }
    }
}
