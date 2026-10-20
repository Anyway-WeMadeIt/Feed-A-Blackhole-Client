using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UITextBindingBuilder
{
    private const string RootPath = "Assets/UI/TextBindings";
    private const string CatalogPath = RootPath + "/UI.TextBindings.asset";

    public static UITextBindingSet Build(UIBase view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        List<UITextBindingEntry> scanned =
            UITextBindingScanner.Scan(view);

        string viewName = view.GetType().Name;
        string viewId = view.GetType().FullName ?? viewName;
        string viewPath = $"{RootPath}/{viewName}";
        string assetPath =
            $"{viewPath}/{viewName}.TextBinding.asset";

        EnsureFolder(RootPath);
        EnsureFolder(viewPath);

        UITextBindingCatalog catalog = GetOrCreateCatalog();

        UITextBindingSet binding =
            AssetDatabase.LoadAssetAtPath<UITextBindingSet>(assetPath);

        bool created = binding == null;

        if (created)
        {
            binding = ScriptableObject.CreateInstance<UITextBindingSet>();
            binding.viewId = viewId;
            binding.texts = scanned;
            AssetDatabase.CreateAsset(binding, assetPath);
        }
        else
        {
            SyncBinding(binding, viewId, scanned);
        }

        SetBinding(catalog, binding);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[TextBindingBuilder] {(created ? "Created" : "Synced")} " +
            $"'{viewName}' with {scanned.Count} active text binding(s).",
            binding);

        return binding;
    }

    private static void SyncBinding(
        UITextBindingSet binding,
        string viewId,
        List<UITextBindingEntry> scanned)
    {
        binding.viewId = viewId;

        List<UITextBindingEntry> oldEntries =
            binding.texts ?? new List<UITextBindingEntry>();

        var oldById = new Dictionary<string, UITextBindingEntry>(StringComparer.Ordinal);

        foreach (UITextBindingEntry entry in oldEntries)
        {
            if (entry == null)
                continue;

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId) || oldById.ContainsKey(refId))
                continue;

            oldById.Add(refId, entry);
        }

        var synced = new List<UITextBindingEntry>(oldEntries.Count + scanned.Count);
        var scannedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (UITextBindingEntry next in scanned)
        {
            string refId = (next.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            scannedIds.Add(refId);

            if (!oldById.TryGetValue(refId, out UITextBindingEntry current))
            {
                next.stale = false;
                synced.Add(next);
                continue;
            }

            current.refId = refId;
            current.stale = false;
            synced.Add(current);
        }

        var staleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (UITextBindingEntry old in oldEntries)
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

        binding.texts = synced;
        EditorUtility.SetDirty(binding);
    }

    private static UITextBindingCatalog GetOrCreateCatalog()
    {
        UITextBindingCatalog catalog =
            AssetDatabase.LoadAssetAtPath<UITextBindingCatalog>(CatalogPath);

        if (catalog != null)
            return catalog;

        catalog = ScriptableObject.CreateInstance<UITextBindingCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static void SetBinding(
        UITextBindingCatalog catalog,
        UITextBindingSet binding)
    {
        catalog.bindings ??= new List<UITextBindingSet>();

        for (int i = 0; i < catalog.bindings.Count; i++)
        {
            UITextBindingSet current = catalog.bindings[i];

            if (current == binding)
                return;

            if (current == null
                || !string.Equals(current.viewId, binding.viewId, StringComparison.Ordinal))
            {
                continue;
            }

            catalog.bindings[i] = binding;
            EditorUtility.SetDirty(catalog);
            return;
        }

        catalog.bindings.Add(binding);
        EditorUtility.SetDirty(catalog);
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
