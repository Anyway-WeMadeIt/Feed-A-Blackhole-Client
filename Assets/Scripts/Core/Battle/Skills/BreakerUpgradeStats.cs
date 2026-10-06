namespace BlackHole.Core
{
    // Breaker가 공개하는 업그레이드 수치 이름. 노드의 업그레이드(Upgrade.Stat)가 이 이름으로 판의 Breaker 수치를 보정한다.
    // 업그레이드 시스템은 이 이름을 해석하지 않는다.
    public static class BreakerUpgradeStats
    {
        // 피해. 노드 예: 더하기 1.
        public const string Damage = "breaker.damage";
        // 공격 속도(기본 1). 주기는 기본 주기 ÷ 공격 속도다. 노드 예: 비율 0.25.
        public const string Speed = "breaker.speed";
        // 공격 원의 반지름. 노드 예: 비율 0.1.
        public const string Radius = "breaker.radius";
        // 한 Tick이 치명타일 확률(1을 넘지 않는다). 노드 예: 더하기 0.05.
        public const string CritChance = "breaker.crit-chance";
        // 치명타 피해 보너스(기본 1 = +100%). +100%에서 25%p 올리는 노드는 더하기 0.25(1 → 1.25).
        public const string CritDamage = "breaker.crit-damage";
        // 달 버프 중첩 하나의 지속 시간(초). 노드 예: 더하기 1.
        public const string MoonDuration = "breaker.moon-duration";
        // 달 버프 중첩 하나의 공격 속도 보너스(기본 0.2 = +20%). 노드 예: 더하기 0.05.
        public const string MoonSpeedBonus = "breaker.moon-speed-bonus";
        // 달 버프 중첩 하나의 공격 범위(반지름) 보너스(기본 0.1 = +10%). 노드 예: 더하기 0.05.
        public const string MoonRadiusBonus = "breaker.moon-radius-bonus";
        // 혜성 버프 중첩 하나의 지속 시간(초). 노드 예: 더하기 1.
        public const string CometDuration = "breaker.comet-duration";
        // 혜성 버프 중첩 하나의 치명타 피해 보너스 증가(기본 0.5 = +50%). 노드 예: 더하기 0.1.
        public const string CometCritDamageBonus = "breaker.comet-crit-damage-bonus";

        // 행성에 대한 브레이커 보너스 피해 : 0을 시작으로 기본 피해 및 치명타와의 계산 순서확인 필요
        public const string PlanetBonus = "breaker.planet-damage-bonus";
        // 별에 대한 브레이커의 보너스 대미지 : 0을 시작으로 기본 피해 및 치명타와의 계산 순서확인 필요
        public const string StarBonus = "breaker.star-damage-bonus";
    }
}
