using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 노드 구매 정책: 지금 살 수 있는(드러났고 Gold가 되는) 노드 중 다음 Rank가 가장 싼 것부터, 살 수 없을 때까지 산다.
    // 값이 같으면 노드 ID 순서(ordinal)다 — 테스트 세팅의 자동 구매(ScenarioRunner)와 같은 규칙이다.
    // 산 노드가 이웃을 드러내므로 한 번 살 때마다 다시 고른다.
    // ScenarioRunner는 산 개수만 알려 주지만, 구매 로그에는 노드·Rank·비용이 필요해서 기록을 돌려주는 이 정책을 따로 둔다.
    internal static class CheapestFirstBuyer
    {
        public static List<PurchaseRecord> BuyAll(ProgressState progress, NodeTree tree)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            var bought = new List<PurchaseRecord>();

            while (true)
            {
                NodeDefinition cheapest = null;
                long cheapestCost = long.MaxValue;

                foreach (NodeDefinition node in tree.Nodes)
                {
                    if (NodePurchase.Check(progress, tree, node.Id) != PurchaseResult.Purchased)
                        continue;

                    if (!NodePurchase.TryGetNextCost(progress, tree, node.Id, out long cost))
                        continue;

                    if (cheapest == null || cost < cheapestCost
                        || (cost == cheapestCost && string.CompareOrdinal(node.Id, cheapest.Id) < 0))
                    {
                        cheapest = node;
                        cheapestCost = cost;
                    }
                }

                if (cheapest == null)
                    return bought;

                int rank = progress.RankOf(cheapest.Id) + 1;

                if (NodePurchase.TryPurchase(progress, tree, cheapest.Id) != PurchaseResult.Purchased)
                    throw new InvalidOperationException($"살 수 있다고 본 노드를 사지 못했다: '{cheapest.Id}'.");

                bought.Add(new PurchaseRecord(cheapest.Id, rank, cheapestCost, progress.Gold));
            }
        }
    }

    // 노드 Rank 하나를 산 기록. 구매 로그(NODE_PURCHASE)의 nodeId, rank(구매 후), cost가 된다.
    internal readonly struct PurchaseRecord
    {
        public string NodeId { get; }
        public int Rank { get; }
        public long Cost { get; }
        public long GoldAfter { get; }

        public PurchaseRecord(string nodeId, int rank, long cost, long goldAfter)
        {
            NodeId = nodeId;
            Rank = rank;
            Cost = cost;
            GoldAfter = goldAfter;
        }
    }
}
