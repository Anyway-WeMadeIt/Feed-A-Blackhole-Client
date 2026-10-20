using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UI.TextBindings",
    menuName = "UI/Text Binding Catalog")]
public sealed class UITextBindingCatalog : ScriptableObject
{
    public List<UITextBindingSet> bindings = new();

    public bool TryGetBinding(Type viewType, out UITextBindingSet binding)
    {
        binding = null;

        if (viewType == null)
            return false;

        string viewId = viewType.FullName ?? viewType.Name;
        return TryGetBinding(viewId, out binding);
    }

    public bool TryGetBinding(string viewId, out UITextBindingSet binding)
    {
        binding = null;

        if (string.IsNullOrWhiteSpace(viewId) || bindings == null)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            UITextBindingSet candidate = bindings[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.viewId, viewId, StringComparison.Ordinal))
                continue;

            binding = candidate;
            return true;
        }

        return false;
    }
}
