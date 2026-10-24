using TMPro;
using UnityEngine;

public sealed class ThemeSpecPatch : IUIPatch
{
    private readonly ThemeSpec _theme;
    private readonly UITextBindingCatalog _textBindings;
    private readonly UIFontSet _fonts;
    private readonly float _textScale;
    private readonly string _localeId;
    // Text colors from the palette row of the current theme. Null without a palette: colors stay as authored.
    private readonly UIPalette.ThemeColors _colors;

    public ThemeSpecPatch(
        ThemeSpec theme,
        UITextBindingCatalog textBindings,
        UIThemeAssets assets,
        float textScale = 1f,
        string localeId = null,
        string themeId = null)
    {
        _theme = theme;
        _textBindings = textBindings;
        _fonts = assets?.Fonts;
        _textScale = textScale > 0f ? textScale : 1f;
        _localeId = localeId;
        _colors = assets?.Palette != null ? assets.Palette.Get(themeId) : null;
    }

    public void Apply(IUIPresentationRefProvider refs)
    {
        if (_theme == null
            || _textBindings == null
            || refs == null)
        {
            return;
        }

        if (!_textBindings.TryGetBinding(
                refs.GetType(),
                out UITextBindingSet binding)
            || binding?.texts == null)
        {
            return;
        }

        string targetName = refs.GetType().Name;

        foreach (UITextBindingEntry entry in binding.texts)
        {
            if (entry == null
                || entry.stale
                || entry.role == UITextRole.Unassigned)
            {
                continue;
            }

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            if (!refs.TryGetText(refId, out TMP_Text text) || text == null)
            {
                Debug.LogWarning(
                    $"[ThemeSpecPatch] TMP_Text not found for text refId='{refId}' on '{targetName}'.");
                continue;
            }

            ApplyTextTheme(text, entry.role, entry.fixedSize ? 1f : _textScale, !entry.fixedFont);
        }
    }

    // The size is always computed from the role size, so re-applying never compounds the scale.
    // Auto-sized texts take the size as their maximum: they grow with the scale and shrink to fit their rect.
    // The font comes from the font set by the current locale and the role (fixedFont entries keep theirs).
    // The color is the palette's text color for the role (Caption = weak); without a palette it is left alone.
    private void ApplyTextTheme(TMP_Text text, UITextRole role, float scale, bool setFont)
    {
        if (setFont && _fonts != null)
        {
            TMP_FontAsset font = _fonts.Get(_localeId, role);
            if (font != null)
                text.font = font;
        }

        SetSize(text, _theme.SizeOf(role) * scale);

        if (_colors != null)
            text.color = role == UITextRole.Caption ? _colors.textWeak : _colors.textMain;
    }

    private static void SetSize(TMP_Text text, float size)
    {
        if (text.enableAutoSizing)
            text.fontSizeMax = size;

        text.fontSize = size;
    }
}
