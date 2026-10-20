// Coarse aspect-ratio bucket that layout variants are authored against.
// Produced by DisplayLayoutClassifier; never stored in DisplayContext.
public enum DisplayLayoutClass
{
    Compact = 0,   // narrower than ~16:10  (4:3, 3:2, and all portrait)
    Standard,      // 16:10 .. 16:9
    Wide,          // 18:9 .. 20:9
    UltraWide,     // 21:9 and beyond
}
