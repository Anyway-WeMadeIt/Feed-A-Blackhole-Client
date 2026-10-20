namespace BlackHole.Core
{
    // 중력장(은하 등): 특수 적이 죽은 자리에 고정된 중력장이 생겨 지속 시간 동안 범위 안의 적을 실제로 중심으로 끌어 뭉친다.
    // - 끌리는 적은 그동안 공전하지 않고 중심을 향해 초당 Force만큼 다가가며, 중심에 닿으면(적 반지름의 절반 안) 그 자리에 머문다.
    // - 중력장이 사라지면 끌려간 자리에서 다시 공전한다(공전은 위치만 보고 돈다 — EnemyBehaviors).
    // - 피해는 없다. 픽업은 끌리지 않는다. 특수 적도 끌린다(뭉친 뒤 그 효과가 더 많은 적에게 닿게).
    // 반지름은 죽은 적의 크기와 무관하다(폭발과 다르다, DeathEffects).
    public sealed class GravityFieldDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.GravityField;

        public float Radius { get; }   // 끌어들이는 범위.
        public float Force { get; }    // 끌리는 속도(초당 거리).
        public float Duration { get; } // 지속 시간(초).

        public GravityFieldDefinition(float radius, float force, float duration)
        {
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
            Force = DefinitionGuard.Positive(force, nameof(force));
            Duration = DefinitionGuard.Positive(duration, nameof(duration));
        }
    }
}
