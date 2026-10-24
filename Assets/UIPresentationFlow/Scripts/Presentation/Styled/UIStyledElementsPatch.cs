using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Applies the resolved theme to a View's code-registered elements (UIStyledElements):
// - texts: font by locale and role, size by the entry's policy × text scale, color by role
// - images: sprite variant for the theme (sprite set) and/or palette color by role (alpha kept)
// Catalog entries (TextBindings, Image Themes) are handled by ThemeSpecPatch and UIImageThemePatch;
// this patch covers everything the View registered in code. Without a ThemeSpec the Role size policy
// falls back to Prefab (there is no role size to use).
public sealed class UIStyledElementsPatch : IUIPatch, IUIStyledElementsApplier
{
    private readonly ThemeSpec _theme;
    private readonly UIAppliedTheme _applied;

    // theme: the screen's ThemeSpec (role sizes); null when the presentation has none.
    // applied: the fonts, colors and sprites chosen for the user context (UIResolver.AppliedTheme).
    public UIStyledElementsPatch(ThemeSpec theme, in UIAppliedTheme applied)
    {
        _theme = theme;
        _applied = applied;
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

        TMP_FontAsset font = _applied.FontFor(entry.Role);
        text.font = font != null ? font : entry.Baseline.Font;

        Color color = _applied.TextColorFor(entry.Role) ?? entry.Baseline.Color;
        color.a = entry.Baseline.Color.a;
        text.color = color;

        switch (entry.SizePolicy)
        {
            case UITextSizePolicy.Role when _theme != null:
                float size = _theme.SizeOf(entry.Role) * _applied.TextScale;
                if (text.enableAutoSizing)
                    text.fontSizeMax = size;
                text.fontSize = size;
                break;

            default: // Prefab, or Role without a theme
                if (text.enableAutoSizing)
                {
                    text.fontSizeMin = entry.Baseline.FontSizeMin * _applied.TextScale;
                    text.fontSizeMax = entry.Baseline.FontSizeMax * _applied.TextScale;
                }
                else
                {
                    text.fontSize = entry.Baseline.FontSize * _applied.TextScale;
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
            Color color = _applied.ColorFor(role) ?? entry.Baseline.Color;
            color.a = entry.Baseline.Color.a;
            image.color = color;
        }
    }

    private Sprite SpriteFor(Sprite sprite) => _applied.SpriteFor(sprite);
}

// A View that has code-registered styled elements. UIBase implements it.
public interface IUIStyledElementsOwner
{
    UIStyledElements Styled { get; }
}
