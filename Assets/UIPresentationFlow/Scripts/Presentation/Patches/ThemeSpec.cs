using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    [Header("Base Colors")]
    // Colors by theme (UIContext.ThemeId), shared by every screen's theme. Empty = the two colors below for every theme.
    public UIPalette palette;
    public Color textMainColor = Color.white;
    public Color textWeakColor = Color.gray;

    [Header("Typography")]
    // Fonts by locale and role, shared by every screen's theme. Empty = keep the authored fonts.
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
        float textScale = 1f,
        string localeId = null,
        string themeId = null)
    {
        if (textBindings == null)
            return;

        patches.Add(new ThemeSpecPatch(this, textBindings, textScale, localeId, themeId));
    }
}
