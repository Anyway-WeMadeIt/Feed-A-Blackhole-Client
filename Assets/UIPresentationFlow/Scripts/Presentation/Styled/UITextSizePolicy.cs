// How a styled text's size follows the user text scale.
public enum UITextSizePolicy
{
    // The theme's role size × scale. Same typography across screens. Default for catalog entries.
    Role,
    // The authored (prefab or code) size × scale. Keeps a design's own proportions. Auto-sized texts scale min and max.
    Prefab,
}
