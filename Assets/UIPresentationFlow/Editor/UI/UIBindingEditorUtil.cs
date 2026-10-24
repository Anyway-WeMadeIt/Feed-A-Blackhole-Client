using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Shared pieces of the binding editor tools (text and image scanners and builders).
public static class UIBindingEditorUtil
{
    // Where the builders put their assets. One place to change.
    public const string TextBindingsRoot = "Assets/UI/TextBindings";
    public const string TextBindingCatalogPath = TextBindingsRoot + "/UI.TextBindings.asset";
    public const string ImageBindingsRoot = "Assets/UI/ImageBindings";

    // Every child (inactive included) whose GameObject name equals refId. The scanners report more than one as an error.
    public static List<GameObject> FindByName(Transform root, string refId)
    {
        var matches = new List<GameObject>();

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == refId)
                matches.Add(child.gameObject);
        }

        return matches;
    }

    // Creates the asset folder and its parents if missing.
    public static void EnsureFolder(string path)
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
