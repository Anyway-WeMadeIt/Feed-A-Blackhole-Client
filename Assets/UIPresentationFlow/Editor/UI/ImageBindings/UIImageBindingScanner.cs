using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UIImageBindingScanner
{
    [MenuItem("Tools/UI Presentation/Image Bindings/Scan Selected UI")]
    private static void ScanSelectedUI()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[ImageBindingScanner] Select a UI View.");
            return;
        }

        UIBase view = selected.GetComponent<UIBase>();
        if (view == null)
        {
            Debug.LogWarning(
                $"[ImageBindingScanner] '{selected.name}' has no UIBase.",
                selected);
            return;
        }

        Scan(view);
    }

    public static List<UIImageBindingEntry> Scan(UIBase view)
    {
        Type refsType = FindRefsType(view.GetType());

        if (refsType == null)
        {
            Debug.LogError(
                $"[ImageBindingScanner] Refs type not found on '{view.GetType().Name}'.",
                view);
            return new List<UIImageBindingEntry>();
        }

        var entries = new List<UIImageBindingEntry>();

        foreach (string refId in Enum.GetNames(refsType))
        {
            if (!refId.EndsWith("_Image", StringComparison.Ordinal))
                continue;

            FieldInfo field = refsType.GetField(
                refId,
                BindingFlags.Public | BindingFlags.Static);

            if (field?.GetCustomAttribute<UIIgnoreImageBindingAttribute>(inherit: false) != null)
                continue;

            List<GameObject> matches = FindByName(view.transform, refId);

            if (matches.Count == 0)
            {
                Debug.LogWarning(
                    $"[ImageBindingScanner] Missing Image ref '{refId}' on '{view.GetType().Name}'.",
                    view);
                continue;
            }

            if (matches.Count > 1)
            {
                Debug.LogError(
                    $"[ImageBindingScanner] Duplicate ref '{refId}' on '{view.GetType().Name}'.",
                    view);
                continue;
            }

            GameObject target = matches[0];

            if (!target.TryGetComponent(out Image image))
            {
                Debug.LogWarning(
                    $"[ImageBindingScanner] Ref '{refId}' has no Image component.",
                    target);
                continue;
            }

            entries.Add(new UIImageBindingEntry
            {
                refId = refId,
                baseSprite = image.sprite,
                sprite = image.sprite,
                stale = false,
            });
        }

        PrintResult(view, entries);

        return entries;
    }

    private static Type FindRefsType(Type viewType)
    {
        Type current = viewType;

        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(UIBase<>))
            {
                return current.GetGenericArguments()[0];
            }

            current = current.BaseType;
        }

        return null;
    }

    private static List<GameObject> FindByName(
        Transform root,
        string refId)
    {
        var matches = new List<GameObject>();

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == refId)
                matches.Add(child.gameObject);
        }

        return matches;
    }

    private static void PrintResult(
        UIBase view,
        List<UIImageBindingEntry> entries)
    {
        string result = $"[ImageBindingScanner] {view.GetType().Name}";

        foreach (UIImageBindingEntry entry in entries)
        {
            string spriteName = entry.baseSprite != null
                ? entry.baseSprite.name
                : "null";

            result += $"\n- {entry.refId} -> {spriteName}";
        }

        Debug.Log(result, view);
    }
}
