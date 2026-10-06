using System;
using System.Text;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // UpgradeScreen을 Host로 사용하여, 트리 보기 페이지를 염.
        // UpgradeScreen Panel이 닫히면 NodeTreeViewPage 닫히고 바인딩 해제됨.
        private void SwitchToNodeTreePage(UpgradeScreen host)
        {
            _ui.SwitchPage<NodeTreeView>(
                host,
                _nodeTreePresentation,
                afterPresented: page =>
                {
                    BindView(page, ApplyBindings);
                    page.Build(_nodes, _tree.Graph.Links);
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(NodeTreeView page)
        {
            AddBinding(page,
                p => p.NodeClicked += HandleNodeTreeNodeClicked,
                p => p.NodeClicked -= HandleNodeTreeNodeClicked);

            AddBinding(page,
                p => p.NodeHovered += HandleNodeTreeNodeHovered,
                p => p.NodeHovered -= HandleNodeTreeNodeHovered);

            AddBinding(page,
                p => p.NodeLeft += HandleNodeTreeNodeLeft,
                p => p.NodeLeft -= HandleNodeTreeNodeLeft);
        }

        private void HandleNodeTreeNodeClicked(string id)
        {
            PurchaseResult result = NodePurchase.TryPurchase(_player, _tree, id);

            if (result == PurchaseResult.Purchased) SoundManager.Instance?.PlayNodeUpgrade();

            RefreshUpgrade();
        }

        // 툴팁은 호스트(업그레이드 화면)가 띄운다. 제목은 노드 ID, 본문은 Rank·효과·비용이다.
        // [임시] 수치의 표시 이름이 정해지면 StatId 대신 쓴다.
        private void HandleNodeTreeNodeHovered(string id, RectTransform node)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.ShowNodeTooltip(node, id, NodeTooltipBody(id));
        }

        // 툴팁 본문: 산 Rank, 다음 Rank의 효과(지금 값 → 산 뒤 값), 다음 비용.
        // 마지막 Rank까지 샀으면 마지막 Rank의 효과와 지금 값. 아직 전투에 이어지지 않은 수치는 표시한다(NodeUpgradeBridge).
        private string NodeTooltipBody(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return string.Empty;

            int rank = _player.RankOf(id);
            bool maxed = rank >= node.MaxRank;
            NodeRankDefinition shown = node.RankAt(maxed ? node.MaxRank : rank + 1);
            UpgradeStatValues values = NodePurchase.StatsFor(_player, _tree);
            var text = new StringBuilder();

            if (node.MaxRank > 1)
                text.Append("Rank ").Append(rank).Append('/').Append(node.MaxRank).Append('\n');

            foreach (NodeEffect effect in shown.Effects)
            {
                UpgradeStatDefinition stat = values.DefinitionOf(effect.StatId);
                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                float now = values.ValueOf(effect.StatId);
                text.Append(effect.StatId).Append(' ').Append(NumberText.Signed(effect.Value)).Append(unit);

                if (maxed)
                {
                    text.Append("  (now ").Append(NumberText.Value(now)).Append(unit).Append(')');
                }
                else
                {
                    float after = Math.Min(stat.Max, Math.Max(stat.Min, now + effect.Value));
                    text.Append("  (").Append(NumberText.Value(now)).Append(unit).Append(" -> ").Append(NumberText.Value(after)).Append(unit).Append(')');
                }

                if (!NodeUpgradeBridge.IsRouted(effect.StatId))
                    text.Append("  [not in battle yet]");

                text.Append('\n');
            }

            text.Append(maxed ? "Maxed" : "Cost " + NumberText.Compact(shown.Cost));
            return text.ToString();
        }

        private void HandleNodeTreeNodeLeft(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.HideNodeTooltip();
        }
    }
}
