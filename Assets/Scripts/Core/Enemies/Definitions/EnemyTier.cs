namespace BlackHole.Core
{
    // 색 등급 하나:
    // - 한 종류 안의 색 하나(빨주노초파보). 색이 기본 HP·기본 Gold·기본 EXP를 정함.
    // - 공식이 아니라 색마다 따로 정한다. 반지름은 색이 아니라 종류의 값이다(EnemyDefinition.Radius).
    public readonly struct EnemyTier
    {
        public float MaxHealth { get; }
        public long Gold { get; }
        public long Exp { get; }

        public EnemyTier(float maxHealth, long gold, long exp = 0)
        {
            MaxHealth = DefinitionGuard.Positive(maxHealth, nameof(maxHealth));
            Gold = DefinitionGuard.NotNegative(gold, nameof(gold));
            Exp = DefinitionGuard.NotNegative(exp, nameof(exp));
        }
    }
}
