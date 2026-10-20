namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private bool _pauseOpen; // Pause 패널이 열려있는가
        private bool _settingsOpen; // Setting 패널이 열려있는가?

        /// <summary>
        /// Shift: 결산 화면에서 Upgrade. 창이 열려 있으면 무시한다.
        /// </summary>
        private void HandleKeyActionShift()
        {
            if (IsEndingPlaying || _ui.HasPanel) return;

            if (_ui.CurrentRoot is SettlementScreen) GoToUpgrade();
        }

        /// <summary>
        /// 결산 화면, 노드 트리에서 Continue. 창(설정 등)이 열려 있으면 무시한다.
        /// 엔딩 중에는 누르고 있는 동안 문구·크레딧 빨리 감기(ScreenFlow.TickEnding), 감사 문구에서 누르면 타이틀로.
        /// </summary>
        private void HandleKeyActionSpace()
        {
            if (IsEndingPlaying)
            {
                ContinueEnding();
                return;
            }

            if (_ui.HasPanel) return;

            if (_ui.CurrentRoot is SettlementScreen
                || _ui.CurrentRoot is UpgradeScreen) RequestStart();
        }

        /// <summary>
        /// 게임·결산·업그레이드 화면에서 Pause 창 열기·닫기. 엔딩 중에는 엔딩 화면 건너뛰기.
        /// </summary>
        private void HandleKeyActionEsc()
        {
            // 엔딩 중에는 창을 열지 않고 엔딩 화면을 건너뛴다.
            if (IsEndingPlaying)
            {
                SkipEnding();
                return;
            }

            if (_ui.CurrentRoot is BattleScreen)
            {
                TogglePauseByEsc();
            }
            else if (_ui.CurrentRoot is SettlementScreen
                     || _ui.CurrentRoot is UpgradeScreen)
            {
                // 화면 전환 중에는 무시한다 — 덮이는 동안 열면 바뀐 화면(전투·업그레이드) 위에 창이 남는다.
                if (_transition.IsPlaying) return;

                TogglePauseByEsc();
            }
        }

        private void TogglePauseByEsc()
        {
            if (_settingsOpen) return; // 설정 창이 열려 있으면 ESC를 무시한다

            if (_pauseOpen) HandlePauseResumeClicked();
            else if (!_ui.HasPanel) OpenPause();
        }
    }
}
