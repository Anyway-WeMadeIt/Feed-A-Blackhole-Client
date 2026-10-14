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
                _presentations.NodeTree,
                afterPresented: page =>
                {
                    BindView(page, ApplyBindings);
                    page.Build(_nodes, _tree.Graph.Links, _regions);
                },
                afterClosed: page =>
                {
                    Unbind(page);

                    if (_ui.CurrentRoot is UpgradeScreen root)
                        root.ShowRegion(null);
                });
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
            AddBinding(page,
                p => p.RegionViewed += HandleNodeTreeRegionViewed,
                p => p.RegionViewed -= HandleNodeTreeRegionViewed);
        }

        // 트리의 보이는 창 가운데가 든 구역이 바뀌었다. 위쪽 글자(블랙홀 자리)에 구역 이름을 보인다.
        private void HandleNodeTreeRegionViewed(string key)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.ShowRegion(key);
        }

        private void HandleNodeTreeNodeClicked(string id)
        {
            // 비용은 사기 전에 읽는다. 산 뒤에는 다음 Rank의 비용이 된다.
            NodePurchase.TryGetNextCost(_progress, _tree, id, out long cost);
            PurchaseResult result = NodePurchase.TryPurchase(_progress, _tree, id);

            if (result == PurchaseResult.Purchased)
            {
                SoundManager.Instance?.PlayNodeUpgrade();
                int rank = _progress.RankOf(id);
                _progressStore.Commit(seq => _syncLogs.NodePurchase(seq, id, rank, cost));
            }

            RefreshUpgrade();
        }

        // 툴팁은 호스트(업그레이드 화면)가 띄운다. 제목은 노드 ID, 본문은 Rank·효과·비용이다.
        // [임시] 수치의 표시 이름이 정해지면 StatId 대신 쓴다.
        private void HandleNodeTreeNodeHovered(string id, RectTransform node)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                //root.ShowNodeTooltip(node, id, NodeTooltipBody(id));
                root.ShowNodeTooltip(node, NodeTooltipTitle(id), NodeTooltipBody(id));
        }

        private string NodeTooltipTitle(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return id;

            string statId = StatIdOf(node.RankAt(1).Effects[0].Stat);
            return statId != null && _localizer.TryGet($"stat.{statId}.name", out string name) ? name : id;
        }

        // 툴팁 본문: 산 Rank, 다음 Rank의 효과(지금 값 → 산 뒤 값), 다음 비용.
        // 마지막 Rank까지 샀으면 마지막 Rank의 효과와 지금 값.
        private string NodeTooltipBody(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return string.Empty;

            int rank = _progress.RankOf(id);
            bool maxed = rank >= node.MaxRank;
            NodeRankDefinition shown = node.RankAt(maxed ? node.MaxRank : rank + 1);
            UpgradeStatValues values = NodePurchase.StatsFor(_progress, _tree);
            //추가코드, 효과줄 이름이 보이게 설정, 만약에 원하지 않으면 주석처리된 코드를 활성화하고 밑에 코드를 주석처리
            //bool named = shown.Effects.Count > 1;
            bool named = true;
            var text = new StringBuilder();

            //추가코드
            string titleStat = StatIdOf(node.RankAt(1).Effects[0].Stat);
            if (titleStat != null && _localizer.TryGet($"stat.{titleStat}.desc", out string description))
                text.Append(description).Append('\n');
            
            if (node.MaxRank > 1)
                text.Append(_localizer.Format("node.rank", rank, node.MaxRank)).Append('\n');

            foreach (NodeEffect effect in shown.Effects)
            {
                UpgradeStatDefinition stat = values.DefinitionOf(effect.Stat);
                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                float now = values.ValueOf(effect.Stat);

                //추가코드
                if (named)
                    text.Append(StatName(stat.StatId)).Append(' ');
                
                text.Append(NumberText.Signed(effect.Value)).Append(unit);
                //text.Append(stat.StatId).Append(' ').Append(NumberText.Signed(effect.Value)).Append(unit);

                if (maxed)
                {
                    text.Append("  (").Append(_localizer.Format("node.now", NumberText.Value(now) + unit)).Append(')');
                }
                else
                {
                    float after = Math.Min(stat.Max, Math.Max(stat.Min, now + effect.Value));
                    text.Append("  (").Append(NumberText.Value(now)).Append(unit).Append(" -> ").Append(NumberText.Value(after)).Append(unit).Append(')');
                }

                text.Append('\n');
            }

            text.Append(maxed ? _localizer.Get("node.maxed") : _localizer.Format("node.cost", NumberText.Compact(shown.Cost)));
            return text.ToString();
        }

        private void HandleNodeTreeNodeLeft(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.HideNodeTooltip();
        }

        // 수치 이름: 번역표 stat.{StatId}.name. 표에 없으면 StatId 그대로.
        private string StatName(string statId) =>
            _localizer.TryGet($"stat.{statId}.name", out string name) ? name : statId;

        // UpgradeStat → 시트의 StatId("timer"). 정의가 없으면 null.
        private string StatIdOf(UpgradeStat stat) =>
            NodePurchase.StatsFor(_progress, _tree).DefinitionOf(stat)?.StatId;
    }
}
