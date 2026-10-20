public sealed partial class UIManager
{
    public UIContext Context => _resolver.Context;

    // Swap the user context (theme, locale, text size) and re-apply what is visible.
    // Hidden views pick it up when they are shown next.
    public void SetContext(in UIContext context)
    {
        _resolver.SetContext(context);
        ReapplyVisible(UnityDisplayContextProvider.GetCurrent());
    }

    private void ApplyPresentation(
        UIBase view,
        UIPresentationSpec spec,
        in DisplayContext display)
    {
        view.EnsureInitialized();

        if (view is not IUIPage)
            SafeAreaUtility.Apply(view, display);

        UIResolveResult result =
            _resolver.Resolve(spec, display);

        _presentationApplier.Apply(view, result);

        LastDisplay = display;
        LastResult = result;
    }
}