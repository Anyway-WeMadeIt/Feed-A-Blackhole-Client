// The design assets every screen shares, handed to the UIResolver once: fonts by locale and role,
// colors by theme, and sprite variants by theme. A screen's ThemeSpec holds only its role sizes.
public sealed class UIThemeAssets
{
    public static readonly UIThemeAssets None = new(null, null, null);

    public UIFontSet Fonts { get; }
    public UIPalette Palette { get; }
    public UISpriteSet Sprites { get; }

    public UIThemeAssets(UIFontSet fonts, UIPalette palette, UISpriteSet sprites)
    {
        Fonts = fonts;
        Palette = palette;
        Sprites = sprites;
    }
}
