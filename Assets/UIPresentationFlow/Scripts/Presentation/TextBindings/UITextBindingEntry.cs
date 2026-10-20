using System;

[Serializable]
public sealed class UITextBindingEntry
{
    public string refId;
    public UITextRole role = UITextRole.Unassigned;
    public bool stale;

    // Keep the theme size regardless of the user text size (e.g. a logo-like title).
    public bool fixedSize;

    // Keep the authored font in every locale (e.g. an untranslated logo).
    public bool fixedFont;
}
