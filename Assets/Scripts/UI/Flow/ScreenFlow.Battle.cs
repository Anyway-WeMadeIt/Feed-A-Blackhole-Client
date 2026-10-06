using System;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private void ShowBattle()
        {
            _ui.SwitchRoot<BattleScreen>(
                _battlePresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    root.ShowIdle();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(BattleScreen root)
        {
            AddBinding(root,
                r => r.PauseClicked += HandleBattlePauseClicked,
                r => r.PauseClicked -= HandleBattlePauseClicked);

            AddBinding(root,
                r => r.EndClicked += HandleBattleEndClicked,
                r => r.EndClicked -= HandleBattleEndClicked);
        }

        private void HandleBattlePauseClicked() => OpenPause();
        private void HandleBattleEndClicked() => RequestEnd();

        internal void HandleBattleTimeExpired() => RequestEnd();

        // 화면 버튼과 시간 종료가 같은 전환 경로를 사용한다. 판은 화면이 다 덮인 뒤에 바꾼다.
        // 시작: 덮인 뒤 판을 조립·시작하고 전투 화면으로 바꾼다. 덮이는 동안 판이 흐르지 않는다.
        private void RequestStart()
        {
            SoundManager.Instance.PlaySwitchingScreens();
            _transition.Play(StartBattle);
        }

        // 끝: 덮인 뒤 판을 정리·결산하고 결산 화면으로 바꾼다. 적이 치워지는 모습이 보이지 않는다.
        private void RequestEnd() => _transition.Play(EndBattleAsync);
 
        private void StartBattle()
        {
            try
            {
                if (_battle.TryStart(NodePurchase.UpgradesFor(_player, _tree)))
                    ShowBattle();
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private async Task EndBattleAsync()
        {
            try
            {
                BattleRawData raw = await _battle.TryEndAsync();
                if (raw != null)
                    ShowSettlement(raw);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
