using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Texts and images a View styles by code: cloned rows, cells created at runtime, widgets drawn in code.
// They sit beside the View's catalog entries (TextBindings, Image Themes) and get the same theme, fonts,
// scale and sprite variants from the same applier. Each entry keeps its own baseline (the values as
// authored), so re-applying never compounds and removing the theme restores them.
//
// Register as soon as the element exists; if a presentation is already applied, the element is styled
// at once. Remove or Clear when the elements are destroyed (cloned rows), or let Prune drop dead ones.
public sealed class UIStyledElements
{
    public sealed class TextEntry
    {
        public TMP_Text Text { get; }
        public UITextRole Role { get; }
        public UITextSizePolicy SizePolicy { get; }
        internal UIStyledTextBaseline Baseline { get; } = new();

        internal TextEntry(TMP_Text text, UITextRole role, UITextSizePolicy sizePolicy)
        {
            Text = text;
            Role = role;
            SizePolicy = sizePolicy;
        }
    }

    public sealed class ImageEntry
    {
        public Image Image { get; }
        // The sprite follows the theme (sprite set variants). Null = this image's sprite is left alone.
        public bool ThemesSprite { get; }
        // The color follows the palette role. Null = the color is left alone.
        public UIColorRole? ColorRole { get; }
        internal UIStyledImageBaseline Baseline { get; } = new();

        internal ImageEntry(Image image, bool themesSprite, UIColorRole? colorRole)
        {
            Image = image;
            ThemesSprite = themesSprite;
            ColorRole = colorRole;
        }
    }

    private readonly Dictionary<TMP_Text, TextEntry> _texts = new();
    private readonly Dictionary<Image, ImageEntry> _images = new();
    private readonly List<TextEntry> _textList = new();
    private readonly List<ImageEntry> _imageList = new();
    // Set by the applier after a presentation is applied, so late registrations are styled at once.
    private IUIStyledElementsApplier _applier;

    public IReadOnlyList<TextEntry> Texts => _textList;
    public IReadOnlyList<ImageEntry> Images => _imageList;

    public void AddText(TMP_Text text, UITextRole role, UITextSizePolicy sizePolicy = UITextSizePolicy.Role)
    {
        if (text == null || _texts.ContainsKey(text))
            return;

        var entry = new TextEntry(text, role, sizePolicy);
        _texts.Add(text, entry);
        _textList.Add(entry);
        _applier?.Apply(entry);
    }

    // The image's sprite follows the theme (sprite set variants); Sprite Swap states of its Selectable too.
    // An image without a sprite has nothing to theme and is not registered.
    public void AddSprite(Image image)
    {
        if (image != null && image.sprite != null)
            AddImage(image, themesSprite: true, colorRole: null);
    }

    // All images under root (inactive included) follow the theme. For a cloned row or card.
    public void AddSprites(Component root)
    {
        if (root == null)
            return;

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            AddSprite(image);
    }

    // The image's color follows the palette role. The authored alpha is kept.
    public void AddImageColor(Image image, UIColorRole role) => AddImage(image, themesSprite: false, colorRole: role);

    private void AddImage(Image image, bool themesSprite, UIColorRole? colorRole)
    {
        if (image == null)
            return;

        if (_images.TryGetValue(image, out ImageEntry existing))
        {
            // Registering the other aspect of an image already registered: merge into one entry.
            if (existing.ThemesSprite == themesSprite && existing.ColorRole == colorRole)
                return;

            existing.Baseline.Restore(image);
            Remove(image);
            themesSprite |= existing.ThemesSprite;
            colorRole ??= existing.ColorRole;
        }

        var entry = new ImageEntry(image, themesSprite, colorRole);
        _images.Add(image, entry);
        _imageList.Add(entry);
        _applier?.Apply(entry);
    }

    public void Remove(TMP_Text text)
    {
        if (text == null || !_texts.TryGetValue(text, out TextEntry entry))
            return;

        _texts.Remove(text);
        _textList.Remove(entry);
    }

    public void Remove(Image image)
    {
        if (image == null || !_images.TryGetValue(image, out ImageEntry entry))
            return;

        _images.Remove(image);
        _imageList.Remove(entry);
    }

    // Forget every text and image under root (inactive included). For a cloned row or card about to be destroyed.
    public void RemoveUnder(Component root)
    {
        if (root == null)
            return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            Remove(text);

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            Remove(image);
    }

    // Forget every element. Does not restore their values (they are usually being destroyed).
    public void Clear()
    {
        _texts.Clear();
        _images.Clear();
        _textList.Clear();
        _imageList.Clear();
    }

    // Drop entries whose element was destroyed.
    public void Prune()
    {
        for (int i = _textList.Count - 1; i >= 0; i--)
        {
            if (_textList[i].Text == null)
            {
                _texts.Remove(_textList[i].Text);
                _textList.RemoveAt(i);
            }
        }

        for (int i = _imageList.Count - 1; i >= 0; i--)
        {
            if (_imageList[i].Image == null)
            {
                _images.Remove(_imageList[i].Image);
                _imageList.RemoveAt(i);
            }
        }
    }

    // Applier side: put every element back to its baseline before a new presentation is applied.
    internal void RestoreAll()
    {
        Prune();

        foreach (TextEntry entry in _textList)
            entry.Baseline.Restore(entry.Text);

        foreach (ImageEntry entry in _imageList)
            entry.Baseline.Restore(entry.Image);
    }

    internal void SetApplier(IUIStyledElementsApplier applier) => _applier = applier;
}

// Styles one late-registered element with the presentation last applied to its View.
internal interface IUIStyledElementsApplier
{
    void Apply(UIStyledElements.TextEntry entry);
    void Apply(UIStyledElements.ImageEntry entry);
}
