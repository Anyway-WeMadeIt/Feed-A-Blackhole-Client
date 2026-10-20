using System.Collections.Generic;
using UnityEngine;

public sealed class UITextBindingSet : ScriptableObject
{
    // Concrete UI View type FullName.
    public string viewId;

    public List<UITextBindingEntry> texts = new();
}
