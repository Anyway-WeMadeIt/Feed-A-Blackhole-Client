using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 끝난 판의 원자료와 결산을 마친 진행 상태를 보여 준다(성장도는 이미 올라 있다).
        // tutorial: 튜토리얼 판의 결과. 진행 상태에 결산하지 않았으므로 단계는 "튜토리얼 → 첫 단계"로 보이고 막대는 가득 찬다.
        private void ShowSettlement(BattleRawData raw, bool tutorial = false)
        {
            string stageName = tutorial ? _localizer.Get("milestone.tutorial.name") : MilestoneName(raw.Stage);
            string nextName = tutorial
                ? MilestoneName(HqGrowthDefinition.StartStage)
                : raw.Stage < _growth.MaxStage ? MilestoneName(raw.Stage + 1) : null;
            float fraction = tutorial ? 1f : LevelFraction(raw);

            _ui.SwitchRoot<SettlementScreen>(
                _presentations.Settlement,
                afterPresented: root =>
                {
                    // 특수 성질(황금·전기·달·슈퍼노바 …)은 종류가 아니므로 그 종류로 센다. 혜성(픽업)은 물질 행에 넣지 않는다.
                    int asteroids = KillsOf(raw.Kills, EnemyType.Asteroid);
                    int planets = KillsOf(raw.Kills, EnemyType.Planet);
                    int stars = KillsOf(raw.Kills, EnemyType.Star);

                    BindView(root, ApplyBindings);
                    root.ShowResult(raw.ReachedMilestone);
                    root.ShowStage(stageName, nextName, fraction, raw.ReachedMilestone);
                    root.ShowMatter(asteroids, planets, stars);
                    root.ShowGold(raw.EarnedGold, raw.SettledGold, raw.ReachedMilestone, _progress.Gold);
                    root.ShowStats(raw.Stats, asteroids, planets, stars);
                    root.ShowUpgradeCount(PurchasableNodeCount());
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
            _localizer.TryGet($"milestone.{stage}.name", out string name) ? name : stage.ToString();

        // 이번 판이 시작 체크포인트에서 목표 이정표까지 간 몫(0 ~ 1): 오른 Level 수 + 다음 Level까지의 몫, 목표까지의 Level 수로 나눈다.
        // 목표가 없으면(마지막 이정표 뒤) 1.
        private float LevelFraction(BattleRawData raw)
        {
            int start = _growth.StartLevelAt(raw.Stage);
            int goal = _growth.GoalLevelAt(raw.Stage);

            if (goal <= start)
                return 1;

            float gained = raw.ReachedLevel - start + _growth.ProgressAt(raw.ReachedLevel, raw.Exp);
            return Mathf.Clamp01(gained / (goal - start));
        }

        private static int KillsOf(IReadOnlyList<EnemyKillCount> kills, EnemyType type)
        {
            int count = 0;

            foreach (EnemyKillCount kill in kills)
            {
                if (kill.Enemy.Type == type)
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
                if (NodePurchase.StateOf(_progress, _tree, node.Id) == NodeState.Purchasable)
                    count++;
            }

            return count;
        }
    }
}
