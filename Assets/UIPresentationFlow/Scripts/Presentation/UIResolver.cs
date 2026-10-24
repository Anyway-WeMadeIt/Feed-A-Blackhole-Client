using System.Collections.Generic;

public sealed class UIResolveTrace
{
    private readonly List<string> _lines = new();
    public void Add(string line) => _lines.Add(line);
    public string Dump() => _lines.Count == 0 
        ? "[Trace] (empty)" 
        : "[Trace]\n- " + string.Join("\n- ", _lines);
}

public sealed class UIResolver
{
    private readonly UIVariantResolver _variantResolver = new();
    private UIContext _context;

    // Shared design assets (fonts, palette, sprite variants). Never null; None when the app has no shared assets.
    public UIThemeAssets Assets { get; }

    // When true, each Resolve records how the variant was chosen (UIResolveResult.Trace).
    // Off by default: the trace builds strings on every resolve and nothing reads it in a shipped game.
    public bool TraceEnabled { get; set; }

    public UIResolver(UIContext context, UIThemeAssets assets = null)
    {
        _context = context;
        Assets = assets ?? UIThemeAssets.None;
    }

    public UIContext Context => _context;

    // Replaces the whole context (contexts are never mutated). Callers re-apply
    // visible views afterwards; hidden views resolve with it when shown.
    public void SetContext(in UIContext context)
    {
        _context = context;
    }

    public UIResolveResult Resolve(
        UIPresentationSpec spec,
        in DisplayContext display)
    {
        UIResolveTrace trace = TraceEnabled ? new UIResolveTrace() : null;
        ResolvedUIPresentation resolved =
            _variantResolver.Resolve(spec, _context, display, trace);

        var patches = new List<IUIPatch>(3);
        resolved.Theme?.BuildPatches(
            patches,
            resolved.TextBindings,
            Assets,
            _context.TextScale,
            _context.LocaleId,
            _context.ThemeId);
        resolved.Layout?.BuildPatches(patches);
        resolved.ImageTheme?.BuildPatches(patches, Assets.Sprites, _context.ThemeId);
        // Code-registered elements (UIStyledElements) get the same theme, with or without a ThemeSpec.
        UIAppliedTheme applied = AppliedTheme;
        patches.Add(new UIStyledElementsPatch(resolved.Theme, applied));
        trace?.Add($"[Patches] {patches.Count}");

        return new UIResolveResult(resolved, patches, trace, Assets, applied);
    }

    // The shared assets as the current context sees them: fonts by locale, colors by theme, sprite variants.
    public UIAppliedTheme AppliedTheme => new(_context, Assets.Fonts, Assets.Palette, Assets.Sprites);

}
