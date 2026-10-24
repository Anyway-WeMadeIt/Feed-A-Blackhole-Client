using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Checks View data keys (UIViewKey): no two View types share a key, and every binding asset names a View that exists.
// Duplicate keys are checked after each script reload; the menu also checks the assets.
public static class UIViewKeyCheck
{
    private const string Tag = "[UIViewKey]";

    [InitializeOnLoadMethod]
    private static void OnLoad() => EditorApplication.delayCall += () => ReportDuplicateKeys(silentWhenClean: true);

    [MenuItem("Tools/UI Presentation/Check View Keys")]
    private static void CheckAll()
    {
        bool clean = ReportDuplicateKeys(silentWhenClean: false);
        clean &= ReportUnknownBindingKeys();

        if (clean)
            Debug.Log($"{Tag} View keys and binding assets are consistent.");
    }

    // Concrete View types by key. Logs the keys that two or more types share.
    private static bool ReportDuplicateKeys(bool silentWhenClean)
    {
        var typesByKey = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

        foreach (Type type in TypeCache.GetTypesDerivedFrom<UIBase>())
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            string key = UIViewKey.Of(type);
            if (!typesByKey.TryGetValue(key, out List<Type> types))
                typesByKey[key] = types = new List<Type>();

            types.Add(type);
        }

        bool clean = true;

        foreach (KeyValuePair<string, List<Type>> pair in typesByKey)
        {
            if (pair.Value.Count < 2)
                continue;

            clean = false;
            Debug.LogWarning(
                $"{Tag} Key '{pair.Key}' is shared by {pair.Value.Count} View types: " +
                string.Join(", ", pair.Value.ConvertAll(t => t.FullName)) +
                ". Rename one or pin a key with [UIViewKey].");
        }

        return clean;
    }

    // Every TextBinding / ImageBinding asset must name a key some View type has. A stale id (an old FullName,
    // a renamed class) means the binding is never applied.
    private static bool ReportUnknownBindingKeys()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (Type type in TypeCache.GetTypesDerivedFrom<UIBase>())
        {
            if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                known.Add(UIViewKey.Of(type));
        }

        bool clean = true;

        foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(UITextBindingSet)}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var set = AssetDatabase.LoadAssetAtPath<UITextBindingSet>(path);
            if (set != null && !known.Contains(set.viewId ?? string.Empty))
            {
                clean = false;
                Debug.LogWarning($"{Tag} Text binding '{path}' names unknown View key '{set.viewId}'.", set);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(UIImageBindingSet)}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var set = AssetDatabase.LoadAssetAtPath<UIImageBindingSet>(path);
            if (set != null && !known.Contains(set.viewId ?? string.Empty))
            {
                clean = false;
                Debug.LogWarning($"{Tag} Image binding '{path}' names unknown View key '{set.viewId}'.", set);
            }
        }

        return clean;
    }
}
