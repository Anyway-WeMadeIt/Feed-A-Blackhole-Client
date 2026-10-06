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
        [Tooltip("달 중첩 하나의 공격 속도 보너스(0 이상). 0.2면 +20%. 중첩끼리 더한 뒤 노드가 반영된 공격 속도에 곱한다.")]
        [SerializeField] private float breakerMoonSpeedBonus = 0.2f;
        [Tooltip("달 중첩 하나의 공격 범위(반지름) 보너스(0 이상). 0.1이면 +10%. 중첩끼리 더한 뒤 노드가 반영된 반지름에 곱한다.")]
        [SerializeField] private float breakerMoonRadiusBonus = 0.1f;

        [Header("Breaker 혜성 버프: 확정 치명타, 치명타 피해를 올린다")]
        [Tooltip("혜성 중첩 하나의 지속 시간(초).")]
        [SerializeField] private float breakerCometDuration = 5;
        [Tooltip("혜성 중첩 하나의 치명타 피해 보너스 증가(0 이상). 0.5면 +50%. 중첩끼리 더한 뒤 치명타 피해 보너스에 곱한다.")]
        [SerializeField] private float breakerCometCritDamageBonus = 0.5f;

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
                CometDuration = breakerCometDuration,
                CometCritDamageBonus = breakerCometCritDamageBonus,
            };
        }
    }
}
