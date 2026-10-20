using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UIImageTheme",
    menuName = "UI/Image Theme")]
public sealed class UIImageThemeSpec : ScriptableObject
{
    public string themeId;

    public List<UIImageBindingSet> bindings = new();

    public bool TryGetBinding(Type viewType, out UIImageBindingSet binding)
    {
        binding = null;

        if (viewType == null)
            return false;

        string viewId = viewType.FullName ?? viewType.Name;
        return TryGetBinding(viewId, out binding);
    }

    public bool TryGetBinding(string viewId, out UIImageBindingSet binding)
    {
        binding = null;

        if (string.IsNullOrWhiteSpace(viewId) || bindings == null)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            UIImageBindingSet candidate = bindings[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.viewId, viewId, StringComparison.Ordinal))
                continue;

            binding = candidate;
            return true;
        }

        return false;
    }

    public void BuildPatches(List<IUIPatch> patches)
    {
        patches.Add(new UIImageThemePatch(this));
    }
}
