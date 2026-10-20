using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UIImageBindingBuilder
{
    private const string RootPath = "Assets/UI/ImageBindings";

    public static UIImageBindingSet Build(
        UIBase view,
        string themeId)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        if (string.IsNullOrWhiteSpace(themeId))
            throw new ArgumentException(
                "Theme id is required.",
                nameof(themeId));

        themeId = themeId.Trim();

        List<UIImageBindingEntry> scanned =
            UIImageBindingScanner.Scan(view);

        string viewName = view.GetType().Name;
        string viewId = view.GetType().FullName ?? viewName;
        string themePath = $"{RootPath}/{themeId}";
        string viewPath = $"{themePath}/{viewName}";
        string assetPath =
            $"{viewPath}/{viewName}.ImageBinding.asset";

        EnsureFolder(RootPath);
        EnsureFolder(themePath);
        EnsureFolder(viewPath);

        UIImageThemeSpec theme =
            GetOrCreateTheme(themeId, themePath);

        UIImageBindingSet binding =
            AssetDatabase.LoadAssetAtPath<UIImageBindingSet>(assetPath);

        bool created = binding == null;

        if (created)
        {
            binding = ScriptableObject.CreateInstance<UIImageBindingSet>();
            binding.viewId = viewId;
            binding.images = scanned;

            AssetDatabase.CreateAsset(binding, assetPath);
        }
        else
        {
            SyncBinding(binding, viewId, scanned);
        }

        SetBinding(theme, binding);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[ImageBindingBuilder] {(created ? "Created" : "Synced")} " +
            $"'{themeId}/{viewName}' with {scanned.Count} active image binding(s).",
            binding);

        return binding;
    }

    private static void SyncBinding(
        UIImageBindingSet binding,
        string viewId,
        List<UIImageBindingEntry> scanned)
    {
        binding.viewId = viewId;

        List<UIImageBindingEntry> oldEntries =
            binding.images ?? new List<UIImageBindingEntry>();

        var oldById = new Dictionary<string, UIImageBindingEntry>(StringComparer.Ordinal);

        foreach (UIImageBindingEntry entry in oldEntries)
        {
            if (entry == null)
                continue;

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId) || oldById.ContainsKey(refId))
                continue;

            oldById.Add(refId, entry);
        }

        var synced = new List<UIImageBindingEntry>(oldEntries.Count + scanned.Count);
        var scannedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (UIImageBindingEntry next in scanned)
        {
            string refId = (next.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            scannedIds.Add(refId);

            if (!oldById.TryGetValue(refId, out UIImageBindingEntry current))
            {
                next.stale = false;
                synced.Add(next);
                continue;
            }

            bool followsBase = current.sprite == current.baseSprite;

            current.refId = refId;
            current.baseSprite = next.baseSprite;
            current.stale = false;

            if (followsBase)
                current.sprite = next.baseSprite;

            synced.Add(current);
        }

        var staleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (UIImageBindingEntry old in oldEntries)
        {
            if (old == null)
                continue;

            string refId = (old.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId)
                || scannedIds.Contains(refId)
                || !staleIds.Add(refId))
            {
                continue;
            }

            old.stale = true;
            synced.Add(old);
        }

        binding.images = synced;
        EditorUtility.SetDirty(binding);
    }

    private static UIImageThemeSpec GetOrCreateTheme(
        string themeId,
        string themePath)
    {
        string assetPath =
            $"{themePath}/{themeId}.ImageTheme.asset";

        UIImageThemeSpec theme =
            AssetDatabase.LoadAssetAtPath<UIImageThemeSpec>(assetPath);

        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UIImageThemeSpec>();
            theme.themeId = themeId;
            AssetDatabase.CreateAsset(theme, assetPath);
            return theme;
        }

        if (theme.themeId != themeId)
        {
            theme.themeId = themeId;
            EditorUtility.SetDirty(theme);
        }

        return theme;
    }

    private static void SetBinding(
        UIImageThemeSpec theme,
        UIImageBindingSet binding)
    {
        theme.bindings ??= new List<UIImageBindingSet>();

        for (int i = 0; i < theme.bindings.Count; i++)
        {
            UIImageBindingSet current = theme.bindings[i];

            if (current == binding)
                return;

            if (current == null
                || !string.Equals(current.viewId, binding.viewId, StringComparison.Ordinal))
            {
                continue;
            }

            theme.bindings[i] = binding;
            EditorUtility.SetDirty(theme);
            return;
        }

        theme.bindings.Add(binding);
        EditorUtility.SetDirty(theme);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');
        if (split <= 0)
            throw new InvalidOperationException($"Invalid asset folder path: {path}");

        string parent = path.Substring(0, split);
        string folderName = path.Substring(split + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
