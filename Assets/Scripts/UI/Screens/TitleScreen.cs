using System;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 타이틀 화면. 버튼이 눌렸다는 사실만 알린다. 무엇을 할지는 ScreenFlow가 정한다.
    // 버튼 연출(올림·누름)은 버튼마다 붙은 ButtonAnimation이 맡는다. 화면은 사건을 넘기기만 한다.
    public sealed class TitleScreen : UIRoot<TitleScreen.Refs>
    {
        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        // 테마가 들어왔다: 배경 위 문양의 머티리얼을 테마에 맞춘다. 바탕색·그림·글자는 Presentation과 Styled가 맞췄다.
        protected override void OnPresented(in UIAppliedTheme theme) => Ambient?.Apply(theme.ThemeId);

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

        protected override void OnInitialize()
        {
            // 화면 바탕색(배경 그림 뒤)은 팔레트의 Backdrop을 따른다. 나머지 그림·글자는 Presentation이 맞춘다.
            Styled.AddImageColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _startButton = View.Button(Refs.StartBtn_Button);
            _settingsButton = View.Button(Refs.SettingsBtn_Button);
            _quitButton = View.Button(Refs.QuitBtn_Button);

            ButtonFeedback.Bind(_startButton, () => StartClicked?.Invoke());
            ButtonFeedback.Bind(_settingsButton, () => SettingsClicked?.Invoke());
            ButtonFeedback.Bind(_quitButton, () => QuitClicked?.Invoke());
        }
    }
}
