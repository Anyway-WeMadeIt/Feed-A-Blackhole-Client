using System.Collections.Generic;

public sealed class UIResolveResult
{
    public ResolvedUIPresentation Resolved { get; }
    public List<IUIPatch> Patches { get; }
    // The shared assets the patches were built with. The applier uses them to decide what it owns.
    public UIThemeAssets Assets { get; }
    // Null unless UIResolver.TraceEnabled was set when this result was resolved.
    public UIResolveTrace Trace { get; }

    public UIResolveResult(
        ResolvedUIPresentation resolved,
        List<IUIPatch> patches,
        UIResolveTrace trace,
        UIThemeAssets assets = null)
    {
        Resolved = resolved;
        Patches  = patches;
        Trace    = trace;
        Assets   = assets ?? UIThemeAssets.None;
    }
}