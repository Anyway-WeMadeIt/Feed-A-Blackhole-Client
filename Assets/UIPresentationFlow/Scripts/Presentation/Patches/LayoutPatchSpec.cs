using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class RectTransformPatch
{
    [Header("Anchors")]
    public bool overrideAnchors;
    public Vector2 anchorMin;
    public Vector2 anchorMax;

    [Header("Pivot")]
    public bool overridePivot;
    public Vector2 pivot;

    [Header("Position")]
    public bool overrideAnchoredPosition;
    public Vector2 anchoredPosition;

    [Header("Size")]
    public bool overrideSizeDelta;
    public Vector2 sizeDelta;
}

[Serializable]
public sealed class WidgetLayoutPatch
{
    [Tooltip("Target ref id. Must match a Refs enum member exposed by the screen's IUIPresentationRefProvider.")]
    public string refId;

    [Header("Active")]
    public bool overrideActive;
    public bool active = true;

    [Header("RectTransform")]
    public RectTransformPatch rect = new RectTransformPatch();
}

[CreateAssetMenu(menuName = "UI/LayoutPatchSpec")]
public sealed class LayoutPatchSpec : ScriptableObject
{
    [Tooltip("Layout targets addressed by presentation ref id.")]
    public List<WidgetLayoutPatch> widgets = new();

    //TODO: SafeArea/global layout policy may be added here

    public void BuildPatches(List<IUIPatch> patches)
    {
        patches.Add(new LayoutSpecPatch(this));
    }
}
