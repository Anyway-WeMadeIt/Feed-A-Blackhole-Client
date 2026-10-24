using System;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class PausePanel : UIPanel<PausePanel.Refs>, IUISoundView
    {
        public enum Refs
        {
            ResumeBtn_Button,
            SettingsBtn_Button,
            MainMenuBtn_Button,
            QuitBtn_Button,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // 버튼의 올림·누름·막힘 그림은 프리팹의 Button(Sprite Swap)에 있다.
            Panel_Image,
            Title_Text,
            ResumeBtn_Image,
            ResumeBtn_Text,
            SettingsBtn_Image,
            SettingsBtn_Text,
            MainMenuBtn_Image,
            MainMenuBtn_Text,
            QuitBtn_Image,
            QuitBtn_Text,
        }

        private Button _resumeButton;
        private Button _settingsButton;
        private Button _mainMenuButton;
        private Button _quitButton;

        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action MainMenuClicked;
        public event Action QuitClicked;

        public void SetSounds(UISounds sounds) => ButtonAnimation.SetSounds(this, sounds);

        protected override void OnInitialize()
        {
            _resumeButton = View.Button(Refs.ResumeBtn_Button);
            _settingsButton = View.Button(Refs.SettingsBtn_Button);
            _mainMenuButton = View.Button(Refs.MainMenuBtn_Button);
            _quitButton = View.Button(Refs.QuitBtn_Button);

            ButtonFeedback.Bind(_resumeButton, () => ResumeClicked?.Invoke());
            ButtonFeedback.Bind(_settingsButton, () => SettingsClicked?.Invoke());
            ButtonFeedback.Bind(_mainMenuButton, () => MainMenuClicked?.Invoke());
            ButtonFeedback.Bind(_quitButton, () => QuitClicked?.Invoke());
        }
    }
}
