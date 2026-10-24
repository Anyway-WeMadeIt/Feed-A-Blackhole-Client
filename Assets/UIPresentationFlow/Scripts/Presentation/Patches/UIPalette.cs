using System;
using UnityEngine;

// Colors by theme. One shared palette for the app (UIThemeAssets), so a theme's colors
// change in one place instead of in every screen's theme (same idea as UIFontSet for fonts).
//
// Lookup (pure, no Unity state): the row whose themeId equals UIContext.ThemeId; if none, the first row.
[CreateAssetMenu(fileName = "UIPalette", menuName = "UI/Palette")]
public sealed class UIPalette : ScriptableObject
{
    [Serializable]
    public sealed class ThemeColors
    {
        public string themeId;                          // e.g. "Light", "Dark"
        public Color textMain = Color.white;            // Title, Body
        public Color textWeak = Color.gray;             // Caption
        public Color accent = Color.green;              // progress fills, selected states
        public Color surface = Color.white;             // chips/panels drawn by code
        public Color line = Color.gray;                 // outlines, tracks, dividers
        public Color backdrop = Color.white;            // screen background behind the background image
    }

    [Tooltip("The first row is used for themes without their own row.")]
    public ThemeColors[] themes;

    public ThemeColors Get(string themeId)
    {
        if (themes == null || themes.Length == 0)
            return null;

        foreach (ThemeColors row in themes)
        {
            if (row != null && string.Equals(row.themeId, themeId, StringComparison.Ordinal))
                return row;
        }

        return themes[0];
    }
}
