using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class UITextBindingScanner
{
    [MenuItem("Tools/UI Presentation/Text Bindings/Scan Selected UI")]
    private static void ScanSelectedUI()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[TextBindingScanner] Select a UI View.");
            return;
        }

        UIBase view = selected.GetComponent<UIBase>();
        if (view == null)
        {
            Debug.LogWarning(
                $"[TextBindingScanner] '{selected.name}' has no UIBase.",
                selected);
            return;
        }

        Scan(view);
    }

    public static List<UITextBindingEntry> Scan(UIBase view)
    {
        Type refsType = FindRefsType(view.GetType());

        if (refsType == null)
        {
            Debug.LogError(
                $"[TextBindingScanner] Refs type not found on '{view.GetType().Name}'.",
                view);
            return new List<UITextBindingEntry>();
        }

        var entries = new List<UITextBindingEntry>();

        foreach (string refId in Enum.GetNames(refsType))
        {
            if (!refId.EndsWith("_Text", StringComparison.Ordinal))
                continue;

            List<GameObject> matches = FindByName(view.transform, refId);

            if (matches.Count == 0)
            {
                Debug.LogWarning(
                    $"[TextBindingScanner] Missing Text ref '{refId}' on '{view.GetType().Name}'.",
                    view);
                continue;
            }

            if (matches.Count > 1)
            {
                Debug.LogError(
                    $"[TextBindingScanner] Duplicate ref '{refId}' on '{view.GetType().Name}'.",
                    view);
                continue;
            }

            GameObject target = matches[0];

            if (!target.TryGetComponent(out TMP_Text _))
            {
                Debug.LogWarning(
                    $"[TextBindingScanner] Ref '{refId}' has no TMP_Text component.",
                    target);
                continue;
            }

            entries.Add(new UITextBindingEntry
            {
                refId = refId,
                role = UITextRole.Unassigned,
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
        List<UITextBindingEntry> entries)
    {
        string result = $"[TextBindingScanner] {view.GetType().Name}";

        foreach (UITextBindingEntry entry in entries)
            result += $"\n- {entry.refId}";

        Debug.Log(result, view);
    }
}
