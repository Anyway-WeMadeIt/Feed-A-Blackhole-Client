using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Applies the resolved theme to a View's code-registered elements (UIStyledElements):
// - texts: font by locale and role, size by the entry's policy × text scale, color by role
// - images: sprite variant for the theme (sprite set) and/or palette color by role (alpha kept)
// Catalog entries (TextBindings, Image Themes) are handled by ThemeSpecPatch and UIImageThemePatch;
// this patch covers everything the View registered in code. Without a ThemeSpec only the Prefab size
// policy and the shared assets apply.
public sealed class UIStyledElementsPatch : IUIPatch, IUIStyledElementsApplier
{
    private readonly ThemeSpec _theme;
    private readonly UIFontSet _fonts;
    private readonly UIPalette.ThemeColors _colors;
    private readonly UISpriteSet _sprites;
    private readonly float _textScale;
    private readonly string _localeId;
    private readonly string _themeId;

    public UIStyledElementsPatch(ThemeSpec theme, UIThemeAssets assets, float textScale, string localeId, string themeId)
    {
        _theme = theme;
        _fonts = theme != null ? theme.FontsOr(assets) : assets?.Fonts;
        UIPalette palette = theme != null ? theme.PaletteOr(assets) : assets?.Palette;
        _colors = palette != null ? palette.Get(themeId) : null;
        _sprites = assets?.Sprites;
        _textScale = textScale > 0f ? textScale : 1f;
        _localeId = localeId;
        _themeId = themeId;
    }

    public void Apply(IUIPresentationRefProvider refs)
    {
        if (refs is not IUIStyledElementsOwner owner)
            return;

        UIStyledElements styled = owner.Styled;

        foreach (UIStyledElements.TextEntry entry in styled.Texts)
            Apply(entry);

        foreach (UIStyledElements.ImageEntry entry in styled.Images)
            Apply(entry);

        styled.SetApplier(this);
    }

    public void Apply(UIStyledElements.TextEntry entry)
    {
        TMP_Text text = entry.Text;
        if (text == null)
            return;

        entry.Baseline.CaptureOnce(text);

        TMP_FontAsset font = _fonts != null ? _fonts.Get(_localeId, entry.Role) : null;
        text.font = font != null ? font : entry.Baseline.Font;

        Color color = TextColorFor(entry.Role) ?? entry.Baseline.Color;
        color.a = entry.Baseline.Color.a;
        text.color = color;

        switch (entry.SizePolicy)
        {
            case UITextSizePolicy.Role when _theme != null:
                float size = RoleSize(entry.Role) * _textScale;
                if (text.enableAutoSizing)
                    text.fontSizeMax = size;
                text.fontSize = size;
                break;

            default: // Prefab, or Role without a theme
                if (text.enableAutoSizing)
                {
                    text.fontSizeMin = entry.Baseline.FontSizeMin * _textScale;
                    text.fontSizeMax = entry.Baseline.FontSizeMax * _textScale;
                }
                else
                {
                    text.fontSize = entry.Baseline.FontSize * _textScale;
                }
                break;
        }
    }

    public void Apply(UIStyledElements.ImageEntry entry)
    {
        Image image = entry.Image;
        if (image == null)
            return;

        entry.Baseline.CaptureOnce(image);

        if (entry.ThemesSprite)
        {
            image.sprite = SpriteFor(entry.Baseline.Sprite);

            if (entry.Baseline.HasSpriteState)
            {
                Selectable selectable = UIImageThemePatch.SelectableOf(image);
                if (selectable != null && selectable.transition == Selectable.Transition.SpriteSwap)
                {
                    SpriteState state = entry.Baseline.SpriteState;
                    state.highlightedSprite = SpriteFor(state.highlightedSprite);
                    state.pressedSprite = SpriteFor(state.pressedSprite);
                    state.selectedSprite = SpriteFor(state.selectedSprite);
                    state.disabledSprite = SpriteFor(state.disabledSprite);
                    selectable.spriteState = state;
                }
            }
        }

        if (entry.ColorRole is UIColorRole role)
        {
            Color color = ColorFor(role) ?? entry.Baseline.Color;
            color.a = entry.Baseline.Color.a;
            image.color = color;
        }
    }

    private float RoleSize(UITextRole role) => role switch
    {
        UITextRole.Title => _theme.titleSize,
        UITextRole.Caption => _theme.captionSize,
        _ => _theme.bodySize,
    };

    private Color? TextColorFor(UITextRole role)
    {
        if (_colors != null)
            return role == UITextRole.Caption ? _colors.textWeak : _colors.textMain;

        if (_theme != null)
            return role == UITextRole.Caption ? _theme.textWeakColor : _theme.textMainColor;

        return null;
    }

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

    private Sprite SpriteFor(Sprite sprite) => _sprites != null ? _sprites.For(sprite, _themeId) : sprite;
}

// A View that has code-registered styled elements. UIBase implements it.
public interface IUIStyledElementsOwner
{
    UIStyledElements Styled { get; }
}
