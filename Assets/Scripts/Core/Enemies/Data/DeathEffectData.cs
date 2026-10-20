using System;

namespace BlackHole.Core
{
    // 사망 효과의 수치. 효과 종류(성질 종류가 정한다)마다 쓰는 칸이 다르다. 쓰지 않는 칸은 읽지 않는다.
    [Serializable]
    public sealed class DeathEffectData
    {
        // Golden(Gold 배율)
        public float Multiplier;
        // Golden 치명타 확률 발생 시 추가 골드 배율
        public float CritRewardScale;
        // ChainLightning, LaserBurst, Explosion(고정 피해)
        public float Damage;
        // ChainLightning(한 번 옮겨 가는 거리), Explosion(반경), GravityField(끌어들이는 범위)
        public float Radius;
        // ChainLightning(한 줄기가 옮겨 가는 최대 횟수)
        public int MaxTargets;
        // StarLightning(확률과 무관하게 늘 나가는 줄기의 수, 1 이상). ChainLightning은 늘 1이라 읽지 않는다.
        public int Branches;
        // ChainLightning, StarLightning(줄기가 하나 더 나갈 확률, 0 ~ 1)
        public float BranchChance;
        // ChainLightning, LaserBurst, Golden(치명타 확률 0 ~ 1, 치명타일 때 피해 배율)
        public float CritChance;
        public float CritMultiplier;
        // LaserBurst(레이저 너비)
        public float Width;
        // GravityField(끌리는 속도, 초당 거리)
        public float Force;
        // GravityField(지속 시간, 초)
        public float Duration;
        // MoonBuff·CometBuff는 쓰는 칸이 없다: 버프 시간과 수치는 Breaker의 것이다(BreakerData).
    }
}
