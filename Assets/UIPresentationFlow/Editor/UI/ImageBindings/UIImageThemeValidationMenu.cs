using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UIImageThemeValidationMenu
{
    [MenuItem(
        "Tools/UI Presentation/Image Bindings/Validate Selected Theme")]
    private static void ValidateSelectedTheme()
    {
        UIImageThemeSpec theme = Selection.activeObject as UIImageThemeSpec;

        if (theme == null)
        {
            Debug.LogWarning(
                "[UIImageThemeValidator] Select a UIImageThemeSpec asset.");
            return;
        }

        List<string> problems =
            UIImageThemeSpecValidator.Validate(theme, theme.name);

        if (problems.Count == 0)
        {
            Debug.Log(
                $"[UIImageThemeValidator] '{theme.name}' is valid.",
                theme);
            return;
        }

        Debug.LogWarning(
            $"[UIImageThemeValidator] '{theme.name}' has {problems.Count} problem(s):\n- " +
            string.Join("\n- ", problems),
            theme);
    }
}
