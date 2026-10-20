using UnityEditor;
using UnityEngine;

public sealed class UIImageBindingBuilderWizard
    : ScriptableWizard
{
    public string themeId = "Theme1";

    [MenuItem(
        "Tools/UI Presentation/Image Bindings/Create or Sync Binding")]
    private static void Open()
    {
        DisplayWizard<UIImageBindingBuilderWizard>(
            "Create or Sync Image Binding",
            "Create / Sync");
    }

    private void OnWizardCreate()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning(
                "[ImageBindingBuilder] Select a UI View.");
            return;
        }

        UIBase view = selected.GetComponent<UIBase>();

        if (view == null)
        {
            Debug.LogWarning(
                $"[ImageBindingBuilder] '{selected.name}' has no UIBase.",
                selected);
            return;
        }

        UIImageBindingSet binding =
            UIImageBindingBuilder.Build(
                view,
                themeId);

        Selection.activeObject = binding;
        EditorGUIUtility.PingObject(binding);
    }
}
