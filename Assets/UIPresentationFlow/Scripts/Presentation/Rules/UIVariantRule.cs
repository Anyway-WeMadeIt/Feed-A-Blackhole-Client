using System;

[Serializable]
public class UIVariantRule
{
    public string variantId; // e.g. "Shop_Layout_B"
    public int priority;

    public VariantCondition condition;

    // A variant changes presentation fields only.
    public ThemeSpec overrideTheme;
    public LayoutPatchSpec overrideLayout;
    public UIImageThemeSpec overrideImageTheme;
}
