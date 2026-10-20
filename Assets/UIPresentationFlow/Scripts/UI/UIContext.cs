using System.Collections.Generic;

public readonly struct UIContext
{
    public readonly string ThemeId;   // e.g. "Light", "Dark"
    public readonly string LocaleId;  // e.g. "ko-KR", "ja-JP"

    // User text size multiplier (1 = as authored). Applied on top of the resolved
    // theme's role sizes; it does not select variants.
    public readonly float TextScale;

    // Active experiment assignments for the current UI context.
    public readonly IReadOnlyDictionary<ExperimentKey, VariantId> Experiments;

    // Debug/QA-only forced variant overrides by Presentation identity.
    // Presentation identity is data identity, not View routing identity.
    public readonly IReadOnlyDictionary<string, VariantId> PresentationOverrides;

    public UIContext(
        string themeId,
        string localeId,
        IReadOnlyDictionary<ExperimentKey, VariantId> experiments = null,
        IReadOnlyDictionary<string, VariantId> presentationOverrides = null,
        float textScale = 1f)
    {
        ThemeId               = themeId;
        LocaleId              = localeId;
        Experiments           = experiments;
        PresentationOverrides = presentationOverrides;
        TextScale             = textScale > 0f ? textScale : 1f;
    }

    // Same context with other user choices. Experiments and overrides are kept.
    public UIContext With(string themeId, string localeId, float textScale)
        => new UIContext(themeId, localeId, Experiments, PresentationOverrides, textScale);

    public static UIContext Default =>
        new UIContext("Light", "ko-KR");
}