using System.Collections.Generic;
using UnityEngine;

public sealed class UITextBindingSet : ScriptableObject
{
    // The View's key (UIViewKey.Of: type name, or [UIViewKey]).
    public string viewId;

    public List<UITextBindingEntry> texts = new();
}
