using System.Collections.Generic;
using UnityEngine;

public sealed class UIImageBindingSet : ScriptableObject
{
    // The View's key (UIViewKey.Of: type name, or [UIViewKey]).
    // Example: "TitleUIRoot" or "Game.UI.TitleUIRoot".
    public string viewId;

    public List<UIImageBindingEntry> images = new();
}
