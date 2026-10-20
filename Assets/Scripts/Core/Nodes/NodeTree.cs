using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 불러온 노드 트리:
    // - 그래프(NodeGraph, 배치에서)와 노드 정의(NodeDefinition, 콘텐츠에서)를 노드 ID로 이어줌.
    // - 구매 규칙은 NodePurchase.
    // - 로더는 그래프의 노드와 Nodes의 노드가 같은 ID 묶음이라는 것을 보장. 배치되지 않은 콘텐츠 노드는 들어 있지 않다(살 수 없다).
    public sealed class NodeTree
    {
        private readonly Dictionary<string, NodeDefinition> _byId = new(StringComparer.Ordinal);

        public NodeGraph Graph { get; }
        // 배치된 노드(배치 순서).
        public IReadOnlyList<NodeDefinition> Nodes { get; }
        // 이 트리를 만든 콘텐츠: 수치 정의(Stats)와 배치되지 않은 노드까지 모든 노드.
        public NodeContent Content { get; }

        internal NodeTree(NodeGraph graph, List<NodeDefinition> nodes, NodeContent content)
        {
            Graph = graph;
            Nodes = nodes.AsReadOnly();
            Content = content;

            foreach (NodeDefinition node in nodes)
                _byId.Add(node.Id, node);
        }

        public bool TryGet(string id, out NodeDefinition node)
        {
            node = null;
            return id != null && _byId.TryGetValue(id, out node);
        }
    }
}
