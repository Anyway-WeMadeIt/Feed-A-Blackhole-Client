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
            long? cost = NodePurchase.StatusOf(_progress, _tree, id).NextCost;
            PurchaseResult result = NodePurchase.TryPurchase(_progress, _tree, id);

            if (result == PurchaseResult.Purchased)
            {
                _sounds.PlayNodeUpgrade();
                _recorder.Purchase(id, _progress.RankOf(id), cost ?? 0);
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

            NodeStatus status = NodePurchase.StatusOf(_progress, _tree, id);
            root.PinNodeTooltip(rect, page.IconOf(id), _nodeTooltip.Title(id), _nodeTooltip.Body(id, _progress, withCost: false), status.State);

            // 비용은 골드 칩 옆에 "-70"으로. 다음 Rank가 없거나(최대) 아직 닿지 않은 노드면 없다.
            root.ShowPendingCost(status.State != NodeState.Hidden ? status.NextCost : null);
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

        // 올림 툴팁은 호스트(업그레이드 화면)가 띄운다. 글은 NodeTooltipText가 만든다. 먼 구역의 점은 트리 보기가 올림을 알리지 않는다.
        private void HandleNodeTreeNodeHovered(string id, RectTransform node)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.ShowNodeTooltip(node, _nodeTooltip.Title(id), _nodeTooltip.Body(id, _progress, withCost: true));
        }

        // 올림이 벗어났다. 고정 툴팁(터치)은 올림으로 닫지 않는다 — 터치 노트북에서 마우스가 지나가도 남는다.
        private void HandleNodeTreeNodeLeft(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root && !root.IsTooltipPinned)
                root.HideNodeTooltip();
        }
    }
}
