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
    public UITextBindingCatalog textBindings;   // nullable: View text semantics
    public UIVariantRule[] variants;             // nullable
}
