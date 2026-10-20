// Policy, not fact: turns an aspect ratio into the coarse class that layout
// variants are authored against. It lives outside DisplayContext so the same
// facts can be classified differently by a different project or a later
// policy change, and so every threshold exists in exactly one place.
//
// Thresholds (M3). Chosen against the canonical demo screen, not from a
// device list; adjust here, never in individual VariantConditions.
//
//   Compact     a <  1.60          4:3 = 1.333   3:2 = 1.5    portrait
//   Standard    1.60 <= a < 2.00   16:10 = 1.6   16:9 = 1.778
//   Wide        2.00 <= a < 2.30   18:9 = 2.0    19.5:9 = 2.167   20:9 = 2.222
//   UltraWide   a >= 2.30          21:9 = 2.333
//
// Only width/height is considered; orientation is a separate condition.
public static class DisplayLayoutClassifier
{
    public const float StandardMin  = 1.60f;
    public const float WideMin      = 2.00f;
    public const float UltraWideMin = 2.30f;

    public static DisplayLayoutClass Classify(float aspectRatio)
    {
        if (aspectRatio >= UltraWideMin) return DisplayLayoutClass.UltraWide;
        if (aspectRatio >= WideMin)      return DisplayLayoutClass.Wide;
        if (aspectRatio >= StandardMin)  return DisplayLayoutClass.Standard;
        return DisplayLayoutClass.Compact;
    }

    public static DisplayLayoutClass Classify(in DisplayContext display)
        => Classify(display.AspectRatio);
}
