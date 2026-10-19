using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.CelestialBattle.Editor
{
    public sealed class BattleTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            // 처음 임포트할 때만 설정한다(BlackHoleGUIImporter와 같다). 임포트마다 설정을 다시 쓰면 Unity가 .meta를 다시 저장하고,
            // 그때 설치된 플랫폼(예: WebGL) 항목이 붙어 .meta가 계속 바뀐 것으로 뜬다. 이미 있는 텍스처는 EnsureAssets(메뉴)가 설정한다.
            if (!assetImporter.importSettingsMissing)
                return;

            if (assetPath.StartsWith(BattleBackgroundSetup.Root + "/Textures/"))
                BattleBackgroundSetup.ConfigureTexture((TextureImporter)assetImporter, assetPath);
        }
    }

    public static class BattleBackgroundSetup
    {
        public const string Root = "Assets/BlackHoleCelestialBattle";
        private const string Menu = "Tools/Black Hole/Battle Background/";

        [MenuItem(Menu + "Create White - UI Canvas")]
        private static void CreateWhite() => Create(false);
        [MenuItem(Menu + "Create Black - UI Canvas")]
        private static void CreateBlack() => Create(true);
        [MenuItem(Menu + "Create White - World Background")]
        private static void CreateWorldWhite() => Create(false, true);
        [MenuItem(Menu + "Create Black - World Background")]
        private static void CreateWorldBlack() => Create(true, true);
        [MenuItem(Menu + "Selected Background - White")]
        private static void SwitchWhite() => SwitchTheme(false);
        [MenuItem(Menu + "Selected Background - Black")]
        private static void SwitchBlack() => SwitchTheme(true);

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
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                ConfigureTexture(importer, path);
                importer.SaveAndReimport();
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/SG_UIBattleCelestial.shadergraph");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogError("Battle background shader did not compile. Check the first Console error. Target: Unity 6000.6 / URP 17.6 Canvas Shader Graph.");
                return false;
            }
            var masks = LoadTexture("Battle_Masks");
            var flow = LoadTexture("Battle_Flow");
            if (masks == null || flow == null || LoadTexture("BG_BattleWhite") == null || LoadTexture("BG_BattleBlack") == null)
            {
                Debug.LogError("Missing battle background textures. Copy the full Assets/BlackHoleCelestialBattle folder.");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
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

        private static void Create(bool dark, bool worldBackground = false)
        {
            Camera worldCamera = worldBackground ? Camera.main : null;
            if (worldBackground && worldCamera == null)
            {
                Debug.LogError("World Background requires your gameplay camera to have the MainCamera tag.");
                return;
            }
            Transform parent = worldBackground ? null : Selection.activeTransform;
            if (parent != null && (!(parent is RectTransform) || parent.GetComponentInParent<Canvas>() == null))
            {
                Debug.LogError("Select a Canvas or a screen RectTransform inside a Canvas.");
                return;
            }
            if (!EnsureAssets()) return;
            if (parent == null)
            {
                var canvas = new GameObject("Battle Background Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Undo.RegisterCreatedObjectUndo(canvas, "Create Battle Background Canvas");
                var uiCanvas = canvas.GetComponent<Canvas>();
                if (worldBackground)
                {
                    canvas.name = "Battle World Background Canvas";
                    uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    uiCanvas.worldCamera = worldCamera;
                    uiCanvas.planeDistance = Mathf.Lerp(worldCamera.nearClipPlane, worldCamera.farClipPlane, 0.9f);
                    uiCanvas.sortingLayerID = SortingLayer.NameToID("Default");
                    uiCanvas.sortingOrder = -1000;
                }
                else uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                parent = canvas.transform;
            }
            Selection.activeGameObject = Build(parent, dark).gameObject;
            Debug.Log("Background created. Place it behind your world objects and UI; Play to verify motion. See README for Canvas/camera ordering.");
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

        private static void SwitchTheme(bool dark)
        {
            var driver = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<BattleBackgroundDriver>() : null;
            if (driver == null)
            {
                Debug.LogError("Select a generated BattleBackground or its child.");
                return;
            }
            if (!EnsureAssets()) return;
            Undo.RegisterFullObjectHierarchyUndo(driver.gameObject, "Change Battle Background Theme");
            driver.SetTheme(LoadTexture(dark ? "BG_BattleBlack" : "BG_BattleWhite"), LoadMaterial(dark));
            EditorUtility.SetDirty(driver);
        }

        internal static string MaterialPath(bool dark) => Root + "/Materials/M_Battle" + (dark ? "Black" : "White") + ".mat";
        internal static Material LoadMaterial(bool dark) => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(dark));
        internal static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");
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
