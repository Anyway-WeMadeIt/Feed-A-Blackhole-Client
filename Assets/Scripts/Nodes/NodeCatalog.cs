using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임의 노드 트리 = 노드 콘텐츠 + 배치.
    // - 노드 콘텐츠(NodeContentSource): 어떤 노드가 있는가(ID·Rank·비용·효과)와 수치 정의. 데이터 시트(CSV)가 원본이다.
    // - 배치: 노드마다 격자 칸, 시작 노드인가, 이어진 노드. 노드 도구(메뉴 BlackHole > Node Tree)가 원본이다.
    // 둘은 노드 ID로 짝짓는다(NodeTreeLoader). 배치하지 않은 콘텐츠 노드는 트리에 들어가지 않는다.
    [CreateAssetMenu(fileName = "NodeCatalog", menuName = "BlackHole/Node Catalog")]
    public sealed class NodeCatalog : ScriptableObject
    {
        [Tooltip("노드 콘텐츠(UpgradeStats·Nodes·NodeCost·NodeEffects CSV).")]
        [SerializeField] private NodeContentSource _content;

        [SerializeField] private NodeTreeData _layout = new();

        public NodeContentSource Content => _content;

        // 노드 도구가 고치는 배치 원본. 게임 코드는 ToData()로 읽는다.
        internal NodeTreeData Tree => _layout;

        public NodeTreeData ToData() =>
            new NodeTreeData
            {
                Nodes = new List<NodeData>(_layout.Nodes),
                Regions = new List<NodeRegionData>(_layout.Regions)
            };
    }
}
