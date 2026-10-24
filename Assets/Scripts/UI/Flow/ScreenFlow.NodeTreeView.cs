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
        // 터치로 고른 노드(툴팁을 고정한 노드)는 트리(NodeTreeView.SelectedId)가 가진다. 여기서는 따로 들고 있지 않는다.
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
                    {
                        root.ShowRegion(null);
                        root.HideNodeTooltip();
                        root.ShowPendingCost(null);
                    }
                });
        }

        private void ApplyBindings(NodeTreeView page)
        {
            AddBinding(page,
                p => p.NodeClicked += HandleNodeTreeNodeClicked,
                p => p.NodeClicked -= HandleNodeTreeNodeClicked);

            AddBinding(page,
                p => p.NodeTapped += HandleNodeTreeNodeTapped,
                p => p.NodeTapped -= HandleNodeTreeNodeTapped);

            AddBinding(page,
                p => p.EmptyTapped += HandleNodeTreeEmptyTapped,
                p => p.EmptyTapped -= HandleNodeTreeEmptyTapped);

            AddBinding(page,
                p => p.ViewMoved += ClearNodeSelection,
                p => p.ViewMoved -= ClearNodeSelection);

            AddBinding(page,
                p => p.NodeHeld += HandleNodeTreeNodeHeld,
                p => p.NodeHeld -= HandleNodeTreeNodeHeld);

            AddBinding(page,
                p => p.NodeHoldArmed += HandleNodeTreeNodeHoldArmed,
                p => p.NodeHoldArmed -= HandleNodeTreeNodeHoldArmed);

            AddBinding(page,
                p => p.NodeHoldShort += HandleNodeTreeNodeHoldShort,
                p => p.NodeHoldShort -= HandleNodeTreeNodeHoldShort);

            AddBinding(page,
                p => p.NodeHoldReleased += HandleNodeTreeNodeHoldReleased,
                p => p.NodeHoldReleased -= HandleNodeTreeNodeHoldReleased);

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

        // 마우스·펜: 노드를 누르면 바로 산다(올림 툴팁으로 읽는다).
        private void HandleNodeTreeNodeClicked(string id) => Purchase(id);

        // 구매의 유일한 진입점. 마우스 클릭·고정 패널의 구매 버튼·노드 꾹 누름이 같은 길로 온다. 샀으면 true.
        private bool Purchase(string id)
        {
            // 비용은 사기 전에 읽는다. 산 뒤에는 다음 Rank의 비용이 된다.
            NodePurchase.TryGetNextCost(_progress, _tree, id, out long cost);
            PurchaseResult result = NodePurchase.TryPurchase(_progress, _tree, id);

            if (result == PurchaseResult.Purchased)
            {
                _sounds.PlayNodeUpgrade();
                _recorder.Purchase(id, _progress.RankOf(id), cost);
            }

            RefreshUpgrade();
            return result == PurchaseResult.Purchased;
        }

        // 터치: 노드를 누르면 트리가 고르고(SelectedId) 알린다. 툴팁을 고정한다(바로 사지 않는다). 잠긴 노드도 설명은 보인다.
        private void HandleNodeTreeNodeTapped(string id, RectTransform node) => PinSelectedTooltip();

        // 빈 곳을 누르면 선택을 풀고 툴팁을 닫는다(고정 중일 때만 뜻이 있다).
        private void HandleNodeTreeEmptyTapped()
        {
            if (_ui.GetUI<NodeTreeView>()?.SelectedId != null)
                ClearNodeSelection();
        }

        // 고정 툴팁의 구매 버튼을 눌렀다. 고른 노드를 산다.
        private void HandleUpgradeBuyConfirmed()
        {
            string id = _ui.GetUI<NodeTreeView>()?.SelectedId;

            if (id != null)
                PurchaseSelected(id);
        }

        // 터치로 노드를 꾹 눌렀다 뗐다. 그 노드를 고른 채로 산다. 이어서 오는 탭(NodeTapped)이 패널을 새 내용으로 고정한다.
        private void HandleNodeTreeNodeHeld(string id)
        {
            _ui.GetUI<NodeTreeView>()?.SetSelected(id);
            PurchaseSelected(id);
        }

        // 고른 노드를 사고, 같은 노드에 툴팁을 다시 고정한다(다음 Rank 비용·최대로 갱신). 샀으면 패널의 구매 버튼을 누른 연출을 건다.
        private void PurchaseSelected(string id)
        {
            bool purchased = Purchase(id);
            PinSelectedTooltip();

            if (purchased && _ui.CurrentRoot is UpgradeScreen root)
                root.PlayBuyFeedback();
        }

        // 노드를 꾹 눌러 살 수 있게 된 순간: 패널의 구매 버튼도 눌린 모양으로. 떼거나 끌면 푼다(산 경우 NodeHeld가 이어서 확정 연출을 건다).
        private void HandleNodeTreeNodeHoldArmed(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.SetBuyPressed(true);
        }

        // Gold가 모자란 노드를 사려고 꾹 눌렀다(시간이 참): 골드와 구매 탭을 붉게. 떼면(NodeHoldReleased) 되돌린다.
        private void HandleNodeTreeNodeHoldShort(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.SetGoldShort(true);
        }

        private void HandleNodeTreeNodeHoldReleased(string id)
        {
            if (!(_ui.CurrentRoot is UpgradeScreen root))
                return;

            root.SetBuyPressed(false);
            root.SetGoldShort(false);
        }

        // 트리가 고른 노드에 툴팁을 고정한다. 고른 노드가 없거나 칸이 없으면(숨거나 먼 노드가 됨) 툴팁을 닫는다.
        private void PinSelectedTooltip()
        {
            if (!(_ui.CurrentRoot is UpgradeScreen root))
                return;

            NodeTreeView page = _ui.GetUI<NodeTreeView>();
            string id = page?.SelectedId;
            RectTransform rect = page?.RectOf(id);

            if (rect == null)
            {
                ClearNodeSelection();
                return;
            }

            NodeState state = NodePurchase.StateOf(_progress, _tree, id);
            root.PinNodeTooltip(rect, page.IconOf(id), NodeTooltipTitle(id), NodeTooltipBody(id, withCost: false), state);

            // 비용은 골드 칩 옆에 "-70"으로. 다음 Rank가 없거나(최대) 아직 닿지 않은 노드면 없다.
            long? pendingCost = null;

            if (state != NodeState.Hidden && NodePurchase.TryGetNextCost(_progress, _tree, id, out long cost))
                pendingCost = cost;

            root.ShowPendingCost(pendingCost);
        }

        // 선택을 풀고 툴팁을 닫는다. 지도를 옮기기 시작했을 때(ViewMoved)도 온다 — 트리는 선택을 이미 풀었다.
        private void ClearNodeSelection()
        {
            _ui.GetUI<NodeTreeView>()?.ClearSelection();

            if (_ui.CurrentRoot is UpgradeScreen root)
            {
                root.HideNodeTooltip();
                root.ShowPendingCost(null);
            }
        }

        // 툴팁은 호스트(업그레이드 화면)가 띄운다. 제목은 노드 ID, 본문은 Rank·효과·비용이다.
        // 열린 구역의 자물쇠 노드(Hidden)는 제목(이름)만 보인다. 먼 구역의 점은 트리 보기가 올림을 알리지 않는다.
        // [임시] 수치의 표시 이름이 정해지면 StatId 대신 쓴다.
        private void HandleNodeTreeNodeHovered(string id, RectTransform node)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.ShowNodeTooltip(node, NodeTooltipTitle(id), NodeTooltipBody(id, withCost: true));
        }

        private string NodeTooltipTitle(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return id;

            string statId = StatIdOf(node.RankAt(1).Effects[0].Stat);
            return statId != null && _localizer.TryGet($"stat.{statId}.name", out string name) ? name : id;
        }

        // 툴팁 본문: 산 Rank, 다음 Rank의 효과(지금 값 → 산 뒤 값), 다음 비용, Gold가 모자라면 모자란 만큼.
        // 마지막 Rank까지 샀으면 마지막 Rank의 효과와 지금 값. 아직 닿지 않은 노드(자물쇠)는 비운다(이름만).
        // 효과 줄에서 산 뒤 수치의 색(연한 녹색). 팔레트에 이 역할이 없어 상수로 둔다.
        private const string AfterValueColor = "#8FC8A0";

        // 툴팁 본문: 설명·Rank·효과. 올림 툴팁은 비용까지(withCost), 고정 패널은 비용을 따로 받는다(NodeTooltipCost).
        private string NodeTooltipBody(string id, bool withCost)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return string.Empty;

            // 아직 닿지 않은 노드(자물쇠): 수치·비용 없이 무엇을 주는지 한 줄만(새 요소를 미리 알 수 있게). 설명이 없으면 이름만.
            if (NodePurchase.StateOf(_progress, _tree, id) == NodeState.Hidden)
            {
                string hiddenStat = StatIdOf(node.RankAt(1).Effects[0].Stat);
                return hiddenStat != null && _localizer.TryGet($"stat.{hiddenStat}.desc", out string hiddenDescription)
                    ? hiddenDescription
                    : string.Empty;
            }

            int rank = _progress.RankOf(id);
            bool maxed = rank >= node.MaxRank;
            NodeRankDefinition shown = node.RankAt(maxed ? node.MaxRank : rank + 1);
            UpgradeStatValues values = NodePurchase.StatsFor(_progress, _tree);
            // 효과 이름은 효과가 둘 이상일 때만(하나면 제목과 같은 이름이 반복된다).
            bool named = shown.Effects.Count > 1;
            var text = new StringBuilder();

            string titleStat = StatIdOf(node.RankAt(1).Effects[0].Stat);
            if (titleStat != null && _localizer.TryGet($"stat.{titleStat}.desc", out string description))
                text.Append(description).Append('\n');

            if (node.MaxRank > 1)
                text.Append(_localizer.Format("node.rank", rank, node.MaxRank)).Append('\n');

            // 설명과 효과 사이 빈 줄 하나.
            text.Append('\n');

            // 효과마다 두 줄: "[이름] +50%" 그리고 "140% → 190%"(산 뒤 수치만 녹색). 수치 줄은 안에서 줄바꿈되지 않게 <nobr>.
            // 마지막 Rank까지 샀으면 증가량은 뜻이 없으니 "[이름] 현재 190%" 한 줄만.
            foreach (NodeEffect effect in shown.Effects)
            {
                UpgradeStatDefinition stat = values.DefinitionOf(effect.Stat);
                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                float now = values.ValueOf(effect.Stat);

                if (named)
                    text.Append(StatName(stat.StatId)).Append(' ');

                if (maxed)
                {
                    text.Append("<nobr>").Append(_localizer.Format("node.now", NumberText.Value(now) + unit)).Append("</nobr>");
                }
                else
                {
                    text.Append(NumberText.Signed(effect.Value)).Append(unit).Append('\n');
                    float after = Math.Min(stat.Max, Math.Max(stat.Min, now + effect.Value));
                    text.Append("<nobr>").Append(NumberText.Value(now)).Append(unit)
                        .Append("  →  <color=").Append(AfterValueColor).Append('>').Append(NumberText.Value(after)).Append(unit).Append("</color></nobr>");
                }

                text.Append('\n');
            }

            if (!withCost)
                return text.ToString().TrimEnd('\n');

            // 설명·효과와 비용 사이 빈 줄 하나(가독성).
            text.Append('\n').Append(NodeTooltipCost(id));
            return text.ToString();
        }

        // 비용 줄: "비용 70" / "최대", Gold가 모자라면 모자란 만큼을 한 줄 더. 아직 닿지 않은 노드는 빈 글자.
        private string NodeTooltipCost(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node) || NodePurchase.StateOf(_progress, _tree, id) == NodeState.Hidden)
                return string.Empty;

            int rank = _progress.RankOf(id);
            bool maxed = rank >= node.MaxRank;

            if (maxed)
                return _localizer.Get("node.maxed");

            long cost = node.RankAt(rank + 1).Cost;
            string text = _localizer.Format("node.cost", NumberText.Compact(cost));

            if (_progress.Gold < cost)
                text += "\n" + _localizer.Format("node.short", NumberText.Compact(cost - _progress.Gold));

            return text;
        }

        // 올림이 벗어났다. 고정 툴팁(터치)은 올림으로 닫지 않는다 — 터치 노트북에서 마우스가 지나가도 남는다.
        private void HandleNodeTreeNodeLeft(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root && !root.IsTooltipPinned)
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
