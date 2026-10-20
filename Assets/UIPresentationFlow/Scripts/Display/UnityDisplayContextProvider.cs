using UnityEngine;

// The runtime boundary for Unity's global display state.
// Presentation rules use the captured DisplayContext instead of reading Screen.
public static class UnityDisplayContextProvider
{
    public static DisplayContext GetCurrent()
    {
        int width  = Screen.width;
        int height = Screen.height;

        Vector2Int resolution = new(width, height);
        Rect safeArea  = ClampToResolution(Screen.safeArea, resolution);

        return new DisplayContext(resolution, safeArea, MapPlatform(Application.platform));
    }

    public static Rect ClampToResolution(Rect rect, Vector2Int resolution)
    {
        if (!IsFinite(rect.x) || !IsFinite(rect.y) || !IsFinite(rect.width) || !IsFinite(rect.height))
            return new Rect(0, 0, resolution.x, resolution.y);

        float xMin = Mathf.Clamp(rect.xMin, 0f, resolution.x);
        float yMin = Mathf.Clamp(rect.yMin, 0f, resolution.y);
        float xMax = Mathf.Clamp(rect.xMax, xMin, resolution.x);
        float yMax = Mathf.Clamp(rect.yMax, yMin, resolution.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    public static DisplayPlatform MapPlatform(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer:
                return DisplayPlatform.Desktop;

            case RuntimePlatform.Android:
            case RuntimePlatform.IPhonePlayer:
                return DisplayPlatform.Mobile;

            case RuntimePlatform.PS4:
            case RuntimePlatform.PS5:
            case RuntimePlatform.XboxOne:
            case RuntimePlatform.GameCoreXboxOne:
            case RuntimePlatform.GameCoreXboxSeries:
            case RuntimePlatform.Switch:
                return DisplayPlatform.Console;

            default:
                return DisplayPlatform.Unknown;
        }
    }
}