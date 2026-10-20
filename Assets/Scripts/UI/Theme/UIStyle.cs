using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // Presentation(TextBindings·ImageBindings)이 닿지 않는 글자·그림을 가진 화면. 등록된 화면이면 UISettingsSync가 시작할 때 한 번,
    // 다크 모드·언어·글자 크기 설정이 바뀔 때마다 부른다(닫힌 화면도). 화면은 제 글자·그림을 StyledTexts·ThemedImages에 넣어 두고 스타일만 넘긴다.
    // TextBindings·ImageBindings에 등록된 것은 Theme가 맞추므로 여기에 넣지 않는다(글자는 크기가 두 번 곱해진다).
    public interface IStyleView
    {
        void ApplyStyle(in UIStyle style);
    }

    // 팔레트 색의 역할. 그림(단색 Image)에 색을 줄 때 쓴다.
    public enum UIColorRole
    {
        TextMain,
        TextWeak,
        Accent,
        Surface,
        Line,
        Backdrop,
    }

    // 지금 UI 스타일: 글자 크기 배율, 언어별 글꼴, 테마의 색(UIPalette), 테마의 그림(UISpriteSet).
    // Theme가 TextBindings·ImageBindings에 넣는 것과 같은 기준이다.
    public readonly struct UIStyle
    {
        public readonly float Scale;
        public readonly string ThemeId;
        private readonly UIFontSet _fonts;
        private readonly string _localeId;
        private readonly UIPalette.ThemeColors _colors;
        private readonly UISpriteSet _sprites;

        public UIStyle(float scale, UIFontSet fonts, string localeId, UIPalette palette, UISpriteSet sprites, string themeId)
        {
            Scale = scale > 0f ? scale : 1f;
            ThemeId = themeId;
            _fonts = fonts;
            _localeId = localeId;
            _colors = palette != null ? palette.Get(themeId) : null;
            _sprites = sprites;
        }

        // 시작 전 기본값: 배율 1, 글꼴·색·그림은 프리팹 그대로.
        public static UIStyle Default => new(1f, null, null, null, null, null);

        public bool HasColors => _colors != null;

        // 이 역할의 글꼴. 글꼴 묶음이 없거나 칸이 비면 null(프리팹 글꼴을 쓴다).
        public TMP_FontAsset FontFor(UITextRole role) =>
            _fonts != null ? _fonts.Get(_localeId, role) : null;

        // 이 글자 역할의 색(Title·Body = textMain, Caption = textWeak). 팔레트가 없으면 null.
        public Color? TextColorFor(UITextRole role)
        {
            if (_colors == null)
                return null;

            return role == UITextRole.Caption ? _colors.textWeak : _colors.textMain;
        }

        // 이 역할의 색. 팔레트가 없으면 null.
        public Color? ColorFor(UIColorRole role)
        {
            if (_colors == null)
                return null;

            switch (role)
            {
                case UIColorRole.TextWeak: return _colors.textWeak;
                case UIColorRole.Accent: return _colors.accent;
                case UIColorRole.Surface: return _colors.surface;
                case UIColorRole.Line: return _colors.line;
                case UIColorRole.Backdrop: return _colors.backdrop;
                default: return _colors.textMain;
            }
        }

        // 이 그림의 지금 테마 버전. 짝이 없으면 그대로.
        public Sprite SpriteFor(Sprite sprite) =>
            _sprites != null ? _sprites.For(sprite, ThemeId) : sprite;
    }
}
