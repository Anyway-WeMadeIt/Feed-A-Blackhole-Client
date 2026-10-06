using System;

namespace BlackHole.Core
{
    // Breaker의 공유 정의: 기본 수치. 조준점을 중심으로 한 원에 닿는 적(적의 크기 포함) 전부를 주기마다 친다(GAME_RULES 6절).
    // 콘텐츠에 Breaker가 없으면 판에 Breaker가 없다. 실행 상태는 참가자마다의 BreakerSkill이 가진다.
    // 치명타는 Breaker의 수치다(원작의 Breaker 강화 축, 노드 수치 breaker.crit-chance). 모든 스킬의 공통 수치가 아니다(SKILL_SYSTEM_PLAN D2).
    // 처치 버프(달·혜성)의 지속 시간과 중첩당 수치도 Breaker의 수치다. 적은 어떤 버프를 주는지만 정하고 수치를 갖지 않는다.
    public sealed class BreakerDefinition
    {
        public float Damage { get; }
        // 공격 주기(초).
        public float Interval { get; }
        // 공격 원의 기본 반지름(노드 반영). 달 버프 중의 반지름은 BreakerSkill.CurrentRadius이고, 판정과 화면의 범위 표시는 그 값을 쓴다.
        public float Radius { get; }
        // 한 Tick이 치명타일 확률(0 ~ 1).
        public float CritChance { get; }
        // 치명타 피해 보너스(0 이상). 치명타 Tick의 피해 = 피해 × (1 + 보너스). 1이면 +100%(2배)다.
        // 혜성 버프 중에는 이 값에 (1 + 중첩 보너스의 합)을 곱한다(BreakerSkill).
        public float CritDamage { get; }
        // 달 버프 중첩 하나의 지속 시간(초), 공격 속도 보너스와 공격 범위(반지름) 보너스(0 이상, 0.2면 +20%). 두 보너스는 함께 적용된다.
        public float MoonDuration { get; }
        public float MoonSpeedBonus { get; }
        public float MoonRadiusBonus { get; }
        // 혜성 버프 중첩 하나의 지속 시간(초)과 치명타 피해 보너스 증가(0 이상, 0.5면 +50%).
        public float CometDuration { get; }
        public float CometCritDamageBonus { get; }

        // 행성, 별 보너스 피해
        public float PlanetDamageBonus { get; }
        public float StarDamageBunos { get; }

        public BreakerDefinition(float damage, float interval, float radius, float critChance, float critDamage,
            float moonDuration, float moonSpeedBonus, float moonRadiusBonus, float cometDuration, float cometCritDamageBonus,
            float planetBonus, float starBonus)
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
            CometDuration = DefinitionGuard.Positive(cometDuration, nameof(cometDuration));
            CometCritDamageBonus = NotNegative(cometCritDamageBonus, nameof(cometCritDamageBonus));

            PlanetDamageBonus = NotNegative(planetBonus, nameof(planetBonus));
            StarDamageBunos = NotNegative(starBonus, nameof(starBonus));
        }

        private static float NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "0 이상의 유한한 값이 필요하다.");
            return value;
        }

        // 업그레이드 표로 이 판의 Breaker 수치를 계산한다. 판 조립이 참가자마다 한 번 부르고, 판 동안 바뀌지 않는다.
        // 수치 이름은 BreakerUpgradeStats다. 각 수치의 기본값은 이 정의의 값이고, 공격 속도의 기본값은 1이다.
        // 표의 합성 규칙을 적용한 뒤 Breaker의 한계를 건다:
        // - 주기 = 기본 주기 ÷ 공격 속도 [임시]. 공격 속도 +25%(비율 0.25)면 주기가 1/1.25배다.
        // - 치명타 확률은 1을 넘지 않는다.
        // 한계 밖(0 이하의 피해·공격 속도·반지름·버프 시간, 음수 치명타 확률·치명타 피해 보너스·버프 보너스)은 예외다 — 노드 저작 오류이며 UpgradeContentCheck가 로드 때 찾는다.
        public BreakerDefinition Upgraded(UpgradeTable upgrades)
        {
            if (upgrades == null)
                throw new ArgumentNullException(nameof(upgrades));

            float speed = upgrades.Apply(BreakerUpgradeStats.Speed, 1);

            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(upgrades), $"Breaker 공격 속도는 0보다 커야 한다. 업그레이드 합: {speed}.");

            return new BreakerDefinition(
                upgrades.Apply(BreakerUpgradeStats.Damage, Damage),
                Interval / speed,
                upgrades.Apply(BreakerUpgradeStats.Radius, Radius),
                Math.Min(1, upgrades.Apply(BreakerUpgradeStats.CritChance, CritChance)),
                upgrades.Apply(BreakerUpgradeStats.CritDamage, CritDamage),
                upgrades.Apply(BreakerUpgradeStats.MoonDuration, MoonDuration),
                upgrades.Apply(BreakerUpgradeStats.MoonSpeedBonus, MoonSpeedBonus),
                upgrades.Apply(BreakerUpgradeStats.MoonRadiusBonus, MoonRadiusBonus),
                upgrades.Apply(BreakerUpgradeStats.CometDuration, CometDuration),
                upgrades.Apply(BreakerUpgradeStats.CometCritDamageBonus, CometCritDamageBonus),
                upgrades.Apply(BreakerUpgradeStats.PlanetDamageBonus, PlanetDamageBonus),
                upgrades.Apply(BreakerUpgradeStats.StarDamageBonus, StarDamageBunos));
        }
    }
}
