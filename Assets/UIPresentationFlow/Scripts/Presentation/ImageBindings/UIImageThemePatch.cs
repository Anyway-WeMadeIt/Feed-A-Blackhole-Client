using UnityEngine;
using UnityEngine.UI;

public sealed class UIImageThemePatch : IUIPatch
{
    private readonly UIImageThemeSpec _theme;
    private readonly UISpriteSet _sprites;
    private readonly string _themeId;

    // sprites/themeId: the global sprite variants. An entry with no sprite of its own takes the variant of its
    // base sprite from here; without a variant the image keeps its authored sprite (it is never blanked).
    public UIImageThemePatch(UIImageThemeSpec theme, UISpriteSet sprites = null, string themeId = null)
    {
        _theme = theme;
        _sprites = sprites;
        _themeId = themeId;
    }

    public void Apply(IUIPresentationRefProvider refs)
    {
        if (_theme == null || refs == null)
            return;

        if (!_theme.TryGetBinding(refs.GetType(), out UIImageBindingSet binding)
            || binding?.images == null)
        {
            return;
        }

        string targetName = refs.GetType().Name;

        foreach (UIImageBindingEntry entry in binding.images)
        {
            if (entry == null || entry.stale)
                continue;

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            if (!refs.TryGetImage(refId, out Image image) || image == null)
            {
                Debug.LogWarning(
                    $"[UIImageThemePatch] Image not found for refId='{refId}' on '{targetName}'.");
                continue;
            }

            image.sprite = entry.sprite != null ? entry.sprite : VariantOf(entry.baseSprite != null ? entry.baseSprite : image.sprite);

            // The Selectable is usually on the parent with this image as its target graphic.
            Selectable selectable = entry.HasStateSprites ? SelectableOf(image) : null;

            if (selectable != null && selectable.transition == Selectable.Transition.SpriteSwap)
            {
                SpriteState state = selectable.spriteState;
                if (entry.highlightedSprite != null) state.highlightedSprite = entry.highlightedSprite;
                if (entry.pressedSprite != null) state.pressedSprite = entry.pressedSprite;
                if (entry.disabledSprite != null) state.disabledSprite = entry.disabledSprite;
                selectable.spriteState = state;
            }
        }
    }

    private Sprite VariantOf(Sprite sprite) => _sprites != null ? _sprites.For(sprite, _themeId) : sprite;

    // The Selectable (on this object or a parent) whose target graphic is this image. Null if none.
    public static Selectable SelectableOf(Image image)
    {
        Selectable selectable = image.GetComponentInParent<Selectable>(true);
        return selectable != null && selectable.targetGraphic == image ? selectable : null;
    }
}
