using System;

namespace BlackHole.Core
{
    // 레이저 별: 죽은 자리를 지나는 무작위 방향의 직선 레이저가 화면을 가로질러 경로 위의 적 전부에게 피해를 준다.
    // 죽는 순간 경로가 정해지고, 예고 시간(DeathEffects, 0.4초)이 지난 뒤 쏜다.
    public sealed class LaserBurstDefinition : DeathEffectDefinition
    {
        public override DeathEffectType Type => DeathEffectType.LaserBurst;

        public float Damage { get; }
        public float Width { get; } // 레이저의 너비(경로 양쪽 거리의 합).
        public float CritChance { get; }
        public float CritMultiplier { get; } // 치명타일 때 피해에 곱하는 값.
        public float GalaxyBonusDamage { get; } // 은하를 맞힐 때 더하는 피해(치명타 배율도 함께 받는다). 0이면 없다.

        public LaserBurstDefinition(float damage, float width, float critChance, float critMultiplier, float galaxyBonusDamage = 0)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Width = DefinitionGuard.Positive(width, nameof(width));

            if (float.IsNaN(critChance) || critChance < 0 || critChance > 1)
                throw new ArgumentOutOfRangeException(nameof(critChance), "0부터 1까지의 값이 필요하다.");

            CritChance = critChance;
            CritMultiplier = DefinitionGuard.Positive(critMultiplier, nameof(critMultiplier));

            if (float.IsNaN(galaxyBonusDamage) || float.IsInfinity(galaxyBonusDamage) || galaxyBonusDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(galaxyBonusDamage), "0 이상의 유한한 값이 필요하다.");

            GalaxyBonusDamage = galaxyBonusDamage;
        }

        // 대상 하나가 받는 피해. 은하면 보너스를 더한 뒤 치명타 배율을 곱한다.
        internal float DamageTo(Enemy target, bool critical)
        {
            float amount = target.Definition.Type == EnemyType.Galaxy ? Damage + GalaxyBonusDamage : Damage;
            return critical ? amount * CritMultiplier : amount;
        }
    }
}
