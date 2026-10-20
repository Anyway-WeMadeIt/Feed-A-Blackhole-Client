using System.Collections.Generic;

public sealed class ResolvedUIPresentation
{
    public string PresentationId { get; }
    public UIPresentationSpec BaseSpec { get; }

    public ThemeSpec Theme { get; }
    public LayoutPatchSpec Layout { get; }
    public UIImageThemeSpec ImageTheme { get; }
    public UITextBindingCatalog TextBindings { get; }

    // IDs of matched rules in priority order.
    // Forced override produces a single entry.
    public IReadOnlyList<string> AppliedVariantIds { get; }

    public ResolvedUIPresentation(
        UIPresentationSpec baseSpec,
        ThemeSpec theme,
        LayoutPatchSpec layout,
        UIImageThemeSpec imageTheme,
        List<string> appliedVariantIds)
    {
        BaseSpec = baseSpec;
        PresentationId = baseSpec.presentationId;
        Theme = theme;
        Layout = layout;
        ImageTheme = imageTheme;
        TextBindings = baseSpec.textBindings;
        AppliedVariantIds = appliedVariantIds.AsReadOnly();
    }
}
