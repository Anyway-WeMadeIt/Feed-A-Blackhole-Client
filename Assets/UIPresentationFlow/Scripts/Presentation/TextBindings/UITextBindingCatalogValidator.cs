using System;
using System.Collections.Generic;

public static class UITextBindingCatalogValidator
{
    public static List<string> Validate(
        UITextBindingCatalog catalog,
        string context = null)
    {
        var problems = new List<string>();
        string prefix = string.IsNullOrEmpty(context)
            ? string.Empty
            : context + ": ";

        if (catalog == null)
        {
            problems.Add(prefix + "text binding catalog is null");
            return problems;
        }

        if (catalog.bindings == null)
        {
            problems.Add(prefix + "bindings is null");
            return problems;
        }

        var seenViewIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < catalog.bindings.Count; i++)
        {
            UITextBindingSet binding = catalog.bindings[i];
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

            if (binding.texts == null)
            {
                problems.Add($"{at}: texts is null");
                continue;
            }

            var seenRefIds = new HashSet<string>(StringComparer.Ordinal);

            for (int j = 0; j < binding.texts.Count; j++)
            {
                UITextBindingEntry entry = binding.texts[j];
                string textAt = $"{at}.texts[{j}]";

                if (entry == null)
                {
                    problems.Add($"{textAt}: null entry");
                    continue;
                }

                string refId = (entry.refId ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(refId))
                    problems.Add($"{textAt}: refId is empty");
                else if (!seenRefIds.Add(refId))
                    problems.Add($"{textAt}: duplicate refId '{refId}'");

                if (entry.stale)
                    problems.Add($"{textAt}: stale refId '{refId}'");

                if (entry.role == UITextRole.Unassigned)
                    problems.Add($"{textAt}: role is Unassigned for '{refId}'");
            }
        }

        return problems;
    }
}
