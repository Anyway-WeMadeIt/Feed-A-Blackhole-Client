using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 노드 콘텐츠의 원본 시트 4개(CSV): UpgradeStats · Nodes · NodeCost · NodeEffects.
    // 시트에서 내려받은 CSV를 그대로 둔다. 값을 고치는 곳은 시트이고, 이 에셋은 CSV를 가리키기만 한다.
    // 노드 ID·Rank·비용·효과·수치 정의가 여기서 온다. 칸·선·시작 노드는 노드 목록(NodeCatalog)이 가진다.
    [CreateAssetMenu(fileName = "NodeContent", menuName = "BlackHole/Node Content")]
    public sealed class NodeContentSource : ScriptableObject
    {
        [SerializeField] private TextAsset _upgradeStats;
        [SerializeField] private TextAsset _nodes;
        [SerializeField] private TextAsset _nodeCost;
        [SerializeField] private TextAsset _nodeEffects;

        // 부를 때마다 CSV를 새로 읽는다. 연결하지 않은 CSV는 "시트가 없다" 진단이 된다.
        public NodeContentLoadResult Load() =>
            NodeContentCsv.Load(TextOf(_upgradeStats), TextOf(_nodes), TextOf(_nodeCost), TextOf(_nodeEffects));

        // CSV를 읽기만 한다(규칙 검사 전). 형식 오류는 into에 더한다. 밸런스 프로필이 이 데이터에 패치를 적용한 뒤 로더에 넘긴다.
        internal NodeContentData Read(List<ContentDiagnostic> into) =>
            NodeContentCsv.Read(TextOf(_upgradeStats), TextOf(_nodes), TextOf(_nodeCost), TextOf(_nodeEffects), into);

        private static string TextOf(TextAsset csv) => csv != null ? csv.text : null;

        // 원본 CSV. 서버 콘텐츠 묶음의 버전 계산(ServerContent)이 실행 중에도 읽고,
        // 에디터에서는 테스트 도구의 승격·되돌리기(M4)가 행을 고치고 시트 연동(M5)이 덮어쓴다.
        internal TextAsset UpgradeStatsCsv => _upgradeStats;
        internal TextAsset NodesCsv => _nodes;
        internal TextAsset NodeCostCsv => _nodeCost;
        internal TextAsset NodeEffectsCsv => _nodeEffects;

        // 에셋 인스펙터의 ⋮ 메뉴 > 검사: 불러와서 개수나 진단을 콘솔에 낸다.
        [ContextMenu("검사")]
        private void Check()
        {
            NodeContentLoadResult result = Load();

            foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                Debug.LogError("[노드 콘텐츠] " + diagnostic, this);

            if (!result.Succeeded)
            {
                Debug.LogError($"[노드 콘텐츠] 불러오지 못했다. 진단 {result.Diagnostics.Count}개.", this);
                return;
            }

            NodeContent content = result.Content;
            int ranks = 0;
            int effects = 0;
            int repeating = 0;

            foreach (NodeDefinition node in content.Nodes)
            {
                ranks += node.MaxRank;

                if (node.MaxRank > 1)
                    repeating++;

                foreach (NodeRankDefinition rank in node.Ranks)
                    effects += rank.Effects.Count;
            }

            Debug.Log($"[노드 콘텐츠] 수치 {content.Stats.Count} · 노드 {content.Nodes.Count} · Rank {ranks} · 효과 {effects}" +
                $" · 여러 Rank 노드 {repeating}", this);
        }
    }
}
