using System;

namespace BlackHole.Core
{
    // 폭발(슈퍼노바 별): 죽은 자리를 중심으로 반지름 안에 닿는 적(적의 크기 포함) 전부에게 한 번 고정 피해(Damage)를 준다.
    // Radius는 크기 1 별의 폭발 반지름이다. 큰 별은 별 반지름과 같은 배율로 넓게 터진다(DeathEffects, SizeRule.RadiusMultiplier).
    // 원작은 "현재체력 비례 슈퍼노바 대미지"(대상 현재 HP × 비율)였지만, 고정 수치로 바꿨다: 큰 적일수록 효과가 커지고 작은 적은 죽지 않던 것을 없앤다.
    // 노드 supernovaStar.damage가 피해를 더한다(원작 수치 ID는 supernovaStar.hpDamage).
    public sealed class ExplosionDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.Explosion;

        public float Damage { get; }
        public float Radius { get; }

        public ExplosionDefinition(float damage, float radius)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
        }

        // 대상 하나가 받는 피해. 대상과 상관없이 같다.
        internal float DamageTo(Enemy target) => Damage;
    }
}
