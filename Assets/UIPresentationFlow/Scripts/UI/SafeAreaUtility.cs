using UnityEngine;

// Projects DisplayContext.SafeArea onto a View's optional SafeAreaRoot.
//
// Contract:
// - SafeAreaRoot is optional.
// - Its parent must represent the full-screen coordinate space.
// - SafeAreaRoot itself must not be controlled by LayoutPatch.
// - Application is absolute and idempotent; no inset is accumulated.
public static class SafeAreaUtility
{
    private const string SafeAreaRootRefId = "SafeAreaRoot";

    public static void Apply(UIBase view, in DisplayContext display)
    {
        if (!view.TryGetRect(SafeAreaRootRefId, out RectTransform safeAreaRoot) || safeAreaRoot == null)
            return;

        Apply(safeAreaRoot, display);
    }

    private static void Apply(RectTransform target, in DisplayContext display)
    {
        Rect safeArea = display.SafeAreaNormalized;

        Vector2 anchorMin = new(
            safeArea.xMin,
            safeArea.yMin);

        Vector2 anchorMax = new(
            safeArea.xMax,
            safeArea.yMax);

        // Always derive the result from DisplayContext.
        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;

        // SafeAreaRoot fills exactly the normalized anchor rectangle.
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}