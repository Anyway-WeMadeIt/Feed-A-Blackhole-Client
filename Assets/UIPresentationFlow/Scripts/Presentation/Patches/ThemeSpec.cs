using System.Collections.Generic;
using UnityEngine;

// A screen's typography: the size of each text role. Fonts, colors and sprite variants are not here —
// they are the shared UIThemeAssets (font set by locale, palette by theme, sprite set), the same for every screen.
// Catalog texts (TextBindings) get role size × user text scale, the role's font and the palette's text color.
[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    [Header("Role Sizes")]
    public int titleSize = 28;
    public int bodySize = 16;
    public int captionSize = 13;

    public float SizeOf(UITextRole role) => role switch
    {
        UITextRole.Title => titleSize,
        UITextRole.Caption => captionSize,
        _ => bodySize,
    };

    // assets: the shared fonts and palette. textScale/localeId/themeId: the user context (UIContext).
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
}
