namespace BlackHole.Core
{
    // 사망 효과 종류(DeathEffectDefinition.Type). 성질 종류(EnemyTraitType)가 정한다 — 저작하는 값이 아니다.
    public enum DeathEffectType
    {
        Golden,
        ChainLightning,
        Explosion,
        LaserBurst,
        MoonBuff,
        CometBuff,
        GravityField,
        // 전기 별 전용 번개. 지금은 연쇄 번개와 같은 규칙이지만 정의·기록·피해 출처·화면이 따로다(앞으로 따로 바뀐다).
        StarLightning,
    }
}
