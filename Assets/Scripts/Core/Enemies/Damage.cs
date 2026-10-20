using System;

namespace BlackHole.Core
{
    public readonly struct Damage
    {
        public float Amount { get; }
        // 피해를 준 스킬·효과. 정산의 스킬별 피해 같은 통계가 이 값으로 나눈다.
        public DamageSource Source { get; }
        // 사망 효과의 피해면 그 효과를 낸 특수 적의 종류. 같은 번개라도 전기 소행성과 전기 별을 나눈다. Breaker의 피해면 null.
        public EnemyType? SourceEnemyType { get; }
        public bool IsCritical { get; }

        public Damage(float amount, DamageSource source, bool isCritical = false, EnemyType? sourceEnemyType = null)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "피해량은 유한한 양수여야 한다.");

            Amount = amount;
            Source = source;
            SourceEnemyType = sourceEnemyType;
            IsCritical = isCritical;
        }
    }
}
