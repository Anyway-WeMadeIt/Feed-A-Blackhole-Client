using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 타이틀 화면. 버튼이 눌렸다는 사실만 알린다. 무엇을 할지는 ScreenFlow가 정한다.
    // 버튼 연출(올림·누름)은 버튼마다 붙은 ButtonAnimation이 맡는다. 화면은 사건을 넘기기만 한다.
    public sealed class TitleScreen : UIRoot<TitleScreen.Refs>, IStyleView, IUISoundView
    {
        // 테마 색: 화면 바탕색(배경 그림 뒤). 나머지 그림·글자는 Presentation이 맞춘다.
        private readonly ThemedImages _themed = new();

        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        public void SetSounds(UISounds sounds) => ButtonAnimation.SetSounds(this, sounds);

        public void ApplyStyle(in UIStyle style)
        {
            _themed.SetStyle(style);
            Ambient?.Apply(style.ThemeId);
        }

        public enum Refs
        {
            StartBtn_Button,
            SettingsBtn_Button,
            QuitBtn_Button,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
            // 배경 위의 별빛·궤도 문양(UIScreenAmbient). 테마에 따라 머티리얼만 바뀐다.
            Ambient,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // _Image는 Image Theme(UI/ImageBindings)가 기본 그림을, _Text는 ThemeSpec이 역할(Title·Body)에 맞춰 글꼴·크기·색을 정한다.
            // 버튼의 올림·누름·막힘 그림은 프리팹의 Button(Sprite Swap)에 있다.
            Logo_Image,
            Title_Text,
            StartBtn_Image,
            StartBtn_Text,
            SettingsBtn_Image,
            SettingsBtn_Text,
            QuitBtn_Image,
            QuitBtn_Text,
            // 화면 전체를 덮는 배경(안전 영역 밖까지). 기본은 호 무늬 배경.
            Background_Image,
        }

        public event Action StartClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        private Button _startButton;
        private Button _settingsButton;
        private Button _quitButton;
        private ButtonAnimation _startAnimation;
        private ButtonAnimation _settingsAnimation;
        private ButtonAnimation _quitAnimation;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _themed.AddColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _startButton = View.Button(Refs.StartBtn_Button);
            _settingsButton = View.Button(Refs.SettingsBtn_Button);
            _quitButton = View.Button(Refs.QuitBtn_Button);

            _startAnimation = ButtonAnimation.Of(_startButton);
            _settingsAnimation = ButtonAnimation.Of(_settingsButton);
            _quitAnimation = ButtonAnimation.Of(_quitButton);

            BindEvent(_startButton, ClickStartButton);
            BindEvent(_startButton, HoverStartButton, ETouchEvent.PointerEnter);
            BindEvent(_startButton, LeaveStartButton, ETouchEvent.PointerExit);
            BindEvent(_startButton, PressStartButton, ETouchEvent.PointerDown);
            BindEvent(_startButton, ReleaseStartButton, ETouchEvent.PointerUp);

            BindEvent(_settingsButton, ClickSettingsButton);
            BindEvent(_settingsButton, HoverSettingsButton, ETouchEvent.PointerEnter);
            BindEvent(_settingsButton, LeaveSettingsButton, ETouchEvent.PointerExit);
            BindEvent(_settingsButton, PressSettingsButton, ETouchEvent.PointerDown);
            BindEvent(_settingsButton, ReleaseSettingsButton, ETouchEvent.PointerUp);

            BindEvent(_quitButton, ClickQuitButton);
            BindEvent(_quitButton, HoverQuitButton, ETouchEvent.PointerEnter);
            BindEvent(_quitButton, LeaveQuitButton, ETouchEvent.PointerExit);
            BindEvent(_quitButton, PressQuitButton, ETouchEvent.PointerDown);
            BindEvent(_quitButton, ReleaseQuitButton, ETouchEvent.PointerUp);
        }

        private void ClickStartButton(PointerEventData _) => StartClicked?.Invoke();
        private void HoverStartButton(PointerEventData _) => _startAnimation.Hover();
        private void LeaveStartButton(PointerEventData _) => _startAnimation.Leave();
        private void PressStartButton(PointerEventData _) => _startAnimation.Press();
        private void ReleaseStartButton(PointerEventData _) => _startAnimation.Release();

        private void ClickSettingsButton(PointerEventData _) => SettingsClicked?.Invoke();
        private void HoverSettingsButton(PointerEventData _) => _settingsAnimation.Hover();
        private void LeaveSettingsButton(PointerEventData _) => _settingsAnimation.Leave();
        private void PressSettingsButton(PointerEventData _) => _settingsAnimation.Press();
        private void ReleaseSettingsButton(PointerEventData _) => _settingsAnimation.Release();

        private void ClickQuitButton(PointerEventData _) => QuitClicked?.Invoke();
        private void HoverQuitButton(PointerEventData _) => _quitAnimation.Hover();
        private void LeaveQuitButton(PointerEventData _) => _quitAnimation.Leave();
        private void PressQuitButton(PointerEventData _) => _quitAnimation.Press();
        private void ReleaseQuitButton(PointerEventData _) => _quitAnimation.Release();
    }
}
