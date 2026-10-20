namespace BlackHole.Core
{
    // 피해를 준 스킬·효과(Damage.Source). 사망 효과 이름은 사망 효과 종류(DeathEffectType)와 같다.
    // 중력장(GravityField)은 피해 없이 적을 옮기기만 하므로 여기에 없다.
    public enum DamageSource
    {
        Breaker,
        ChainLightning,
        Explosion,
        LaserBurst,
        // 전기 별의 번개(StarLightningDefinition). 전기 소행성의 ChainLightning과 따로 센다.
        StarLightning,
    }
}
