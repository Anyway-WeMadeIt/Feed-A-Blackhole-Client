using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    [Header("Base Colors")]
    // Override for this screen only. Empty = the shared palette (UIThemeAssets); without that, the two colors below.
    public UIPalette palette;
    public Color textMainColor = Color.white;
    public Color textWeakColor = Color.gray;

    [Header("Typography")]
    // Override for this screen only. Empty = the shared font set (UIThemeAssets); without that, keep the authored fonts.
    public UIFontSet fonts;
    public int titleSize = 28;
    public int bodySize = 16;
    public int captionSize = 13;

    // textScale: user text size multiplier applied to the role sizes (UIContext.TextScale).
    // localeId: picks the locale row of the font set (UIContext.LocaleId).
    // themeId: picks the theme row of the palette (UIContext.ThemeId).
    public void BuildPatches(
        List<IUIPatch> patches,
        UITextBindingCatalog textBindings,
        UIThemeAssets assets,
        float textScale = 1f,
        string localeId = null,
        string themeId = null)
    {
        if (textBindings == null)
            return;

        patches.Add(new ThemeSpecPatch(this, textBindings, assets, textScale, localeId, themeId));
    }

    // The font set this screen uses: its own override, else the shared one. Null = keep authored fonts.
    public UIFontSet FontsOr(UIThemeAssets assets) => fonts != null ? fonts : assets?.Fonts;

    // The palette this screen uses: its own override, else the shared one. Null = the spec's own two colors.
    public UIPalette PaletteOr(UIThemeAssets assets) => palette != null ? palette : assets?.Palette;
}
