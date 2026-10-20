using System;
using System.Collections.Generic;
using UnityEngine;

// The pure decision step:
//   (presentation, ui, display) -> ResolvedUIPresentation
//
// Contract
//   - never mutates authored data or either context
//   - reads no Unity global state; same inputs -> same output
//   - requires an explicit, valid DisplayContext
//   - explains itself through the optional UIResolveTrace
//
// Priority policy: highest priority wins, per presentation field.
// Rules are visited in priority-descending order; ties keep authored order.
//
// Forced override:
//   UIContext.PresentationOverrides[presentationId] = variantId
// Debug/QA may force one authored variant without evaluating its condition.
public sealed class UIVariantResolver
{
    public ResolvedUIPresentation Resolve(
        UIPresentationSpec spec,
        in UIContext ui,
        in DisplayContext display,
        UIResolveTrace trace = null)
    {
        ThemeSpec theme = spec.baseTheme;
        LayoutPatchSpec layout = spec.baseLayout;
        UIImageThemeSpec imageTheme = spec.baseImageTheme;
        List<string> matchedRules = new(4);

        trace?.Add(
            $"[Resolve] presentation={spec.presentationId} " +
            $"base theme={Name(theme)} layout={Name(layout)} imageTheme={Name(imageTheme)}");
        trace?.Add(
            $"[Input] ui theme={ui.ThemeId} locale={ui.LocaleId} " +
            $"experiments={ui.Experiments?.Count ?? 0} " +
            $"overrides={ui.PresentationOverrides?.Count ?? 0}");
        trace?.Add($"[Input] display {display}");

        UIVariantRule[] rules = spec.variants;

        // 1) forced override
        if (TryGetForcedVariantId(spec, ui, out string forcedId))
        {
            UIVariantRule forced = FindFirstById(rules, forcedId);
            if (forced != null)
            {
                matchedRules.Add(forced.variantId);

                if (forced.overrideTheme != null)
                    theme = forced.overrideTheme;

                if (forced.overrideLayout != null)
                    layout = forced.overrideLayout;

                if (forced.overrideImageTheme != null)
                    imageTheme = forced.overrideImageTheme;

                trace?.Add($"[Forced] variantId={forcedId} applied; rule conditions skipped");
                trace?.Add(ResultLine(theme, layout, imageTheme, matchedRules));

                return new ResolvedUIPresentation(
                    spec,
                    theme,
                    layout,
                    imageTheme,
                    matchedRules);
            }

            trace?.Add($"[Forced] variantId={forcedId} not found in presentation; evaluating rules normally");
        }

        // 2) normal evaluation
        if (rules != null && rules.Length > 0)
        {
            bool themeLocked = false;
            bool layoutLocked = false;
            bool imageThemeLocked = false;

            foreach ((UIVariantRule rule, int index) in OrderByPriority(rules))
            {
                if (rule.condition == null)
                {
                    trace?.Add($"[Rule] {rule.variantId} p{rule.priority} SKIP (null condition, index {index})");
                    continue;
                }

                bool match = rule.condition.Matches(ui, display);
                trace?.Add($"[Rule] {rule.variantId} p{rule.priority} {(match ? "MATCH" : "MISS")}");
                if (!match)
                    continue;

                matchedRules.Add(rule.variantId);

                if (!themeLocked && rule.overrideTheme != null)
                {
                    theme = rule.overrideTheme;
                    themeLocked = true;
                    trace?.Add($"[Winner] theme <- {rule.variantId} ({Name(theme)})");
                }

                if (!layoutLocked && rule.overrideLayout != null)
                {
                    layout = rule.overrideLayout;
                    layoutLocked = true;
                    trace?.Add($"[Winner] layout <- {rule.variantId} ({Name(layout)})");
                }

                if (!imageThemeLocked && rule.overrideImageTheme != null)
                {
                    imageTheme = rule.overrideImageTheme;
                    imageThemeLocked = true;
                    trace?.Add($"[Winner] imageTheme <- {rule.variantId} ({Name(imageTheme)})");
                }
            }
        }

        trace?.Add(ResultLine(theme, layout, imageTheme, matchedRules));

        return new ResolvedUIPresentation(
            spec,
            theme,
            layout,
            imageTheme,
            matchedRules);
    }

    private static List<(UIVariantRule rule, int index)> OrderByPriority(UIVariantRule[] rules)
    {
        var ordered = new List<(UIVariantRule rule, int index)>(rules.Length);
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i] != null)
                ordered.Add((rules[i], i));
        }

        ordered.Sort((a, b) =>
            a.rule.priority != b.rule.priority
                ? b.rule.priority.CompareTo(a.rule.priority)
                : a.index.CompareTo(b.index));

        return ordered;
    }

    private static bool TryGetForcedVariantId(
        UIPresentationSpec spec,
        in UIContext ui,
        out string variantId)
    {
        variantId = null;

        if (ui.PresentationOverrides == null || string.IsNullOrWhiteSpace(spec.presentationId))
            return false;

        if (!ui.PresentationOverrides.TryGetValue(spec.presentationId, out VariantId id) || !id.IsValid)
            return false;

        variantId = id.Value;
        return true;
    }

    private static UIVariantRule FindFirstById(UIVariantRule[] rules, string variantId)
    {
        if (rules == null)
            return null;

        for (int i = 0; i < rules.Length; i++)
        {
            UIVariantRule rule = rules[i];
            if (rule != null && rule.variantId == variantId)
                return rule;
        }

        return null;
    }

    private static string ResultLine(
        ThemeSpec theme,
        LayoutPatchSpec layout,
        UIImageThemeSpec imageTheme,
        List<string> applied)
    {
        return $"[Result] theme={Name(theme)} layout={Name(layout)} " +
               $"imageTheme={Name(imageTheme)} applied=[{string.Join(", ", applied)}]";
    }

    private static string Name(UnityEngine.Object value)
        => value != null ? value.name : "null";
}
