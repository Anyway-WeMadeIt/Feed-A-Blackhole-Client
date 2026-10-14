using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 한 게임의 콘텐츠 세트: 값을 직접 갖지 않고 콘텐츠 에셋들을 가리키는 진입점이다.
    // GameBootstrap은 이것 하나만 알고, GameContentLoader가 이것으로 게임 정의(GameContent)와 노드 트리를 만든다.
    [CreateAssetMenu(fileName = "GameContentSetup", menuName = "BlackHole/Game Content Setup")]
    public sealed class GameContentSetup : ScriptableObject
    {
        [Tooltip("판 규칙(제한 시간, 처치 시간 추가).")]
        [SerializeField] private BattleRules _battleRules;
        [Tooltip("스킬 기본 수치(Breaker).")]
        [SerializeField] private SkillSetup _skills;
        [Tooltip("적 종류 목록.")]
        [SerializeField] private EnemyCatalog _enemies;
        [Tooltip("출현 배치와 전투 시작 공급.")]
        [SerializeField] private EnemySupplySetup _supply;
        [Tooltip("블랙홀 성장(Level 사다리, 이정표).")]
        [SerializeField] private HqGrowthSetup _growth;
        [Tooltip("노드 목록(노드 콘텐츠 + 배치).")]
        [SerializeField] private NodeCatalog _nodes;

        // 적 화면이 외형(스프라이트·색)을 읽는다.
        public EnemyCatalog Enemies => _enemies;
        public NodeCatalog Nodes => _nodes;

        // 연결하지 않은 칸. 비어 있어야 불러올 수 있다(GameContentLoader).
        internal IReadOnlyList<string> MissingReferences()
        {
            var missing = new List<string>();

            if (_battleRules == null) missing.Add("판 규칙(BattleRules)");
            if (_skills == null) missing.Add("스킬 설정(SkillSetup)");
            if (_enemies == null) missing.Add("적 종류 목록(EnemyCatalog)");
            if (_supply == null) missing.Add("적 공급 설정(EnemySupplySetup)");
            if (_growth == null) missing.Add("블랙홀 성장 설정(HqGrowthSetup)");
            if (_nodes == null) missing.Add("노드 목록(NodeCatalog)");

            return missing;
        }

        // Core 저작 형식을 채운다. 적 종류와 공급은 같은 적 콘텐츠를 함께 채운다. 검증은 ContentLoader가 한다.
        internal ContentData ToData()
        {
            var data = new ContentData();
            _battleRules.WriteTo(data);
            _skills.WriteTo(data);
            _enemies.WriteTo(data.Enemies);
            _supply.WriteTo(data.Enemies);
            data.Growth = _growth.ToData();
            return data;
        }
    }
}
