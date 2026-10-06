using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 업그레이드 화면으로(모드 선택, 결산).
        public void GoToUpgrade() => _transition.Play(ShowUpgrade);

        private void ShowUpgrade()
        {
            SoundManager.Instance?.ResetNodeUpgradeIndex(); // 업그레이드 SFX 인덱스 초기화
            _ui.SwitchRoot<UpgradeScreen>(
                _upgradePresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    SwitchToNodeTreePage(root);
                    RefreshUpgrade();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(UpgradeScreen root)
        {
            AddBinding(root,
                r => r.StartBattleClicked += HandleUpgradeStartBattleClicked,
                r => r.StartBattleClicked -= HandleUpgradeStartBattleClicked);
        }

        private void HandleUpgradeStartBattleClicked() => RequestStart();

        // 진행 상태를 화면 값(Gold, 블랙홀 성장도·다음 판의 목표 Level, 노드마다의 상태·다음 Rank 비용·산 Rank)으로 바꿔 넘긴다.
        private void RefreshUpgrade()
        {
            if (!(_ui.CurrentRoot is UpgradeScreen root))
                return;

            root.ShowGold(_player.Gold);

            int stage = _player.GrowthStage;
            root.ShowHq(stage, _growth.GoalLevelAt(stage));

            var states = new Dictionary<string, NodeState>(_tree.Nodes.Count, StringComparer.Ordinal);
            var costs = new Dictionary<string, long>(_tree.Nodes.Count, StringComparer.Ordinal);
            var ranks = new Dictionary<string, int>(_tree.Nodes.Count, StringComparer.Ordinal);
            foreach (NodeDefinition node in _tree.Nodes)
            {
                states.Add(node.Id, NodePurchase.StateOf(_player, _tree, node.Id));
                ranks.Add(node.Id, _player.RankOf(node.Id));

                if (NodePurchase.TryGetNextCost(_player, _tree, node.Id, out long cost))
                    costs.Add(node.Id, cost);
            }

            _ui.GetUI<NodeTreeView>().Show(states, costs, ranks);
        }
    }
}
