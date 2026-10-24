using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 끝난 판의 원자료와 결산을 마친 진행 상태를 보여 준다(성장도는 이미 올라 있다).
        // tutorial: 튜토리얼 판의 결과. 진행 상태에 결산하지 않았으므로 단계는 "튜토리얼 → 첫 단계"로 보이고 막대는 가득 찬다.
        private void ShowSettlement(BattleRawData raw, bool tutorial = false)
        {
            string stageName = tutorial ? _localizer.Get(UIText.Milestone.TutorialName) : MilestoneName(raw.Stage);
            string nextName = tutorial
                ? MilestoneName(HqGrowthDefinition.StartStage)
                : raw.Stage < _growth.MaxStage ? MilestoneName(raw.Stage + 1) : null;
            float fraction = tutorial ? 1f : _growth.StageFractionAt(raw.Stage, raw.ReachedLevel, raw.Exp);

            _ui.SwitchRoot<SettlementScreen>(
                _presentations.Settlement,
                afterPresented: root =>
                {
                    // 혜성(픽업)은 물질 행에 넣지 않는다.
                    int asteroids = raw.KillsOf(EnemyType.Asteroid);
                    int planets = raw.KillsOf(EnemyType.Planet);
                    int stars = raw.KillsOf(EnemyType.Star);

                    BindView(root, ApplyBindings);
                    root.ShowResult(raw.ReachedMilestone);
                    root.ShowStage(stageName, nextName, fraction, raw.ReachedMilestone);
                    root.ShowMatter(asteroids, planets, stars);
                    root.ShowGold(raw.EarnedGold, raw.SettledGold, raw.ReachedMilestone, _progress.Gold);
                    root.ShowStats(raw.Stats, asteroids, planets, stars);
                    // 업그레이드 버튼에 지금 Gold로 살 수 있는 노드 수를 보인다.
                    root.ShowUpgradeCount(NodePurchase.CountOf(_progress, _tree, NodeState.Purchasable));
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(SettlementScreen root)
        {
            AddBinding(root,
                r => r.UpgradeClicked += HandleSettlementUpgradeClicked,
                r => r.UpgradeClicked -= HandleSettlementUpgradeClicked);

            // 계속: 업그레이드 화면을 거치지 않고 지금 산 노드로 다음 판을 시작한다.
            AddBinding(root,
                r => r.ContinueClicked += RequestStart,
                r => r.ContinueClicked -= RequestStart);
        }

        // 업그레이드: 업그레이드 화면으로.
        private void HandleSettlementUpgradeClicked()
        {
            _sounds.PlaySwitchingScreens();
            GoToUpgrade();
        }

        // 이정표 이름(번역표 milestone.<n>.name). 표에 없으면 번호.
        private string MilestoneName(int stage) =>
            _localizer.TryGet(UIText.Milestone.Name(stage), out string name) ? name : stage.ToString();
    }
}
