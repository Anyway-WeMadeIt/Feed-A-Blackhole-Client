using System;
using System.Collections.Generic;

namespace BlackHole.Unity
{
    // 사용자 설정 중 UI 표현에 들어가는 것(다크 모드, 언어, 글자 크기, Gold 표기)을 UI에 넣는다. 시작할 때 한 번, 설정이 바뀔 때마다.
    // - UIContext: 테마·언어·글자 배율을 담아 UIManager에 바꿔 끼운다 → 보이는 화면을 다시 적용한다(숨은 화면은 다음에 열 때).
    //   테마(Light/Dark)는 Presentation의 Image Theme과 팔레트(UIPalette)의 색을 고른다. 글자는 Theme가 "역할 크기 × 배율"과
    //   "언어 × 역할" 글꼴로 맞춘다. 화면이 코드로 넣은 글자·그림(Styled)도 같은 적용에서 맞고, 그 밖의 것은 화면이 OnPresented에서 맞춘다.
    // - Gold 숫자 표기(Number Format): INumberFormatView 화면에 표기를 넣는다. 화면이 보이는 숫자를 다시 쓴다.
    // 글자 내용(번역)은 Localizer가 따로 맡는다.
    internal sealed class UISettingsSync : IDisposable
    {
        private readonly UIManager _ui;
        private readonly IReadOnlyList<UIBase> _views;
        private readonly GameSettings _settings;

        public UISettingsSync(UIManager ui, IReadOnlyList<UIBase> views, GameSettings settings)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _views = views ?? Array.Empty<UIBase>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _settings.Changed += HandleSettingChanged;
            ApplyNumberFormat();
        }

        public void Dispose() => _settings.Changed -= HandleSettingChanged;

        // 시작할 때의 UIContext. UIManager를 만들 때 쓴다.
        public static UIContext ContextFor(GameSettings settings) =>
            new(ThemeOf(settings).ToId(), settings.LanguageLocale.ToId(), textScale: settings.TextScale);

        // 설정의 Dark Mode → UI 테마.
        public static UITheme ThemeOf(GameSettings settings) =>
            settings.IsOn(GameSettings.DarkMode) ? UITheme.Dark : UITheme.Light;

        private void HandleSettingChanged(string id)
        {
            if (id == GameSettings.NumberFormat)
            {
                ApplyNumberFormat();
                return;
            }

            if (id != GameSettings.TextSize && id != GameSettings.Language && id != GameSettings.DarkMode)
                return;

            _ui.SetContext(_ui.Context.With(ThemeOf(_settings).ToId(), _settings.LanguageLocale.ToId(), _settings.TextScale));
        }

        // Gold 숫자 표기: 숫자를 보이는 화면에 표기를 넣는다. 화면이 마지막 값을 다시 쓴다.
        private void ApplyNumberFormat()
        {
            NumberFormat format = _settings.NumberFormatValue;

            foreach (UIBase view in _views)
            {
                if (view is INumberFormatView numbers)
                    numbers.SetNumberFormat(format);
            }
        }
    }
}
