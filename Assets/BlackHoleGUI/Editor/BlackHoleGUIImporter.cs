#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// BlackHoleGUI/PNG 안의 PNG를 처음 들여올 때(.meta가 없을 때)만 스프라이트 설정을 넣는다.
// 이미 .meta가 있는 PNG를 다시 임포트할 때 설정을 건드리면 Unity가 .meta를 다시 쓰면서
// 그 PC에 깔린 빌드 모듈의 플랫폼 항목(WebGL 등)을 덧붙인다 — 팀원마다 .meta 변경이 생기는 원인이었다.
public sealed class BlackHoleGUIImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing) return;
        if (!assetPath.Contains("/BlackHoleGUI/PNG/") || !assetPath.EndsWith(".png")) return;
        var texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Sprite;
        texture.spriteImportMode = SpriteImportMode.Single;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.sRGBTexture = true;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.maxTextureSize = 1024;
        texture.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings();
        texture.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        texture.SetTextureSettings(settings);
        int border = 0;
        if (assetPath.Contains("/Buttons/rect_") || assetPath.EndsWith("/tooltip.png") || assetPath.Contains("/slider_track.png") || assetPath.Contains("/slider_fill.png")) border = 24;
        if (assetPath.EndsWith("/panel.png")) border = 28;
        // Settlement 막대·행 배경. 경계 16px.
        if (assetPath.Contains("/Settlement/Components/")) border = 16;
        texture.spriteBorder = new Vector4(border, border, border, border);
    }
}
#endif
