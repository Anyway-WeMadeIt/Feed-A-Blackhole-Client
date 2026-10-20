using System;
using System.Collections.Generic;

public static class UIImageThemeSpecValidator
{
    public static List<string> Validate(
        UIImageThemeSpec theme,
        string context = null)
    {
        var problems = new List<string>();
        string prefix = string.IsNullOrEmpty(context)
            ? string.Empty
            : context + ": ";

        if (theme == null)
        {
            problems.Add(prefix + "image theme is null");
            return problems;
        }

        if (string.IsNullOrWhiteSpace(theme.themeId))
            problems.Add(prefix + "themeId is empty");

        if (theme.bindings == null)
        {
            problems.Add(prefix + "bindings is null");
            return problems;
        }

        var seenViewIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < theme.bindings.Count; i++)
        {
            UIImageBindingSet binding = theme.bindings[i];
            string at = $"{prefix}bindings[{i}]";

            if (binding == null)
            {
                problems.Add($"{at}: null binding");
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.viewId))
                problems.Add($"{at}: viewId is empty");
            else if (!seenViewIds.Add(binding.viewId))
                problems.Add($"{at}: duplicate viewId '{binding.viewId}'");

            if (binding.images == null)
            {
                problems.Add($"{at}: images is null");
                continue;
            }

            var seenRefIds = new HashSet<string>(StringComparer.Ordinal);

            for (int j = 0; j < binding.images.Count; j++)
            {
                UIImageBindingEntry entry = binding.images[j];
                string imageAt = $"{at}.images[{j}]";

                if (entry == null)
                {
                    problems.Add($"{imageAt}: null entry");
                    continue;
                }

                string refId = (entry.refId ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(refId))
                    problems.Add($"{imageAt}: refId is empty");
                else if (!seenRefIds.Add(refId))
                    problems.Add($"{imageAt}: duplicate refId '{refId}'");

                if (entry.stale)
                    problems.Add($"{imageAt}: stale refId '{refId}'");
            }
        }

        return problems;
    }
}
