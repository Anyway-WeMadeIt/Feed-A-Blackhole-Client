using System;
using TMPro;
using UnityEngine;

// Fonts by locale and text role. One shared set for the app (UIThemeAssets), so a font
// changes in one place instead of in every screen's theme.
//
// Lookup (pure, no Unity state):
//   - The locale row whose localeId equals UIContext.LocaleId; if none, the first row.
//   - The role's font in that row; if empty, the row's body font.
// Weight is not a role: a bold variant of a font goes into that font asset's
// Font Weights table and is picked by the text's Bold style.
[CreateAssetMenu(fileName = "UIFontSet", menuName = "UI/FontSet")]
public sealed class UIFontSet : ScriptableObject
{
    [Serializable]
    public sealed class LocaleFonts
    {
        public string localeId;         // e.g. "en-US", "ko-KR"
        public TMP_FontAsset title;     // empty = body
        public TMP_FontAsset body;
        public TMP_FontAsset caption;   // empty = body
    }

    [Tooltip("The first row is used for locales without their own row.")]
    public LocaleFonts[] locales;

    public TMP_FontAsset Get(string localeId, UITextRole role)
    {
        LocaleFonts row = Find(localeId);
        if (row == null)
            return null;

        TMP_FontAsset font = role switch
        {
            UITextRole.Title => row.title,
            UITextRole.Caption => row.caption,
            _ => row.body,
        };

        return font != null ? font : row.body;
    }

    private LocaleFonts Find(string localeId)
    {
        if (locales == null || locales.Length == 0)
            return null;

        foreach (LocaleFonts row in locales)
        {
            if (row != null && string.Equals(row.localeId, localeId, StringComparison.Ordinal))
                return row;
        }

        return locales[0];
    }
}
