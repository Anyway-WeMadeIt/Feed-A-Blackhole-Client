using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class PausePanel : UIPanel<PausePanel.Refs>, ILocalizedView
    {
        public enum Refs
        {
            ResumeBtn_Button,
            SettingsBtn_Button,
            MainMenuBtn_Button,
            QuitBtn_Button,
            // 웹 빌드에서 종료 버튼 자리에 두는 전체 화면 버튼(WebFullscreen). 웹이 아니면 숨긴다.
            FullscreenBtn_Button,
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
            FullscreenBtn_Image,
            // 글자는 코드가 상태(전체 화면/창 모드)에 맞춰 쓴다. UIText 표에는 화면 키를 두지 않는다.
            FullscreenBtn_Text,
        }

        private Button _resumeButton;
        private Button _settingsButton;
        private Button _mainMenuButton;
        private Button _quitButton;
        private Button _fullscreenButton;
        private ButtonAnimation _resumeAnimation;
        private ButtonAnimation _settingsAnimation;
        private ButtonAnimation _mainMenuAnimation;
        private ButtonAnimation _quitAnimation;
        private ButtonAnimation _fullscreenAnimation;
        private TMP_Text _fullscreenText;
        private Localizer _localizer;
        private bool _fullscreenShown;

        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action MainMenuClicked;
        public event Action QuitClicked;
        public event Action FullscreenClicked;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _resumeButton = View.Button(Refs.ResumeBtn_Button);
            _settingsButton = View.Button(Refs.SettingsBtn_Button);
            _mainMenuButton = View.Button(Refs.MainMenuBtn_Button);
            _quitButton = View.Button(Refs.QuitBtn_Button);
            _fullscreenButton = View.Button(Refs.FullscreenBtn_Button);
            _fullscreenText = View.Text(Refs.FullscreenBtn_Text);

            _resumeAnimation = ButtonAnimation.Of(_resumeButton);
            _settingsAnimation = ButtonAnimation.Of(_settingsButton);
            _mainMenuAnimation = ButtonAnimation.Of(_mainMenuButton);
            _quitAnimation = ButtonAnimation.Of(_quitButton);
            _fullscreenAnimation = ButtonAnimation.Of(_fullscreenButton);

            BindEvent(_resumeButton, ClickResumeButton);
            BindEvent(_resumeButton, HoverResumeButton, ETouchEvent.PointerEnter);
            BindEvent(_resumeButton, LeaveResumeButton, ETouchEvent.PointerExit);
            BindEvent(_resumeButton, PressResumeButton, ETouchEvent.PointerDown);
            BindEvent(_resumeButton, ReleaseResumeButton, ETouchEvent.PointerUp);

            BindEvent(_settingsButton, ClickSettingsButton);
            BindEvent(_settingsButton, HoverSettingsButton, ETouchEvent.PointerEnter);
            BindEvent(_settingsButton, LeaveSettingsButton, ETouchEvent.PointerExit);
            BindEvent(_settingsButton, PressSettingsButton, ETouchEvent.PointerDown);
            BindEvent(_settingsButton, ReleaseSettingsButton, ETouchEvent.PointerUp);

            BindEvent(_mainMenuButton, ClickMainMenuButton);
            BindEvent(_mainMenuButton, HoverMainMenuButton, ETouchEvent.PointerEnter);
            BindEvent(_mainMenuButton, LeaveMainMenuButton, ETouchEvent.PointerExit);
            BindEvent(_mainMenuButton, PressMainMenuButton, ETouchEvent.PointerDown);
            BindEvent(_mainMenuButton, ReleaseMainMenuButton, ETouchEvent.PointerUp);

            BindEvent(_quitButton, ClickQuitButton);
            BindEvent(_quitButton, HoverQuitButton, ETouchEvent.PointerEnter);
            BindEvent(_quitButton, LeaveQuitButton, ETouchEvent.PointerExit);
            BindEvent(_quitButton, PressQuitButton, ETouchEvent.PointerDown);
            BindEvent(_quitButton, ReleaseQuitButton, ETouchEvent.PointerUp);

            BindEvent(_fullscreenButton, ClickFullscreenButton);
            BindEvent(_fullscreenButton, HoverFullscreenButton, ETouchEvent.PointerEnter);
            BindEvent(_fullscreenButton, LeaveFullscreenButton, ETouchEvent.PointerExit);
            BindEvent(_fullscreenButton, PressFullscreenButton, ETouchEvent.PointerDown);
            BindEvent(_fullscreenButton, ReleaseFullscreenButton, ETouchEvent.PointerUp);

            // 웹(브라우저)에서는 게임이 페이지를 닫을 수 없어 종료 버튼 대신 같은 자리의 전체 화면 버튼을 보인다.
            // 빌드 대상이 Web인 에디터 플레이에서도 같게 해, 웹 빌드와 같은 화면을 에디터에서 미리 본다.
            _quitButton.gameObject.SetActive(!WebFullscreen.Available);
            _fullscreenButton.gameObject.SetActive(WebFullscreen.Available);
        }

        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;
            RefreshFullscreenLabel();
        }

        // Esc로 전체 화면이 풀리면 게임은 알림을 받지 못하므로 상태를 보고 글자를 맞춘다.
        private void Update()
        {
            if (WebFullscreen.Available && _fullscreenShown != WebFullscreen.IsOn)
                RefreshFullscreenLabel();
        }

        private void RefreshFullscreenLabel()
        {
            _fullscreenShown = WebFullscreen.IsOn;
            if (_fullscreenText != null && _localizer != null)
                _fullscreenText.text = _localizer.Get(WebFullscreen.LabelKey);
        }

        private void ClickResumeButton(PointerEventData _) => ResumeClicked?.Invoke();
        private void HoverResumeButton(PointerEventData _) => _resumeAnimation.Hover();
        private void LeaveResumeButton(PointerEventData _) => _resumeAnimation.Leave();
        private void PressResumeButton(PointerEventData _) => _resumeAnimation.Press();
        private void ReleaseResumeButton(PointerEventData _) => _resumeAnimation.Release();

        private void ClickSettingsButton(PointerEventData _) => SettingsClicked?.Invoke();
        private void HoverSettingsButton(PointerEventData _) => _settingsAnimation.Hover();
        private void LeaveSettingsButton(PointerEventData _) => _settingsAnimation.Leave();
        private void PressSettingsButton(PointerEventData _) => _settingsAnimation.Press();
        private void ReleaseSettingsButton(PointerEventData _) => _settingsAnimation.Release();

        private void ClickMainMenuButton(PointerEventData _) => MainMenuClicked?.Invoke();
        private void HoverMainMenuButton(PointerEventData _) => _mainMenuAnimation.Hover();
        private void LeaveMainMenuButton(PointerEventData _) => _mainMenuAnimation.Leave();
        private void PressMainMenuButton(PointerEventData _) => _mainMenuAnimation.Press();
        private void ReleaseMainMenuButton(PointerEventData _) => _mainMenuAnimation.Release();

        private void ClickQuitButton(PointerEventData _) => QuitClicked?.Invoke();
        private void HoverQuitButton(PointerEventData _) => _quitAnimation.Hover();
        private void LeaveQuitButton(PointerEventData _) => _quitAnimation.Leave();
        private void PressQuitButton(PointerEventData _) => _quitAnimation.Press();
        private void ReleaseQuitButton(PointerEventData _) => _quitAnimation.Release();

        private void ClickFullscreenButton(PointerEventData _) => FullscreenClicked?.Invoke();
        private void HoverFullscreenButton(PointerEventData _) => _fullscreenAnimation.Hover();
        private void LeaveFullscreenButton(PointerEventData _) => _fullscreenAnimation.Leave();
        private void PressFullscreenButton(PointerEventData _) => _fullscreenAnimation.Press();
        private void ReleaseFullscreenButton(PointerEventData _) => _fullscreenAnimation.Release();
    }
}
