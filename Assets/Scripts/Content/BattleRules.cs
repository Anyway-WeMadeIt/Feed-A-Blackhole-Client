using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 판 규칙 에셋: 한 판의 제한 시간과 적을 파괴할 때 늘어나는 시간. 값은 이 에셋을 Inspector에서 직접 고친다.
    // 칸의 값은 Core의 저작 형식(BattleRulesData)으로 옮겨져 ContentLoader가 검증한다.
    // 모드마다 규칙이 달라지면 이 에셋을 바꿔 끼운다(GameContentSetup).
    [CreateAssetMenu(fileName = "BattleRules", menuName = "BlackHole/Battle Rules")]
    public sealed class BattleRules : ScriptableObject
    {
        [Tooltip("한 판의 제한 시간(초). 노드 timer가 늘린다.")]
        [SerializeField] private float timeLimit = 12;
        [Tooltip("적이 파괴될 때 시간 추가(노드 asteroid.timeChance 등)가 성공하면 제한 시간에 더하는 초. 원작 실측 0.3초.")]
        [SerializeField] private float killTimeBonus = 0.3f;

        [Tooltip("한 판에 파괴로 더할 수 있는 시간의 합(초). 0이면 제한 없음. 재생성·시간 추가 노드가 쌓이면 판이 끝나지 않으므로 둔다.")]
        [Min(0)]
        [SerializeField] private float maxKillTimeBonus = 0f;

        [Tooltip("시간 추가가 성공한 뒤 다음 판정까지 기다리는 초. 0이면 쿨타임 없음. 연쇄 처치가 시간을 몰아주지 않게 둔다.")]
        [Min(0)]
        [SerializeField] private float killTimeCooldown = 0f;

        internal void WriteTo(ContentData data) =>
            data.BattleRules = new BattleRulesData
            {
                TimeLimit = timeLimit,
                KillTimeBonus = killTimeBonus,
                MaxKillTimeBonus = maxKillTimeBonus,
                KillTimeCooldown = killTimeCooldown,
            };
    }
}
