using TMPro;
using UnityEngine;

// What a View sees of the theme applied to it: the user context (theme, locale, text scale) and the fonts,
// palette colors and sprite variants chosen for that context. The same values UIStyledElementsPatch puts on
// registered elements, for things a View draws in code (materials, computed colors, texts it owns outright).
// A View receives it in UIBase.OnPresented; screens built without a prefab read UIManager.Theme.
// default = nothing applied yet: scale 1, no fonts, no colors; sprites pass through.
public readonly struct UIAppliedTheme
{
    public static UIAppliedTheme None => default;

    public UIContext Context { get; }

    private readonly UIFontSet _fonts;
    private readonly UIPalette.ThemeColors _colors;
    private readonly UISpriteSet _sprites;

    public UIAppliedTheme(in UIContext context, UIFontSet fonts, UIPalette palette, UISpriteSet sprites)
    {
        Context = context;
        _fonts = fonts;
        _colors = palette != null ? palette.Get(context.ThemeId) : null;
        _sprites = sprites;
    }

    public string ThemeId => Context.ThemeId;
    public string LocaleId => Context.LocaleId;
    public float TextScale => Context.TextScale > 0f ? Context.TextScale : 1f;

    // The palette has a row for this theme. False = colors are left as authored (ColorFor returns null).
    public bool HasColors => _colors != null;

    // The font for this role in the current locale. Null = keep the authored font.
    public TMP_FontAsset FontFor(UITextRole role) => _fonts != null ? _fonts.Get(LocaleId, role) : null;

    // The text color for this role (Title, Body = TextMain; Caption = TextWeak). Null without a palette.
    public Color? TextColorFor(UITextRole role) =>
        ColorFor(role == UITextRole.Caption ? UIColorRole.TextWeak : UIColorRole.TextMain);

    // The palette color for this role. Null without a palette.
    public Color? ColorFor(UIColorRole role)
    {
        if (_colors == null)
            return null;

        return role switch
        {
            UIColorRole.TextWeak => _colors.textWeak,
            UIColorRole.Accent => _colors.accent,
            UIColorRole.Surface => _colors.surface,
            UIColorRole.Line => _colors.line,
            UIColorRole.Backdrop => _colors.backdrop,
            _ => _colors.textMain,
        };
    }

    // This sprite's variant for the current theme. The sprite itself when it has none.
    public Sprite SpriteFor(Sprite sprite) => _sprites != null ? _sprites.For(sprite, ThemeId) : sprite;
}
