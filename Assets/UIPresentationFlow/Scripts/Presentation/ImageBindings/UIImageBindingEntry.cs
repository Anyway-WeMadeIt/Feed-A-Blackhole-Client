using System;
using UnityEngine;

[Serializable]
public sealed class UIImageBindingEntry
{
    public string refId;

    // Last authored Sprite observed on the UI View.
    // Sync uses this to distinguish an untouched Theme slot from an override.
    public Sprite baseSprite;

    // Sprite applied by this Image Theme.
    public Sprite sprite;

    // Optional: state sprites for a Selectable (Button) on the same object that uses Sprite Swap.
    // Empty = that state keeps its authored sprite. A theme whose normal sprite differs needs these too,
    // or a press flashes the authored (other theme's) sprite.
    public Sprite highlightedSprite;
    public Sprite pressedSprite;
    public Sprite disabledSprite;

    // The Ref no longer exists on the scanned View.
    // Kept for review instead of being deleted automatically.
    public bool stale;

    public bool HasStateSprites => highlightedSprite != null || pressedSprite != null || disabledSprite != null;
}
