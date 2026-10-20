using System;
using UnityEngine;

public enum AspectRule
{
    Landscape = 0,  // display.Orientation == Landscape
    Portrait,       // display.Orientation == Portrait  (Square matches neither)
    Range,          // aspectMin <= display.AspectRatio <= aspectMax, inclusive
}

// Declarative condition attached to a UIVariantRule.
//
// Pure: every input is a parameter of Matches(). Nothing here reads Screen.*
// or Application.platform — that is UnityDisplayContextProvider's job. The
// same condition evaluated against the same (ui, display) pair always gives
// the same answer, on any machine, without a running player.
//
// "Don't care" is expressed by leaving a field empty (theme/locale/experiment)
// or by the use* toggle being off (platform/aspect). There is no separate
// "Any" enum value on purpose: two ways to say the same thing is one too many.
[Serializable]
public sealed class VariantCondition
{
    [Header("Theme / Locale")]
    public string themeId;    // empty = any
    public string localeId;   // empty = any

    [Header("Experiment")]
    // If set, the variant applies only when the UIContext carries this experiment.
    public ExperimentKey experimentKey;
    // If set (and experimentKey is set), the assigned variant must equal this value.
    public VariantId experimentVariantId;

    [Header("Platform (optional)")]
    public bool usePlatform;
    public DisplayPlatform platform = DisplayPlatform.Desktop;

    [Header("Layout Class (optional)")]
    // Preferred way to target an aspect bucket: thresholds live in
    // DisplayLayoutClassifier, not in every rule.
    public bool useLayoutClass;
    public DisplayLayoutClass layoutClass = DisplayLayoutClass.Standard;

    [Header("Aspect Ratio (optional, advanced)")]
    // Raw numeric rule for cases the class buckets cannot express.
    // If both layout class and aspect are enabled they are AND-ed.
    public bool useAspectRatio;
    public AspectRule aspectRule = AspectRule.Landscape;
    public float aspectMin = 1.5f;   // Range only
    public float aspectMax = 2.5f;   // Range only

    public bool Matches(in UIContext ui, in DisplayContext display)
    {
        if (!string.IsNullOrEmpty(themeId) && ui.ThemeId != themeId)
            return false;

        if (!string.IsNullOrEmpty(localeId) && ui.LocaleId != localeId)
            return false;

        if (experimentKey.IsValid)
        {
            if (ui.Experiments == null)
                return false;

            if (!ui.Experiments.TryGetValue(experimentKey, out VariantId assigned))
                return false;

            if (experimentVariantId.IsValid && assigned != experimentVariantId)
                return false;
        }

        if (usePlatform && display.Platform != platform)
            return false;

        if (useLayoutClass && DisplayLayoutClassifier.Classify(display) != layoutClass)
            return false;

        if (useAspectRatio && !MatchesAspect(display))
            return false;

        return true;
    }

    private bool MatchesAspect(in DisplayContext display)
    {
        switch (aspectRule)
        {
            case AspectRule.Landscape:
                return display.Orientation == DisplayOrientation.Landscape;

            case AspectRule.Portrait:
                return display.Orientation == DisplayOrientation.Portrait;

            case AspectRule.Range:
                return display.AspectRatio >= aspectMin && display.AspectRatio <= aspectMax;

            default:
                return false;
        }
    }
}