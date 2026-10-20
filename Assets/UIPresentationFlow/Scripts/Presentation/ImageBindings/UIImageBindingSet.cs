using System.Collections.Generic;
using UnityEngine;

public sealed class UIImageBindingSet : ScriptableObject
{
    // Concrete UI View type FullName.
    // Example: "TitleUIRoot" or "Game.UI.TitleUIRoot".
    public string viewId;

    public List<UIImageBindingEntry> images = new();
}
