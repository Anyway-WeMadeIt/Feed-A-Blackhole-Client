using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UIImageBindingFolderSync
{
    [MenuItem(
        "Tools/UI Presentation/Image Bindings/Sync Selected Theme Folder")]
    private static void SyncSelectedThemeFolder()
    {
        UIImageThemeSpec theme = Selection.activeObject as UIImageThemeSpec;

        if (theme == null)
        {
            Debug.LogWarning(
                "[ImageBindingFolderSync] Select a UIImageThemeSpec asset.");
            return;
        }

        Sync(theme);
    }

    public static int Sync(UIImageThemeSpec theme)
    {
        if (theme == null || theme.bindings == null)
            return 0;

        int applied = 0;

        foreach (UIImageBindingSet binding in theme.bindings)
        {
            if (binding == null || binding.images == null)
                continue;

            string bindingPath = AssetDatabase.GetAssetPath(binding);
            string folderPath = Path.GetDirectoryName(bindingPath)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(folderPath))
                continue;

            Dictionary<string, List<Sprite>> spritesByName =
                CollectSprites(folderPath);

            bool dirty = false;

            foreach (UIImageBindingEntry entry in binding.images)
            {
                if (entry == null || entry.stale)
                    continue;

                string refId = (entry.refId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(refId))
                    continue;

                if (!spritesByName.TryGetValue(refId, out List<Sprite> matches))
                    continue;

                if (matches.Count != 1)
                {
                    Debug.LogWarning(
                        $"[ImageBindingFolderSync] '{binding.name}/{refId}' has {matches.Count} exact Sprite matches. Skipped.",
                        binding);
                    continue;
                }

                Sprite sprite = matches[0];
                if (entry.sprite == sprite)
                    continue;

                entry.sprite = sprite;
                dirty = true;
                applied++;
            }

            if (dirty)
                EditorUtility.SetDirty(binding);
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[ImageBindingFolderSync] Applied {applied} Sprite binding(s) for theme '{theme.themeId}'.",
            theme);

        return applied;
    }

    private static Dictionary<string, List<Sprite>> CollectSprites(
        string folderPath)
    {
        var result = new Dictionary<string, List<Sprite>>(StringComparer.Ordinal);
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is not Sprite sprite)
                    continue;

                if (!result.TryGetValue(sprite.name, out List<Sprite> matches))
                {
                    matches = new List<Sprite>();
                    result.Add(sprite.name, matches);
                }

                if (!matches.Contains(sprite))
                    matches.Add(sprite);
            }
        }

        return result;
    }
}
