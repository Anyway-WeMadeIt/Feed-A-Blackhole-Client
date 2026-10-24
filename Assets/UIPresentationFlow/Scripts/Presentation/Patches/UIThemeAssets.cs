// The design assets every screen shares, handed to the UIResolver once: fonts by locale and role,
// colors by theme, and sprite variants by theme. A ThemeSpec may still point at its own font set or
// palette to override these for one screen; most screens leave those empty.
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
