using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class NodeContent
    {
        private readonly Dictionary<UpgradeStat, UpgradeStatDefinition> _stats = new();
        private readonly Dictionary<string, NodeDefinition> _nodesById = new(StringComparer.Ordinal);

        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }
        public IReadOnlyList<NodeDefinition> Nodes { get; }

        internal NodeContent(List<UpgradeStatDefinition> stats, List<NodeDefinition> nodes)
        {
            Stats = stats.AsReadOnly();
            Nodes = nodes.AsReadOnly();

            foreach (UpgradeStatDefinition stat in stats)
                _stats.Add(stat.Stat, stat);

            foreach (NodeDefinition node in nodes)
                _nodesById.Add(node.Id, node);
        }

        // 모든 수치(UpgradeStat)의 정의가 있다(NodeContentLoader).
        public UpgradeStatDefinition StatOf(UpgradeStat stat) => _stats[stat];

        public bool TryGetNode(string nodeId, out NodeDefinition node)
        {
            if (nodeId is null)
            {
                node = null;
                return false;
            }

            return _nodesById.TryGetValue(nodeId, out node);
        }
    }
}
