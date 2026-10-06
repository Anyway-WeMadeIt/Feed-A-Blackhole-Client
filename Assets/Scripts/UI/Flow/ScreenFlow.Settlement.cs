using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 물질 행의 종류 ID. 특수 성질(황금·전기·달·슈퍼노바 …)은 종류가 아니므로 그 종류로 센다. 혜성(픽업)은 물질 행에 넣지 않는다.
        private const string AsteroidKindId = "asteroid";
        private const string PlanetKindId = "planet";
        private const string StarKindId = "star";

        // 끝난 판의 원자료와 결산을 마친 진행 상태를 보여 준다(성장도는 이미 올라 있다).
        private void ShowSettlement(BattleRawData raw)
        {
            _ui.SwitchRoot<SettlementScreen>(
                _settlementPresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    root.ShowResult(raw.ReachedMilestone);
                    root.ShowStage(raw.Stage, raw.NextStage, _growth.MaxStage);
                    root.ShowMatter(
                        KillsOfFamily(raw.Kills, AsteroidKindId),
                        KillsOfFamily(raw.Kills, PlanetKindId),
                        KillsOfFamily(raw.Kills, StarKindId));
                    root.ShowGold(raw.EarnedGold, raw.SettledGold, raw.ReachedMilestone, _player.Gold);
                    root.ShowUpgradeCount(PurchasableNodeCount());
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(SettlementScreen root)
        {
            AddBinding(root,
                r => r.UpgradeClicked += HandleSettlementUpgradeClicked,
                r => r.UpgradeClicked -= HandleSettlementUpgradeClicked);

            AddBinding(root,
                r => r.ContinueClicked += HandleSettlementContinueClicked,
                r => r.ContinueClicked -= HandleSettlementContinueClicked);
        }

        // 업그레이드: 업그레이드 화면으로. 계속: 업그레이드 화면을 거치지 않고 지금 산 노드로 다음 판을 시작한다.
        private void HandleSettlementUpgradeClicked()
        {
            SoundManager.Instance.PlaySwitchingScreens();
            GoToUpgrade();
        }
        private void HandleSettlementContinueClicked() => RequestStart();

        private static int KillsOfFamily(IReadOnlyList<EnemyKillCount> kills, string family)
        {
            int count = 0;

            foreach (EnemyKillCount kill in kills)
            {
                if (string.Equals(kill.Enemy.Id, family, StringComparison.Ordinal))
                    count += kill.Count;
            }

            return count;
        }

        // 지금 Gold로 살 수 있는 노드 수. 업그레이드 버튼에 보인다.
        private int PurchasableNodeCount()
        {
            int count = 0;

            foreach (NodeDefinition node in _tree.Nodes)
            {
                if (NodePurchase.StateOf(_player, _tree, node.Id) == NodeState.Purchasable)
                    count++;
            }

            return count;
        }
    }
}
