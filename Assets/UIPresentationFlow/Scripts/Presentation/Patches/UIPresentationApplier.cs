using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIPresentationApplier
{
    private readonly Dictionary<UIBase, UIPresentationBaseline> _baselines = new();

    public void Apply(UIBase view, UIResolveResult result)
    {
        UIPresentationBaseline baseline = GetBaseline(view);

        baseline.RestoreOwned(view);
        // Code-registered elements keep their own baselines; put them back before the patches run.
        view.Styled.RestoreAll();
        baseline.CaptureAndOwn(view, result.Resolved, result.Assets);

        foreach (IUIPatch patch in result.Patches)
            patch?.Apply(view);

        view.NotifyPresented(result.Theme);
    }

    private UIPresentationBaseline GetBaseline(UIBase view)
    {
        if (_baselines.TryGetValue(view, out UIPresentationBaseline baseline))
            return baseline;

        baseline = new UIPresentationBaseline();
        _baselines.Add(view, baseline);
        return baseline;
    }

    private sealed class UIPresentationBaseline
    {
        private readonly Dictionary<string, RectBaseline> _rects = new();
        private readonly Dictionary<string, TextBaseline> _texts = new();
        private readonly Dictionary<string, ImageBaseline> _images = new();

        public void RestoreOwned(IUIPresentationRefProvider refs)
        {
            foreach (KeyValuePair<string, RectBaseline> pair in _rects)
            {
                if (refs.TryGetRect(pair.Key, out RectTransform rect) && rect != null)
                    pair.Value.RestoreOwned(rect);
                else
                    pair.Value.ReleaseOwnership();
            }

            foreach (KeyValuePair<string, TextBaseline> pair in _texts)
            {
                if (refs.TryGetText(pair.Key, out TMP_Text text) && text != null)
                    pair.Value.RestoreOwned(text);
                else
                    pair.Value.ReleaseOwnership();
            }

            foreach (KeyValuePair<string, ImageBaseline> pair in _images)
            {
                if (refs.TryGetImage(pair.Key, out Image image) && image != null)
                    pair.Value.RestoreOwned(image);
                else
                    pair.Value.ReleaseOwnership();
            }
        }

        public void CaptureAndOwn(
            IUIPresentationRefProvider refs,
            ResolvedUIPresentation resolved,
            UIThemeAssets assets)
        {
            CaptureLayout(refs, resolved?.Layout);
            CaptureTheme(
                refs,
                resolved?.Theme,
                resolved?.TextBindings,
                assets);
            CaptureImageTheme(refs, resolved?.ImageTheme);
        }

        private void CaptureLayout(
            IUIPresentationRefProvider refs,
            LayoutPatchSpec layout)
        {
            if (layout?.widgets == null)
                return;

            foreach (WidgetLayoutPatch widget in layout.widgets)
            {
                if (widget == null)
                    continue;

                string refId = (widget.refId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(refId))
                    continue;

                if (!refs.TryGetRect(refId, out RectTransform rect) || rect == null)
                    continue;

                if (!_rects.TryGetValue(refId, out RectBaseline baseline))
                {
                    baseline = new RectBaseline();
                    _rects.Add(refId, baseline);
                }

                baseline.CaptureAndOwn(rect, widget);
            }
        }

        private void CaptureTheme(
            IUIPresentationRefProvider refs,
            ThemeSpec theme,
            UITextBindingCatalog textBindings,
            UIThemeAssets assets)
        {
            if (theme == null
                || textBindings == null
                || !textBindings.TryGetBinding(
                    refs.GetType(),
                    out UITextBindingSet binding)
                || binding?.texts == null)
            {
                return;
            }

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
                    continue;

                if (!_texts.TryGetValue(refId, out TextBaseline baseline))
                {
                    baseline = new TextBaseline();
                    _texts.Add(refId, baseline);
                }

                baseline.CaptureAndOwn(text, assets?.Fonts != null && !entry.fixedFont);
            }
        }

        private void CaptureImageTheme(
            IUIPresentationRefProvider refs,
            UIImageThemeSpec imageTheme)
        {
            if (imageTheme == null
                || !imageTheme.TryGetBinding(
                    refs.GetType(),
                    out UIImageBindingSet binding)
                || binding?.images == null)
            {
                return;
            }

            foreach (UIImageBindingEntry entry in binding.images)
            {
                if (entry == null || entry.stale)
                    continue;

                string refId = (entry.refId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(refId))
                    continue;

                if (!refs.TryGetImage(refId, out Image image) || image == null)
                    continue;

                if (!_images.TryGetValue(refId, out ImageBaseline baseline))
                {
                    baseline = new ImageBaseline();
                    _images.Add(refId, baseline);
                }

                baseline.CaptureAndOwn(image);
            }
        }
    }

    private sealed class RectBaseline
    {
        private bool _hasActive;
        private bool _active;
        private bool _ownsActive;

        private bool _hasAnchors;
        private Vector2 _anchorMin;
        private Vector2 _anchorMax;
        private bool _ownsAnchors;

        private bool _hasPivot;
        private Vector2 _pivot;
        private bool _ownsPivot;

        private bool _hasAnchoredPosition;
        private Vector2 _anchoredPosition;
        private bool _ownsAnchoredPosition;

        private bool _hasSizeDelta;
        private Vector2 _sizeDelta;
        private bool _ownsSizeDelta;

        public void CaptureAndOwn(RectTransform rect, WidgetLayoutPatch patch)
        {
            if (patch.overrideActive)
            {
                if (!_hasActive)
                {
                    _active = rect.gameObject.activeSelf;
                    _hasActive = true;
                }

                _ownsActive = true;
            }

            RectTransformPatch rectPatch = patch.rect;
            if (rectPatch == null)
                return;

            if (rectPatch.overrideAnchors)
            {
                if (!_hasAnchors)
                {
                    _anchorMin = rect.anchorMin;
                    _anchorMax = rect.anchorMax;
                    _hasAnchors = true;
                }

                _ownsAnchors = true;
            }

            if (rectPatch.overridePivot)
            {
                if (!_hasPivot)
                {
                    _pivot = rect.pivot;
                    _hasPivot = true;
                }

                _ownsPivot = true;
            }

            if (rectPatch.overrideAnchoredPosition)
            {
                if (!_hasAnchoredPosition)
                {
                    _anchoredPosition = rect.anchoredPosition;
                    _hasAnchoredPosition = true;
                }

                _ownsAnchoredPosition = true;
            }

            if (rectPatch.overrideSizeDelta)
            {
                if (!_hasSizeDelta)
                {
                    _sizeDelta = rect.sizeDelta;
                    _hasSizeDelta = true;
                }

                _ownsSizeDelta = true;
            }
        }

        public void RestoreOwned(RectTransform rect)
        {
            if (_ownsAnchors && _hasAnchors)
            {
                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
            }

            if (_ownsPivot && _hasPivot)
                rect.pivot = _pivot;

            if (_ownsAnchoredPosition && _hasAnchoredPosition)
                rect.anchoredPosition = _anchoredPosition;

            if (_ownsSizeDelta && _hasSizeDelta)
                rect.sizeDelta = _sizeDelta;

            if (_ownsActive && _hasActive)
                rect.gameObject.SetActive(_active);

            ReleaseOwnership();
        }

        public void ReleaseOwnership()
        {
            _ownsActive = false;
            _ownsAnchors = false;
            _ownsPivot = false;
            _ownsAnchoredPosition = false;
            _ownsSizeDelta = false;
        }
    }

    private sealed class TextBaseline
    {
        private bool _hasFont;
        private TMP_FontAsset _font;
        private bool _ownsFont;

        private bool _hasFontSize;
        private float _fontSize;
        private float _fontSizeMax;
        private bool _ownsFontSize;

        private bool _hasColor;
        private Color _color;
        private bool _ownsColor;

        // ownsFont: the theme sets this text's font (it has a font set and the entry is not fixedFont).
        public void CaptureAndOwn(TMP_Text text, bool ownsFont)
        {
            if (ownsFont)
            {
                if (!_hasFont)
                {
                    _font = text.font;
                    _hasFont = true;
                }

                _ownsFont = true;
            }

            if (!_hasFontSize)
            {
                _fontSize = text.fontSize;
                _fontSizeMax = text.fontSizeMax;
                _hasFontSize = true;
            }

            _ownsFontSize = true;

            if (!_hasColor)
            {
                _color = text.color;
                _hasColor = true;
            }

            _ownsColor = true;
        }

        public void RestoreOwned(TMP_Text text)
        {
            if (_ownsFont && _hasFont)
                text.font = _font;

            if (_ownsFontSize && _hasFontSize)
            {
                text.fontSize = _fontSize;
                text.fontSizeMax = _fontSizeMax;
            }

            if (_ownsColor && _hasColor)
                text.color = _color;

            ReleaseOwnership();
        }

        public void ReleaseOwnership()
        {
            _ownsFont = false;
            _ownsFontSize = false;
            _ownsColor = false;
        }
    }

    private sealed class ImageBaseline
    {
        private bool _hasSprite;
        private Sprite _sprite;
        private bool _ownsSprite;

        // Sprite Swap states of a Selectable on the same object (a theme may replace them with the sprite).
        private bool _hasSpriteState;
        private SpriteState _spriteState;

        public void CaptureAndOwn(Image image)
        {
            if (!_hasSprite)
            {
                _sprite = image.sprite;
                _hasSprite = true;

                Selectable selectable = UIImageThemePatch.SelectableOf(image);
                if (selectable != null)
                {
                    _spriteState = selectable.spriteState;
                    _hasSpriteState = true;
                }
            }

            _ownsSprite = true;
        }

        public void RestoreOwned(Image image)
        {
            if (_ownsSprite && _hasSprite)
            {
                image.sprite = _sprite;

                if (_hasSpriteState)
                {
                    Selectable selectable = UIImageThemePatch.SelectableOf(image);
                    if (selectable != null)
                        selectable.spriteState = _spriteState;
                }
            }

            ReleaseOwnership();
        }

        public void ReleaseOwnership()
        {
            _ownsSprite = false;
        }
    }
}
