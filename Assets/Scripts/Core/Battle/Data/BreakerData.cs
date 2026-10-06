using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class BreakerData
    {
        public float Damage;
        // 공격 주기(초).
        public float Interval;
        // 조준점을 중심으로 한 공격 원의 반지름.
        public float Radius;
        // 한 Tick이 치명타일 확률(0 ~ 1).
        public float CritChance;
        // 치명타 피해 보너스(0 이상). 1이면 +100%(2배).
        public float CritDamage;
        // 달 버프 중첩 하나의 지속 시간(초)과 공격 속도 보너스(0 이상), 공격 범위(반지름) 보너스(0 이상).
        public float MoonDuration;
        public float MoonSpeedBonus;
        public float MoonRadiusBonus;
        // 혜성 버프 중첩 하나의 지속 시간(초)과 치명타 피해 보너스 증가(0 이상).
        public float CometDuration;
        public float CometCritDamageBonus;

        // 행성, 별 데미지 보너스
        public float PlanetBonus;
        public float StarBonus;
    }
}
