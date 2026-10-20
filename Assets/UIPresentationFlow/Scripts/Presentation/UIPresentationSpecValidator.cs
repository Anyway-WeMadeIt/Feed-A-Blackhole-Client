using System;
using System.Collections.Generic;

public static class UIPresentationSpecValidator
{
    public static List<string> Validate(
        UIPresentationSpec spec,
        string context = null)
    {
        var problems = new List<string>();
        string prefix = string.IsNullOrEmpty(context)
            ? string.Empty
            : context + ": ";

        if (spec == null)
        {
            problems.Add(prefix + "presentation is null");
            return problems;
        }

        if (string.IsNullOrWhiteSpace(spec.presentationId))
            problems.Add(prefix + "presentationId is empty");

        AppendImageThemeProblems(
            problems,
            spec.baseImageTheme,
            prefix + "baseImageTheme");

        AppendTextBindingProblems(
            problems,
            spec.textBindings,
            prefix + "textBindings");

        if (spec.variants == null)
            return problems;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < spec.variants.Length; i++)
        {
            UIVariantRule rule = spec.variants[i];
            string at = $"{prefix}variants[{i}]";

            if (rule == null)
            {
                problems.Add($"{at}: null rule");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.variantId))
                problems.Add($"{at}: variantId is empty");
            else if (!seenIds.Add(rule.variantId))
                problems.Add($"{at}: duplicate variantId '{rule.variantId}' (forced override would be ambiguous)");

            AppendImageThemeProblems(
                problems,
                rule.overrideImageTheme,
                $"{at}.overrideImageTheme");

            if (rule.condition == null)
            {
                problems.Add($"{at} '{rule.variantId}': condition is null (rule can never match)");
                continue;
            }

            VariantCondition c = rule.condition;
            if (c.useAspectRatio
                && c.aspectRule == AspectRule.Range
                && c.aspectMin > c.aspectMax)
            {
                problems.Add(
                    $"{at} '{rule.variantId}': aspectMin {c.aspectMin} > aspectMax {c.aspectMax}");
            }
        }

        return problems;
    }

    private static void AppendImageThemeProblems(
        List<string> problems,
        UIImageThemeSpec theme,
        string context)
    {
        if (theme == null)
            return;

        problems.AddRange(
            UIImageThemeSpecValidator.Validate(theme, context));
    }

    private static void AppendTextBindingProblems(
        List<string> problems,
        UITextBindingCatalog catalog,
        string context)
    {
        if (catalog == null)
            return;

        problems.AddRange(
            UITextBindingCatalogValidator.Validate(catalog, context));
    }
}
