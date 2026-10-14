using System;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class BreakerData
    {
        public float Damage;
        public float Interval;             // 공격 주기(초).
        public float Radius;               // 조준점을 중심으로 한 공격 원의 반지름.
        public float CritChance;           // 한 Tick이 치명타일 확률(0 ~ 1).
        public float CritDamage;           // 치명타 피해 보너스(0 이상). 1이면 +100%(2배)
        public float MoonDuration;         // 달 버프 중첩 하나의 지속 시간(초)
        public float MoonSpeedBonus;       //
        public float MoonRadiusBonus;      //
        public float MoonRadiusDecay;      // 달 범위 체감(0 ~ 1 미만). 0이면 선형(달 수 × 보너스), 0보다 크면 보너스가 상한이 되고 달 n개는 상한 × (1 − d^n)
        public float MoonSpeedDecay;       // 달 공격 속도 체감(0 ~ 1 미만). 0이면 선형 중첩
        public float CometDuration;        // 혜성 버프 중첩 하나의 지속 시간(초)
        public float CometCritDamageBonus; //
        public float CometCritDecay;       // 혜성 치명타 피해 체감(0 ~ 1 미만). 0이면 선형 중첩
        public float PlanetBonusDamage;    // 행성에 주는 추가 피해
        public float StarBonusDamage;      // 별에 주는 추가 피해
        public float GalaxyBonusDamage;    // 은하에 주는 추가 피해
        public float HoldSlow = 0.05f;     // 누르고 있는 동안 공격 원 안의 적이 움직이는 비율(0 ~ 1). 0이면 완전히 멈춘다.
    }
}
