using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 타이틀로(앱 시작, 메인 메뉴).
        public void GoToTitle() => _transition.Play(ShowTitle);

        private void ShowTitle()
        {
            _ui.SwitchRoot<TitleScreen>(
                _presentations.Title,
                afterPresented: root => BindView(root, ApplyBindings),
                afterClosed: Unbind);
        }

        private void ApplyBindings(TitleScreen root)
        {
            AddBinding(root,
                r => r.StartClicked += HandleTitleStartClicked,
                r => r.StartClicked -= HandleTitleStartClicked);

            AddBinding(root,
                r => r.SettingsClicked += HandleTitleSettingsClicked,
                r => r.SettingsClicked -= HandleTitleSettingsClicked);

            AddBinding(root,
                r => r.QuitClicked += HandleTitleQuitClicked,
                r => r.QuitClicked -= HandleTitleQuitClicked);

            AddBinding(root,
                r => r.FullscreenClicked += HandleFullscreenClicked,
                r => r.FullscreenClicked -= HandleFullscreenClicked);
        }

        // 시작은 타이틀 위에 모드 선택 창을 연다. 모드를 고른 뒤 업그레이드 화면으로 간다.
        private void HandleTitleStartClicked()
        {
            OpenModeSelect();
        }

        private void HandleTitleSettingsClicked() => OpenSettings();

        private static void HandleTitleQuitClicked() => QuitGame();

        // 웹 빌드의 전체 화면 ↔ 창 모드. 버튼 클릭(사용자 입력) 안에서만 브라우저가 허용한다.
        private static void HandleFullscreenClicked() => WebFullscreen.Toggle();

        // 앱을 끝낸다. 에디터에서는 플레이를 멈춘다.
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
