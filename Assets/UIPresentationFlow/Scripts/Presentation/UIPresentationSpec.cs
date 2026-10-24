using UnityEngine;

// A presentation describes how an already-selected UI View should be shown.
//
// It deliberately does NOT select a prefab or a route. Those belong to the
// application/UI lifecycle boundary. This asset only owns adaptive visual data.
[CreateAssetMenu(fileName = "UIPresentation", menuName = "UI/Presentation")]
public sealed class UIPresentationSpec : ScriptableObject
{
    [Tooltip("Stable data/debug identity. This is not a runtime routing key.")]
    public string presentationId;

    public ThemeSpec baseTheme;                 // nullable
    public LayoutPatchSpec baseLayout;          // nullable: base = View as authored
    public UIImageThemeSpec baseImageTheme;     // nullable: base = authored Sprite
    // Image Themes by theme id (each spec carries its themeId). The one matching UIContext.ThemeId replaces
    // baseImageTheme; a variant rule that overrides the image theme still wins. Global sprite variants
    // (UIThemeAssets.Sprites) cover images these do not list.
    public UIImageThemeSpec[] imageThemes;      // nullable
    public UITextBindingCatalog textBindings;   // nullable: View text semantics
    public UIVariantRule[] variants;             // nullable
}
