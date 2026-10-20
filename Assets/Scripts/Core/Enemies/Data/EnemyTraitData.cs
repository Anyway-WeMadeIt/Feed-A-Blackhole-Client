using System;

namespace BlackHole.Core
{
    // 특수 성질 한 줄: 성질 종류와 사망 효과의 수치. 사망 효과의 종류는 성질 종류가 정한다(EnemyContentLoader).
    [Serializable]
    public sealed class EnemyTraitData
    {
        public EnemyTraitType Type;
        public DeathEffectData Effect;
        // 이 성질의 동시 상한(살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩). 0이면 상한이 없다.
        public int MaxActive;
    }
}
