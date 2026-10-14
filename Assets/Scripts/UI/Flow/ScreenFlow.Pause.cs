using System.Threading.Tasks;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 전투에서는 판을 멈추고 연다. 시작 연출 중에도 열 수 있다(연출도 함께 멈춘다).
        // 결산·업그레이드에서는 멈출 판이 없어 창만 연다.
        private void OpenPause()
        {
            if (_ui.CurrentRoot is BattleScreen)
            {
                if (!_battle.CanPause)
                    return;

                SetBattlePaused(true);
            }

            _ui.PushPanel<PausePanel>(
                _presentations.Pause,
                afterPresented: panel => BindView(panel, ApplyBindings),
                afterClosed: Unbind);

            _pauseOpen = true;
        }

        private void ApplyBindings(PausePanel panel)
        {
            AddBinding(panel,
                p => p.ResumeClicked += HandlePauseResumeClicked,
                p => p.ResumeClicked -= HandlePauseResumeClicked);

            AddBinding(panel,
                p => p.SettingsClicked += HandlePauseSettingsClicked,
                p => p.SettingsClicked -= HandlePauseSettingsClicked);

            AddBinding(panel,
                p => p.MainMenuClicked += HandlePauseMainMenuClicked,
                p => p.MainMenuClicked -= HandlePauseMainMenuClicked);

            AddBinding(panel,
                p => p.QuitClicked += HandlePauseQuitClicked,
                p => p.QuitClicked -= HandlePauseQuitClicked);
        }

        private void HandlePauseResumeClicked()
        {
            _ui.PopPanel(Unbind);
            SetBattlePaused(false);
            _pauseOpen = false;
        }

        // 설정: 일시 정지 창 위에 설정 창을 쌓는다. 설정 창을 닫으면 일시 정지 창으로 돌아온다.
        private void HandlePauseSettingsClicked() => OpenSettings();

        private void HandlePauseMainMenuClicked() => _transition.Play(LeaveToTitleAsync);

        // 전투: 판을 포기하고(결산 없음) 타이틀로.
        // 결산·업그레이드: 판이 없어 곧바로 타이틀로. 진행은 결산·노드 구매 때 이미 저장했다.
        private async Task LeaveToTitleAsync()
        {
            _ui.PopAllPanels(Unbind);
            _pauseOpen = false;
            _settingsOpen = false;

            if (_battle.Session == null)
            {
                ShowTitle();
                return;
            }

            if (await _battle.TryAbandonAsync())
            {
                // 포기는 결산 로그가 없다. 서버는 다음 판 시작에서 열린 판을 포기로 닫는다.
                _runId = null;
                ShowTitle();
            }
        }

        private static void HandlePauseQuitClicked() => QuitGame();

        // 판을 멈추거나 다시 움직인다. 판이 없거나 이미 그 상태면 그대로 둔다.
        private void SetBattlePaused(bool paused) => _battle.SetPaused(paused);
    }
}
