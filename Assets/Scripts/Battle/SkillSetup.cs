using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 스킬 설정 에셋: 스킬 종류마다 기본 수치 칸을 따로 둔다(한 칸에 모든 종류의 수치를 섞지 않는다).
    // 칸의 값은 Core의 저작 형식(BreakerData)으로 옮겨져 ContentLoader가 검증한다.
    // 값은 이 에셋을 Inspector에서 직접 고친다.
    [CreateAssetMenu(fileName = "SkillSetup", menuName = "BlackHole/Skill Setup")]
    public sealed class SkillSetup : ScriptableObject
    {
        [Header("Breaker: 조준점 중심 원 안의 적 전부를 주기마다 친다")]
        [SerializeField] private float breakerDamage = 2;
        [Tooltip("공격 주기(초).")]
        [SerializeField] private float breakerInterval = 1;
        [Tooltip("공격 원의 반지름. 화면의 범위 표시도 이 값이다.")]
        [SerializeField] private float breakerRadius = 1.5f;
        [Tooltip("한 Tick이 치명타일 확률(0 ~ 1).")]
        [SerializeField] private float breakerCritChance;
        [Tooltip("치명타 피해 보너스(0 이상). 치명타 Tick의 피해 = 피해 × (1 + 보너스). 1이면 +100%(2배). 혜성 버프 중에는 중첩 보너스만큼 곱해 키운다.")]
        [SerializeField] private float breakerCritDamage = 1;

        // 처치 버프: 적은 버프 종류만 정하고, 시간과 중첩당 수치는 여기서 정한다(판마다 고정). 중첩은 받은 것마다 따로 끝난다.
        [Header("Breaker 달 버프: 공격 속도와 공격 범위를 함께 올린다")]
        [Tooltip("달 중첩 하나의 지속 시간(초).")]
        [SerializeField] private float breakerMoonDuration = 5;
        [Tooltip("달 중첩 하나의 공격 속도 보너스(0 이상). 0.2면 +20%p. 중첩끼리 더한 합(체감 적용)을 노드가 반영된 공격 속도 배율에 더한다(곱하지 않는다).")]
        [SerializeField] private float breakerMoonSpeedBonus = 0.2f;
        [Tooltip("달 중첩 하나의 공격 범위(반지름) 보너스(0 이상). 0.1이면 +10%. 중첩끼리 더한 뒤 노드가 반영된 반지름에 곱한다. 반지름은 2배(BreakerSkill.MoonRadiusMaxScale)를 넘지 않는다.")]
        [SerializeField] private float breakerMoonRadiusBonus = 0.1f;
        [Tooltip("달 범위 보너스의 체감(0 ~ 1 미만). 0이면 선형(달 수 × 보너스). 0보다 크면 범위 보너스(이 값 + 노드 moonPlanet.rangeScale)가 상한이 되고, 달 n개는 상한 × (1 − d^n)만큼 커진다. 0.7이면 첫 달 30%, 둘째 51%, 셋째 66%.")]
        [SerializeField, Range(0, 0.95f)] private float breakerMoonRadiusDecay = 0f;
        [Tooltip("달 공격 속도 보너스의 체감(0 ~ 1 미만). 0이면 선형(달 수 × 보너스). 0.7이면 n번째 달은 보너스 × 0.7^(n−1)만 더해, 합이 보너스 ÷ 0.3에 수렴한다.")]
        [SerializeField, Range(0, 0.95f)] private float breakerMoonSpeedDecay = 0f;

        [Header("Breaker 혜성 버프: 확정 치명타, 치명타 피해를 올린다")]
        [Tooltip("혜성 중첩 하나의 지속 시간(초).")]
        [SerializeField] private float breakerCometDuration = 5;
        [Tooltip("혜성 중첩 하나의 치명타 피해 보너스 증가(0 이상). 0.5면 +50%. 중첩끼리 더한 뒤 치명타 피해 보너스에 곱한다.")]
        [SerializeField] private float breakerCometCritDamageBonus = 0.5f;
        [Tooltip("혜성 치명타 피해 보너스의 체감(0 ~ 1 미만). 0이면 선형(혜성 수 × 보너스). 0.7이면 n번째 혜성은 보너스 × 0.7^(n−1)만 더해, 합이 보너스 ÷ 0.3에 수렴한다(혜성 비 8개가 한 번에 과해지지 않게).")]
        [SerializeField, Range(0, 0.95f)] private float breakerCometCritDecay = 0f;

        [Header("Breaker 붙잡기: 누르고 있는 동안 공격 원 안의 적이 거의 멈춘다")]
        [Tooltip("누르고 있는 동안 공격 원 안의 적이 움직이는 비율(0 ~ 1). 0이면 완전히 멈추고 1이면 평소처럼 공전한다.")]
        [SerializeField, Range(0, 1)] private float breakerHoldSlow = 0.05f;

        public void WriteTo(ContentData data)
        {
            data.Breaker = new BreakerData
            {
                Damage = breakerDamage,
                Interval = breakerInterval,
                Radius = breakerRadius,
                CritChance = breakerCritChance,
                CritDamage = breakerCritDamage,
                MoonDuration = breakerMoonDuration,
                MoonSpeedBonus = breakerMoonSpeedBonus,
                MoonRadiusBonus = breakerMoonRadiusBonus,
                MoonRadiusDecay = breakerMoonRadiusDecay,
                MoonSpeedDecay = breakerMoonSpeedDecay,
                CometDuration = breakerCometDuration,
                CometCritDamageBonus = breakerCometCritDamageBonus,
                CometCritDecay = breakerCometCritDecay,
                HoldSlow = breakerHoldSlow,
            };
        }
    }
}
