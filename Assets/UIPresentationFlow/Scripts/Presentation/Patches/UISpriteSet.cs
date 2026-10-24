using System;
using System.Collections.Generic;
using UnityEngine;

// Sprite variants by theme: for a base sprite (as authored, usually the light theme), the sprite to show
// in each other theme. Lookup (pure): the entry that holds the sprite as its base or as any variant,
// then the variant for the theme; no variant for that theme = the base sprite.
// Image Themes (per view, per ref) take precedence over this set; this set is the default for every
// image that has a variant, including images created in code.
[CreateAssetMenu(fileName = "UISpriteSet", menuName = "UI/Sprite Set")]
public sealed class UISpriteSet : ScriptableObject
{
    [Serializable]
    public sealed class Variant
    {
        public string themeId;   // e.g. "Dark"
        public Sprite sprite;
    }

    [Serializable]
    public sealed class Entry
    {
        public Sprite baseSprite;
        public Variant[] variants;
    }

    [SerializeField] private List<Entry> _entries = new();

    // sprite (base or any variant) → its entry.
    private Dictionary<Sprite, Entry> _bySprite;

    // The sprite to show for themeId. Unknown sprite or no variant for the theme = the entry's base sprite,
    // or the sprite itself if it is in no entry.
    public Sprite For(Sprite sprite, string themeId)
    {
        if (sprite == null)
            return null;

        Build();

        if (!_bySprite.TryGetValue(sprite, out Entry entry))
            return sprite;

        if (entry.variants != null && themeId != null)
        {
            foreach (Variant variant in entry.variants)
            {
                if (variant != null && variant.sprite != null && string.Equals(variant.themeId, themeId, StringComparison.Ordinal))
                    return variant.sprite;
            }
        }

        return entry.baseSprite;
    }

    private void Build()
    {
        if (_bySprite != null)
            return;

        _bySprite = new Dictionary<Sprite, Entry>();

        foreach (Entry entry in _entries)
        {
            if (entry == null || entry.baseSprite == null)
                continue;

            _bySprite[entry.baseSprite] = entry;

            if (entry.variants == null)
                continue;

            foreach (Variant variant in entry.variants)
            {
                if (variant != null && variant.sprite != null)
                    _bySprite[variant.sprite] = entry;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => _bySprite = null;
#endif
}
