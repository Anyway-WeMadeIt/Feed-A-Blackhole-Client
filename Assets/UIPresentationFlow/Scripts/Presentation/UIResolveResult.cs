using System.Collections.Generic;

public sealed class UIResolveResult
{
    public ResolvedUIPresentation Resolved { get; }
    public List<IUIPatch> Patches { get; }
    public UIResolveTrace Trace { get; }

    public UIResolveResult(
        ResolvedUIPresentation resolved,
        List<IUIPatch> patches,
        UIResolveTrace trace)
    {
        Resolved = resolved;
        Patches  = patches;
        Trace    = trace;
    }
}