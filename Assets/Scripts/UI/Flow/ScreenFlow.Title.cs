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
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    ShowNoticesIfDue();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(TitleScreen root)
        {
            // 시작은 타이틀 위에 모드 선택 창을 연다. 모드를 고른 뒤 판으로 간다.
            AddBinding(root,
                r => r.StartClicked += OpenModeSelect,
                r => r.StartClicked -= OpenModeSelect);

            AddBinding(root,
                r => r.SettingsClicked += OpenSettings,
                r => r.SettingsClicked -= OpenSettings);

            AddBinding(root,
                r => r.QuitClicked += QuitGame,
                r => r.QuitClicked -= QuitGame);
        }

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
