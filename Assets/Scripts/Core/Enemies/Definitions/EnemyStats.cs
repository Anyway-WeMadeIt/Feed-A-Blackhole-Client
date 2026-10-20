namespace BlackHole.Core
{
    // 전투 Session에서 적 종류별 고정된 수치.
    public readonly struct EnemyStats
    {
        public float MaxHealth { get; }

        // 이동 속도(초당 거리). 부호가 공전 방향이다: 양수는 반시계, 음수는 시계방향.
        public float MoveSpeed { get; }

        // 반지름. 공격·사망 효과의 판정과 화면에 그리는 크기다. 크기(SizeRule)가 반영된 값이다.
        public float Radius { get; }

        // 이 적의 사망이 확정되는 순간 판의 Gold 합계에 드는 값.
        public long Gold { get; }

        // 이 적의 사망이 확정되는 순간 블랙홀에 드는 EXP.
        public long Exp { get; }

        public EnemyStats(float maxHealth, float moveSpeed, float radius, long gold, long exp = 0)
        {
            MaxHealth = DefinitionGuard.Positive(maxHealth, nameof(maxHealth));
            MoveSpeed = DefinitionGuard.NonZeroFinite(moveSpeed, nameof(moveSpeed));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
            Gold = DefinitionGuard.NotNegative(gold, nameof(gold));
            Exp = DefinitionGuard.NotNegative(exp, nameof(exp));
        }
    }
}
