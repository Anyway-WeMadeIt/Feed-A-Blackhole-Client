using System;
using System.Collections.Generic;

namespace BlackHole.Unity
{
    // 사용자 설정 중 UI 표현에 들어가는 것(다크 모드, 언어, 글자 크기)을 UI에 넣는다. 시작할 때 한 번, 설정이 바뀔 때마다.
    // - UIContext: 테마·언어·글자 배율을 담아 UIManager에 바꿔 끼운다 → 보이는 화면을 다시 적용한다.
    //   테마(Light/Dark)는 Presentation의 Dark 변형(ImageTheme)과 팔레트(UIPalette)의 색을 고른다.
    //   TextBindings에 역할로 등록된 글자는 Theme가 "역할 크기 × 배율"과 "언어 × 역할" 글꼴로 맞춘다. 숨은 화면은 다음에 열 때 맞춰진다.
    // - TextBindings·ImageBindings가 닿지 않는 글자·그림: 등록된 화면 중 IStyleView가 같은 배율·글꼴·테마 색·테마 그림(UIStyle)을 넣는다.
    // 글꼴 묶음(UIFontSet)·팔레트(UIPalette)는 Theme들이 가리키는 것과 같은 에셋이다(GameBootstrap이 확인한다).
    // - Gold 숫자 표기(Number Format): GoldText.Format을 바꾸고 INumberFormatView가 보이는 숫자를 다시 쓴다.
    // 글자 내용(번역)은 Localizer가 따로 맡는다.
    internal sealed class UISettingsSync : IDisposable
    {
        private readonly UIManager _ui;
        private readonly IReadOnlyList<UIBase> _views;
        private readonly GameSettings _settings;
        private readonly UIFontSet _fonts;
        private readonly UIPalette _palette;
        private readonly UISpriteSet _sprites;

        public UISettingsSync(
            UIManager ui, IReadOnlyList<UIBase> views, GameSettings settings,
            UIFontSet fonts, UIPalette palette, UISpriteSet sprites)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _views = views ?? Array.Empty<UIBase>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _fonts = fonts;
            _palette = palette;
            _sprites = sprites;

            _settings.Changed += HandleSettingChanged;
            ApplyTextStyle();
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
            ApplyTextStyle();
        }

        // Gold 숫자 표기: GoldText가 쓰는 표기를 바꾸고, 숫자를 보이는 화면이 마지막 값을 다시 쓴다.
        private void ApplyNumberFormat()
        {
            GoldText.Format = _settings.NumberFormatValue;

            foreach (UIBase view in _views)
            {
                if (view is INumberFormatView numbers)
                    numbers.RefreshNumbers();
            }
        }

        private void ApplyTextStyle()
        {
            var style = new UIStyle(
                _settings.TextScale, _fonts, _settings.LanguageLocale.ToId(), _palette, _sprites, ThemeOf(_settings).ToId());

            foreach (UIBase view in _views)
            {
                if (view is IStyleView styled)
                    styled.ApplyStyle(style);
            }
        }
    }
}
