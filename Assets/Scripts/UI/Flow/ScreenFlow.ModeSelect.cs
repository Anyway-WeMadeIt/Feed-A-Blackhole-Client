using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private const string NormalModeId = "normal";
        private static readonly Color NormalModeColor = new(0.62f, 0.33f, 0.35f);

        // 모드 카드. 계속 버튼은 이어 할 진행이 있을 때만 켠다(진행 저장). 이름·설명은 창을 열 때의 언어로 만든다
        // (모드 선택 창이 열린 채로는 언어를 바꿀 수 없다).
        private ModeSelectPanel.ModeItem[] Modes() => new[]
        {
            new ModeSelectPanel.ModeItem(
                NormalModeId,
                _localizer.Get("mode.normal.title"),
                _localizer.Get("mode.normal.description"),
                NormalModeColor,
                canContinue: _progressStore.CanContinue),
        };

        // 지금 루트(타이틀) 위에 모드 선택 창을 쌓는다. 이미 쌓여 있으면 그 창까지 되돌아간다.
        private void OpenModeSelect()
        {
            _ui.PushPanel<ModeSelectPanel>(
                _presentations.ModeSelect,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(Modes(), NormalModeId);
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(ModeSelectPanel panel)
        {
            AddBinding(panel,
                p => p.NewGameClicked += HandleModeSelectNewGameClicked,
                p => p.NewGameClicked -= HandleModeSelectNewGameClicked);

            AddBinding(panel,
                p => p.ContinueClicked += HandleModeSelectContinueClicked,
                p => p.ContinueClicked -= HandleModeSelectContinueClicked);

            AddBinding(panel,
                p => p.BackClicked += HandleModeSelectBackClicked,
                p => p.BackClicked -= HandleModeSelectBackClicked);
        }

        // 새 게임: 이어 할 진행이 있으면 지운다는 것을 확인받는다.
        private void HandleModeSelectNewGameClicked(string modeId)
        {
            if (_progressStore.CanContinue)
                OpenConfirm(
                    _localizer.Get("confirm.newGame.title"),
                    _localizer.Get("confirm.newGame.body"),
                    _localizer.Get("confirm.newGame.confirm"),
                    _localizer.Get("confirm.cancel"),
                    StartNewGame);
            else
                StartNewGame();
        }

        // 계속: 저장으로 진행 상태를 되살리고 곧바로 판을 시작한다(이번 실행에서 이미 진행 중이면 그대로).
        // 업그레이드 화면은 첫 판의 결산 뒤에 처음 연다.
        private void HandleModeSelectContinueClicked(string modeId)
        {
            _progressStore.Continue(_progress);
            _transition.Play(() => LeavePanelsAnd(StartBattle));
        }

        // 진행 상태를 처음으로 되돌리고 새 게임 로그와 함께 바로 저장한 뒤 튜토리얼 판으로 들어간다.
        private void StartNewGame()
        {
            _progressStore.StartNew(_progress, seq => _syncLogs.Reset(seq));
            _transition.Play(() => LeavePanelsAnd(StartTutorial));
        }

        private void HandleModeSelectBackClicked() => _ui.PopPanel(Unbind);

        // 화면이 다 덮인 뒤 패널을 모두 닫고(바인딩 해제) 판을 시작한다. 루트를 바꿔도 패널은 남기 때문이다.
        private void LeavePanelsAnd(System.Action start)
        {
            _ui.PopAllPanels(Unbind);
            start();
        }
    }
}
