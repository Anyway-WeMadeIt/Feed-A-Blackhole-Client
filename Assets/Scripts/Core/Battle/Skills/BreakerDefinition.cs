using System;

namespace BlackHole.Core
{
    public sealed class BreakerDefinition
    {
        public float Damage { get; }
        public float Interval { get; } // 공격 주기(초). 노드 속도 배율(Speed)만 반영한 값이다.

        // 노드가 반영된 공격 속도 배율(기본 1). Interval은 이미 이 배율로 나눈 값이다.
        // 달 속도 보너스는 이 배율에 더한다(곱하지 않는다): BreakerSkill.CurrentSpeed = Speed + 달 보너스 합.
        public float Speed { get; }
        public float Radius { get; } // 공격 원의 기본 반지름(노드 반영).
        public float CritChance { get; }
        public float CritDamage { get; }
        public float MoonDuration { get; } // 달 버프 중첩 하나의 지속 시간(초)
        public float MoonSpeedBonus { get; }
        public float MoonRadiusBonus { get; }
        public float MoonRadiusDecay { get; } // 달 범위 체감(0 ~ 1 미만). 0이면 선형, 0보다 크면 MoonRadiusBonus가 상한이고 달 n개는 상한 × (1 − d^n)

        // 달 중첩의 공격 속도 체감(0 ~ 1 미만). 0이면 선형(중첩 수 × 보너스).
        // d면 n번째 달은 보너스 × d^(n−1)만 더해 합이 보너스 / (1 − d)에 수렴한다 — 한 개는 제값, 여덟 개는 과하지 않게.
        public float MoonSpeedDecay { get; }
        public float CometDuration { get; } // 혜성 버프 중첩 하나의 지속 시간(초)
        public float CometCritDamageBonus { get; }
        public float CometCritDecay { get; } // 혜성 치명타 피해 보너스의 체감(0 ~ 1 미만). 0이면 선형(중첩 수 × 보너스)
        public float PlanetBonusDamage { get; } // 행성 보너스 피해
        public float StarBonusDamage { get; } // 별 보너스 피해
        public float GalaxyBonusDamage { get; } // 은하 보너스 피해
        // 누르고 있는 동안(조준점이 있는 동안) 공격 원 안의 적이 움직이는 비율(0 ~ 1). 0이면 멈추고 1이면 평소처럼 공전한다.
        public float HoldSlow { get; }

        public BreakerDefinition(
            float damage,
            float interval,
            float radius,
            float critChance,
            float critDamage,
            float moonDuration,
            float moonSpeedBonus,
            float moonRadiusBonus,
            float cometDuration,
            float cometCritDamageBonus,
            float planetBonusDamage,
            float starBonusDamage,
            float galaxyBonusDamage,
            float holdSlow = 0.05f,
            float moonSpeedDecay = 0,
            float speed = 1,
            float cometCritDecay = 0,
            float moonRadiusDecay = 0)
        {
            Damage = DefinitionGuard.Positive(damage, nameof(damage));
            Interval = DefinitionGuard.Positive(interval, nameof(interval));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));

            if (float.IsNaN(critChance) || critChance < 0 || critChance > 1)
                throw new ArgumentOutOfRangeException(nameof(critChance), "0부터 1까지의 값이 필요하다.");

            CritChance = critChance;
            CritDamage = NotNegative(critDamage, nameof(critDamage));
            MoonDuration = DefinitionGuard.Positive(moonDuration, nameof(moonDuration));
            MoonSpeedBonus = NotNegative(moonSpeedBonus, nameof(moonSpeedBonus));
            MoonRadiusBonus = NotNegative(moonRadiusBonus, nameof(moonRadiusBonus));

            if (float.IsNaN(moonSpeedDecay) || moonSpeedDecay < 0 || moonSpeedDecay >= 1)
                throw new ArgumentOutOfRangeException(nameof(moonSpeedDecay), "0 이상 1 미만이어야 한다(0 = 선형 중첩).");

            MoonSpeedDecay = moonSpeedDecay;
            CometDuration = DefinitionGuard.Positive(cometDuration, nameof(cometDuration));
            CometCritDamageBonus = NotNegative(cometCritDamageBonus, nameof(cometCritDamageBonus));

            if (float.IsNaN(cometCritDecay) || cometCritDecay < 0 || cometCritDecay >= 1)
                throw new ArgumentOutOfRangeException(nameof(cometCritDecay), "0 이상 1 미만이어야 한다(0 = 선형 중첩).");

            CometCritDecay = cometCritDecay;

            if (float.IsNaN(moonRadiusDecay) || moonRadiusDecay < 0 || moonRadiusDecay >= 1)
                throw new ArgumentOutOfRangeException(nameof(moonRadiusDecay), "0 이상 1 미만이어야 한다(0 = 선형 중첩).");

            MoonRadiusDecay = moonRadiusDecay;
            PlanetBonusDamage = NotNegative(planetBonusDamage, nameof(planetBonusDamage));
            StarBonusDamage = NotNegative(starBonusDamage, nameof(starBonusDamage));
            GalaxyBonusDamage = NotNegative(galaxyBonusDamage, nameof(galaxyBonusDamage));

            if (float.IsNaN(holdSlow) || holdSlow < 0 || holdSlow > 1)
                throw new ArgumentOutOfRangeException(nameof(holdSlow), "0부터 1까지의 값이 필요하다.");

            HoldSlow = holdSlow;
            Speed = DefinitionGuard.Positive(speed, nameof(speed));
        }

        private static float NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "0 이상의 유한한 값이 필요하다.");
            return value;
        }

        public BreakerDefinition Upgraded(UpgradeStatValues upgrades)
        {
            float speed = 1 + upgrades.GainOf(UpgradeStat.BreakerSpeed) / 100;

            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"Breaker 공격 속도는 0보다 커야 한다. 노드 반영 값: {speed}.");

            return new BreakerDefinition(
                Damage + upgrades.GainOf(UpgradeStat.BreakerDamage),
                Interval / speed,
                Radius * (1 + upgrades.GainOf(UpgradeStat.BreakerRadius) / 100),
                Math.Min(1, CritChance + upgrades.GainOf(UpgradeStat.BreakerCritChance) / 100),
                CritDamage + upgrades.GainOf(UpgradeStat.BreakerCritBonus) / 100,
                MoonDuration + upgrades.GainOf(UpgradeStat.MoonPlanetDuration),
                MoonSpeedBonus + upgrades.GainOf(UpgradeStat.MoonPlanetSpeedScale) / 100,
                MoonRadiusBonus + upgrades.GainOf(UpgradeStat.MoonPlanetRangeScale) / 100,
                CometDuration + upgrades.GainOf(UpgradeStat.CometDuration),
                CometCritDamageBonus + upgrades.GainOf(UpgradeStat.CometCritBonus) / 100,
                PlanetBonusDamage + upgrades.GainOf(UpgradeStat.BreakerPlanetBonusDamage),
                StarBonusDamage + upgrades.GainOf(UpgradeStat.BreakerStarBonusDamage),
                GalaxyBonusDamage + upgrades.GainOf(UpgradeStat.BreakerGalaxyBonusDamage),
                HoldSlow,
                MoonSpeedDecay,
                Speed * speed,
                CometCritDecay,
                MoonRadiusDecay);
        }
    }
}
