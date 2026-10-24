using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 업그레이드 화면으로(모드 선택, 결산).
        public void GoToUpgrade() => _transition.Play(ShowUpgrade);

        private void ShowUpgrade()
        {
            _sounds.ResetNodeUpgradeIndex();
            _ui.SwitchRoot<UpgradeScreen>(
                _presentations.Upgrade,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    SwitchToNodeTreePage(root);
                    RefreshUpgrade();
                    ShowNoticesIfDue();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(UpgradeScreen root)
        {
            AddBinding(root,
                r => r.StartBattleClicked += RequestStart,
                r => r.StartBattleClicked -= RequestStart);

            AddBinding(root,
                r => r.BuyConfirmed += HandleUpgradeBuyConfirmed,
                r => r.BuyConfirmed -= HandleUpgradeBuyConfirmed);
        }

        // 진행 상태를 화면 값(Gold, 블랙홀 성장도·다음 판의 목표 Level, 노드마다의 상태·다음 Rank 비용·산 Rank)으로 바꿔 넘긴다.
        private void RefreshUpgrade()
        {
            if (!(_ui.CurrentRoot is UpgradeScreen root))
                return;

            root.ShowGold(_progress.Gold);

            int stage = _progress.GrowthStage;
            root.ShowHq(stage, _growth.GoalLevelAt(stage));

            _ui.GetUI<NodeTreeView>().Show(NodePurchase.StatusesOf(_progress, _tree));
        }
    }
}
