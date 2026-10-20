namespace BlackHole.Core
{
    // 사망 효과의 정의. 특수 성질(EnemyTraitDefinition)이 가진다 — 종류가 아니다(전기 소행성은 전기 성질이 붙은 소행성이다).
    // 성질이 붙은 적(특수 적)도 사망 효과의 피해를 받는다(달·픽업 제외). 효과가 특수 적을 죽이면 그 효과가 화면에서 닿는 시각에 이어 발동한다(DeathEffects).
    // 새 효과: 하위 정의 + DeathEffectType 값 + DeathEffects.Resolve 분기 + EnemyContentLoader의 성질 → 효과 대응.
    public abstract class DeathEffectDefinition
    {
        private protected DeathEffectDefinition() { }

        public abstract DeathEffectType Type { get; }
    }
}
