using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Values of a styled element as authored, captured the first time a theme touches it. Restore puts them back.
internal sealed class UIStyledTextBaseline
{
    private bool _captured;
    private TMP_FontAsset _font;
    private Color _color;
    private float _fontSize;
    private float _fontSizeMin;
    private float _fontSizeMax;

    public bool Captured => _captured;
    public TMP_FontAsset Font => _font;
    public Color Color => _color;
    public float FontSize => _fontSize;
    public float FontSizeMin => _fontSizeMin;
    public float FontSizeMax => _fontSizeMax;

    public void CaptureOnce(TMP_Text text)
    {
        if (_captured)
            return;

        _font = text.font;
        _color = text.color;
        _fontSize = text.fontSize;
        _fontSizeMin = text.fontSizeMin;
        _fontSizeMax = text.fontSizeMax;
        _captured = true;
    }

    public void Restore(TMP_Text text)
    {
        if (!_captured || text == null)
            return;

        text.font = _font;
        text.color = _color;
        text.fontSize = _fontSize;
        text.fontSizeMin = _fontSizeMin;
        text.fontSizeMax = _fontSizeMax;
    }
}

internal sealed class UIStyledImageBaseline
{
    private bool _captured;
    private Sprite _sprite;
    private Color _color;
    private bool _hasSpriteState;
    private SpriteState _spriteState;

    public bool Captured => _captured;
    public Sprite Sprite => _sprite;
    public Color Color => _color;
    public bool HasSpriteState => _hasSpriteState;
    public SpriteState SpriteState => _spriteState;

    public void CaptureOnce(Image image)
    {
        if (_captured)
            return;

        _sprite = image.sprite;
        _color = image.color;

        Selectable selectable = UIImageThemePatch.SelectableOf(image);
        if (selectable != null)
        {
            _spriteState = selectable.spriteState;
            _hasSpriteState = true;
        }

        _captured = true;
    }

    public void Restore(Image image)
    {
        if (!_captured || image == null)
            return;

        image.sprite = _sprite;
        image.color = _color;

        if (_hasSpriteState)
        {
            Selectable selectable = UIImageThemePatch.SelectableOf(image);
            if (selectable != null)
                selectable.spriteState = _spriteState;
        }
    }
}
