using System;
using UnityEngine;

// 한 시점의 Display 상태를 찍어둔 Snapshot.
// 잘못된 해상도나 Safe Area로는 생성 불가.
public readonly struct DisplayContext : IEquatable<DisplayContext>
{
    public Vector2Int Resolution { get; }

    // Pixel rect with bottom-left origin.
    public Rect SafeAreaPixels { get; }

    public DisplayPlatform Platform { get; }

    public DisplayContext(Vector2Int resolution, Rect safeAreaPixels, DisplayPlatform platform)
    {
        if (resolution.x <= 0 || resolution.y <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(resolution), resolution,
                "Resolution must be positive on both axes.");

        if (!IsRectWithin(safeAreaPixels, resolution))
            throw new ArgumentOutOfRangeException(
                nameof(safeAreaPixels), safeAreaPixels,
                $"Safe area must be finite, non-negative in size, and lie within {resolution.x}x{resolution.y}.");

        Resolution     = resolution;
        SafeAreaPixels = safeAreaPixels;
        Platform       = platform;
    }

    public static DisplayContext FullScreen(int width, int height, DisplayPlatform platform = DisplayPlatform.Unknown)
        => new DisplayContext(new Vector2Int(width, height), new Rect(0, 0, width, height), platform);

    private bool IsValid => Resolution.x > 0 && Resolution.y > 0;

    public float AspectRatio => IsValid ? (float)Resolution.x / Resolution.y : 0f;

    public DisplayOrientation Orientation
    {
        get
        {
            if (Resolution.x > Resolution.y) return DisplayOrientation.Landscape;
            if (Resolution.x < Resolution.y) return DisplayOrientation.Portrait;
            return DisplayOrientation.Square;
        }
    }

    // Safe area as a fraction of the resolution (0..1 on both axes).
    // Directly usable as RectTransform anchorMin/anchorMax.
    public Rect SafeAreaNormalized
    {
        get
        {
            if (!IsValid) return default;
            float w = Resolution.x;
            float h = Resolution.y;
            return new Rect(
                SafeAreaPixels.x      / w,
                SafeAreaPixels.y      / h,
                SafeAreaPixels.width  / w,
                SafeAreaPixels.height / h);
        }
    }

    // True when the safe area is smaller than the full resolution.
    public bool HasSafeAreaInsets
        => IsValid && SafeAreaPixels != new Rect(0, 0, Resolution.x, Resolution.y);

    private static bool IsRectWithin(Rect r, Vector2Int res)
    {
        if (!IsFinite(r.x) || !IsFinite(r.y) || !IsFinite(r.width) || !IsFinite(r.height))
            return false;

        return r.width  >= 0f && r.height >= 0f &&
               r.xMin   >= 0f && r.yMin   >= 0f &&
               r.xMax   <= res.x && r.yMax <= res.y;
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    public bool Equals(DisplayContext other)
        => Resolution == other.Resolution
        && SafeAreaPixels == other.SafeAreaPixels
        && Platform == other.Platform;

    public override bool Equals(object obj) => obj is DisplayContext other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = Resolution.GetHashCode();
            h = (h * 397) ^ SafeAreaPixels.GetHashCode();
            h = (h * 397) ^ (int)Platform;
            return h;
        }
    }

    public static bool operator ==(DisplayContext a, DisplayContext b) => a.Equals(b);
    public static bool operator !=(DisplayContext a, DisplayContext b) => !a.Equals(b);
}