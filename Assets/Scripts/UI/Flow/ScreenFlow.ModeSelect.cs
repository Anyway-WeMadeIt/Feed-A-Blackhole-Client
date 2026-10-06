using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private const string NormalModeId = "normal";

        private static readonly ModeSelectPanel.ModeItem[] Modes =
        {
            new ModeSelectPanel.ModeItem(
                NormalModeId,
                "Normal Mode",
                "The main mode. Break asteroids, planets and stars and feed their matter to the black hole.",
                new Color(0.62f, 0.33f, 0.35f)),
        };

        // 지금 루트(타이틀) 위에 모드 선택 창을 쌓는다. 이미 쌓여 있으면 그 창까지 되돌아간다.
        private void OpenModeSelect()
        {
            _ui.PushPanel<ModeSelectPanel>(
                _modeSelectPresentation,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(Modes, NormalModeId);
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

        private void HandleModeSelectNewGameClicked(string modeId) => EnterMode();
        private void HandleModeSelectContinueClicked(string modeId) => EnterMode();

        private void HandleModeSelectBackClicked() => _ui.PopPanel(Unbind);

        // 화면이 다 덮인 뒤 패널을 모두 닫고(바인딩 해제) 곧바로 판을 시작한다(전투 화면). 루트를 바꿔도 패널은 남기 때문이다.
        // 업그레이드 화면은 첫 판의 결산 뒤에 처음 연다.
        private void EnterMode() => _transition.Play(StartBattleFromModeSelect);

        private void StartBattleFromModeSelect()
        {
            _ui.PopAllPanels(Unbind);
            StartBattle();

            // 판을 시작하지 못했으면(판 조립 오류 등, 로그는 StartBattle이 남긴다) 타이틀에 갇히지 않게 업그레이드 화면으로 간다.
            if (!_battle.IsRunning)
                ShowUpgrade();
        }
    }
}
