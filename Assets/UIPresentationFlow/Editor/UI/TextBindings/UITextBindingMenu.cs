using UnityEditor;
using UnityEngine;

public static class UITextBindingMenu
{
    [MenuItem(
        "Tools/UI Presentation/Text Bindings/Create or Sync Selected UI")]
    private static void CreateOrSyncSelectedUI()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[TextBindingBuilder] Select a UI View.");
            return;
        }

        UIBase view = selected.GetComponent<UIBase>();

        if (view == null)
        {
            Debug.LogWarning(
                $"[TextBindingBuilder] '{selected.name}' has no UIBase.",
                selected);
            return;
        }

        UITextBindingSet binding = UITextBindingBuilder.Build(view);
        Selection.activeObject = binding;
        EditorGUIUtility.PingObject(binding);
    }

    [MenuItem(
        "Tools/UI Presentation/Text Bindings/Validate Catalog")]
    private static void ValidateCatalog()
    {
        UITextBindingCatalog catalog = Selection.activeObject as UITextBindingCatalog;

        if (catalog == null)
        {
            Debug.LogWarning(
                "[TextBindingValidator] Select a UITextBindingCatalog asset.");
            return;
        }

        var problems = UITextBindingCatalogValidator.Validate(
            catalog,
            catalog.name);

        if (problems.Count == 0)
        {
            Debug.Log(
                $"[TextBindingValidator] '{catalog.name}' is valid.",
                catalog);
            return;
        }

        Debug.LogWarning(
            $"[TextBindingValidator] '{catalog.name}' has {problems.Count} problem(s):\n- " +
            string.Join("\n- ", problems),
            catalog);
    }
}
