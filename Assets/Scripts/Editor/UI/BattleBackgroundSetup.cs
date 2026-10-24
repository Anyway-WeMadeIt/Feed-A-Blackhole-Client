using System;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.EditorTools
{
    public sealed class BattleTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            // 처음 임포트할 때만 설정한다(BlackHoleGUIImporter와 같다). 임포트마다 설정을 다시 쓰면 Unity가 .meta를 다시 저장하고,
            // 그때 설치된 플랫폼(예: WebGL) 항목이 붙어 .meta가 계속 바뀐 것으로 뜬다. 이미 있는 텍스처는 EnsureAssets(설치기)가 설정한다.
            if (!assetImporter.importSettingsMissing)
                return;

            if (Array.IndexOf(BattleBackgroundSetup.TexturePaths, assetPath) >= 0)
                BattleBackgroundSetup.ConfigureTexture((TextureImporter)assetImporter, assetPath);
        }
    }

    // 전투 배경의 에셋 준비와 배경 오브젝트 생성. 메뉴는 없고 BattleBackgroundInstaller(Install in Open Scene)가 부른다.
    // 손으로 세팅하는 방법은 Tools/BattleBackground/MANUAL.md.
    public static class BattleBackgroundSetup
    {
        private const string Backgrounds = "Assets/Art/Backgrounds/";
        private const string Vfx = "Assets/VFX/Background/";
        private const string ShaderPath = Vfx + "SG_UIBattleCelestial.shadergraph";

        // 이 코드가 임포트 설정을 맞추는 텍스처: 정적 배경 그림 2장과 셰이더 데이터 맵 2장.
        internal static readonly string[] TexturePaths =
        {
            Backgrounds + "BG_BattleWhite.png",
            Backgrounds + "BG_BattleBlack.png",
            Vfx + "Battle_Masks.png",
            Vfx + "Battle_Flow.png",
        };

        internal static void ConfigureTexture(TextureImporter importer, string path)
        {
            bool data = path.EndsWith("Battle_Masks.png") || path.EndsWith("Battle_Flow.png");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !data;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = data ? 1024 : 2048;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !data;
            importer.isReadable = false;
        }

        internal static bool EnsureAssets()
        {
            // Apply explicitly too: textures may have imported before the new editor script compiled.
            foreach (string path in TexturePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                ConfigureTexture(importer, path);
                importer.SaveAndReimport();
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogError("Battle background shader did not compile. Check the first Console error. Target: Unity 6000.6 / URP 17.6 Canvas Shader Graph.");
                return false;
            }
            var masks = LoadTexture("Battle_Masks");
            var flow = LoadTexture("Battle_Flow");
            if (masks == null || flow == null || LoadTexture("BG_BattleWhite") == null || LoadTexture("BG_BattleBlack") == null)
            {
                Debug.LogError("Missing battle background textures. Expected: " + string.Join(", ", TexturePaths));
                return false;
            }
            foreach (bool dark in new[] { false, true })
            {
                string path = MaterialPath(dark);
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                var material = new Material(shader);
                material.SetTexture("_Masks", masks);
                material.SetTexture("_Flow", flow);
                material.SetFloat("_LineOpacity", dark ? 0.035f : 0.045f);
                material.SetFloat("_StarOpacity", dark ? 0.18f : 0.14f);
                material.SetFloat("_FlowOpacity", dark ? 0.32f : 0.24f);
                material.SetColor("_LineColor", dark ? new Color(0.34f, 0.40f, 0.49f) : new Color(0.36f, 0.42f, 0.49f));
                material.SetColor("_StarColor", dark ? new Color(0.70f, 0.78f, 0.88f) : new Color(0.42f, 0.50f, 0.60f));
                AssetDatabase.CreateAsset(material, path);
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        // Builds Background + Ambient as the first child of parent. Call EnsureAssets() first.
        internal static BattleBackgroundDriver Build(Transform parent, bool dark)
        {
            var root = new GameObject("BattleBackground_" + (dark ? "Black" : "White"), typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create Battle Background");
            root.transform.SetParent(parent, false);
            root.transform.SetAsFirstSibling();
            Stretch((RectTransform)root.transform);
            var bg = Image("Background", root.transform);
            bg.texture = LoadTexture(dark ? "BG_BattleBlack" : "BG_BattleWhite");
            var overlay = Image("Ambient", root.transform);
            overlay.texture = Texture2D.whiteTexture;
            overlay.material = LoadMaterial(dark);
            var driver = root.AddComponent<BattleBackgroundDriver>();
            driver.Configure(bg, overlay);
            return driver;
        }

        internal static string MaterialPath(bool dark) => Vfx + "M_Battle" + (dark ? "Black" : "White") + ".mat";
        internal static Material LoadMaterial(bool dark) => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(dark));
        internal static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(name));
        private static string TexturePath(string name) =>
            (name == "Battle_Masks" || name == "Battle_Flow" ? Vfx : Backgrounds) + name + ".png";
        private static RawImage Image(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
