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

    // When true, each Resolve records how the variant was chosen (UIResolveResult.Trace).
    // Off by default: the trace builds strings on every resolve and nothing reads it in a shipped game.
    public bool TraceEnabled { get; set; }

    public UIResolver(UIContext context)
    {
        _context = context;
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
            _context.TextScale,
            _context.LocaleId,
            _context.ThemeId);
        resolved.Layout?.BuildPatches(patches);
        resolved.ImageTheme?.BuildPatches(patches);
        trace?.Add($"[Patches] {patches.Count}");

        return new UIResolveResult(resolved, patches, trace);
    }
}
