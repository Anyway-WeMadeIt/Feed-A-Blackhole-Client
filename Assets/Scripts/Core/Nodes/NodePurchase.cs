using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 구매:
    // 화면이 노드 ID로 요청. 한 번에 다음 Rank 하나를 산다(Rank 1부터 차례로).
    // 노드는 Rank 1을 사는 순간 "산 노드"가 되어 선으로 이어진 노드를 드러낸다(ProgressState.Owns).
    public static class NodePurchase
    {
        // 지금 사면 어떻게 되는지. 상태를 바꾸지 않음.
        public static PurchaseResult Check(ProgressState progress, NodeTree tree, string nodeId)
        {
            Verify(progress, tree);
            return tree.TryGet(nodeId, out NodeDefinition node) ? Check(progress, tree, node) : PurchaseResult.UnknownNode;
        }

        public static PurchaseResult TryPurchase(ProgressState progress, NodeTree tree, string nodeId)
        {
            Verify(progress, tree);

            if (!tree.TryGet(nodeId, out NodeDefinition node))
                return PurchaseResult.UnknownNode;

            PurchaseResult result = Check(progress, tree, node);

            if (result == PurchaseResult.Purchased)
                progress.BuyRank(node.Id, NextRankOf(progress, node).Cost);

            return result;
        }

        public static NodeState StateOf(ProgressState progress, NodeTree tree, string nodeId)
        {
            switch (Check(progress, tree, nodeId))
            {
                case PurchaseResult.UnknownNode: throw new ArgumentException($"트리에 없는 노드다: '{nodeId}'.", nameof(nodeId));
                case PurchaseResult.MaxRankReached: return NodeState.Owned;
                case PurchaseResult.Hidden: return NodeState.Hidden;
                case PurchaseResult.Purchased: return NodeState.Purchasable;
                default: return NodeState.Revealed;
            }
        }

        // 다음에 살 Rank의 비용. 마지막 Rank까지 샀거나 트리에 없는 노드면 false.
        public static bool TryGetNextCost(ProgressState progress, NodeTree tree, string nodeId, out long cost)
        {
            Verify(progress, tree);
            cost = 0;

            if (!tree.TryGet(nodeId, out NodeDefinition node))
                return false;

            NodeRankDefinition next = NextRankOf(progress, node);

            if (next == null)
                return false;

            cost = next.Cost;
            return true;
        }

        // 산 Rank의 효과로 계산한 수치 값(시트 단위). 노드 트리와 업그레이드 수치가 만나는 자리는 여기 하나.
        public static UpgradeStatValues StatsFor(ProgressState progress, NodeTree tree)
        {
            Verify(progress, tree);
            return StatsFor(tree, node => progress.RankOf(node.Id));
        }

        // 노드마다 ranksOf(node)까지 산 것으로 보고 수치 값을 계산한다. 로드 때의 검사(UpgradeContentCheck)는 모두 산 경우를 본다.
        internal static UpgradeStatValues StatsFor(NodeTree tree, Func<NodeDefinition, int> ranksOf)
        {
            var sums = new Dictionary<UpgradeStat, double>();

            foreach (NodeDefinition node in tree.Nodes)
            {
                int ranks = ranksOf(node);

                for (int rank = 1; rank <= ranks; rank++)
                {
                    foreach (NodeEffect effect in node.RankAt(rank).Effects)
                        sums[effect.Stat] = (sums.TryGetValue(effect.Stat, out double sum) ? sum : 0) + effect.Value;
                }
            }

            return new UpgradeStatValues(tree.Content.Stats, sums);
        }

        // 판정 순서: 마지막 Rank → 숨김 → Gold.
        private static PurchaseResult Check(ProgressState progress, NodeTree tree, NodeDefinition node)
        {
            NodeRankDefinition next = NextRankOf(progress, node);

            if (next == null)
                return PurchaseResult.MaxRankReached;

            if (!tree.Graph.IsRevealed(node.Id, progress.Owns))
                return PurchaseResult.Hidden;

            if (progress.Gold < next.Cost)
                return PurchaseResult.NotEnoughGold;

            return PurchaseResult.Purchased;
        }

        // 다음에 살 Rank. 마지막 Rank까지 샀으면 null.
        private static NodeRankDefinition NextRankOf(ProgressState progress, NodeDefinition node)
        {
            int owned = progress.RankOf(node.Id);
            return owned < node.MaxRank ? node.RankAt(owned + 1) : null;
        }

        private static void Verify(ProgressState progress, NodeTree tree)
        {
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));

            if (tree == null)
                throw new ArgumentNullException(nameof(tree));
        }
    }
}
