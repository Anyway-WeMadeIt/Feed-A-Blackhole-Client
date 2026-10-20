namespace BlackHole.Core
{
    // 처치 버프 — 달(행성의 성질): Breaker에 달 중첩 하나를 더한다. 달 중첩은 공격 속도와 공격 범위를 함께 올린다.
    // 중첩의 지속 시간과 중첩당 보너스는 적이 아니라 받는 Breaker의 수치다
    // (BreakerDefinition.MoonDuration·MoonSpeedBonus·MoonRadiusBonus, 판마다 고정).
    // 중첩은 받은 것마다 따로 끝나고, 보너스는 중첩끼리 합연산한 뒤 노드가 반영된 공격 속도·반지름에 곱한다(BreakerSkill).
    // 달 성질의 동시 상한(MaxActive)은 화면의 달과 남은 중첩을 합쳐 센다(World.CountActive). 다 찼으면 달이 나오지 않으므로 중첩도 그 수를 넘지 않는다.
    // 달은 사망 효과의 피해를 받지 않는다(DeathEffects.CanBeStruck) — Breaker로만 죽는다. 효과가 달을 죽이면 사망과 중첩 추가 사이에 틈이 생겨 위 상한이 어긋난다.
    // 피해를 만들지 않는다.
    public sealed class MoonBuffDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.MoonBuff;
    }
}
