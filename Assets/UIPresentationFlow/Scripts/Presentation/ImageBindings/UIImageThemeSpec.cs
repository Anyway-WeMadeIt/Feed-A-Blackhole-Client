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

        string viewId = UIViewKey.Of(viewType);
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

    // sprites/themeId: the global sprite variants, used for an entry that names no sprite of its own.
    public void BuildPatches(List<IUIPatch> patches, UISpriteSet sprites = null, string themeId = null)
    {
        patches.Add(new UIImageThemePatch(this, sprites, themeId));
    }
}
