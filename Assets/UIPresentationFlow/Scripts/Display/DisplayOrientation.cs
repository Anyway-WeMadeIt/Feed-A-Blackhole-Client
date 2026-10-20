// Geometric orientation of the current viewport, derived from resolution.
// Not UnityEngine.ScreenOrientation, which also carries auto-rotation state
// that presentation rules do not need.
public enum DisplayOrientation
{
    Landscape = 0,  // width >  height
    Portrait,       // width <  height
    Square,         // width == height
}
