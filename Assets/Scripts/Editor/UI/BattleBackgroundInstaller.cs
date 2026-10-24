using BlackHole.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackHole.EditorTools
{
    // 열린 씬(GameScene)의 전투 배경을 Celestial Battle Kit 배경으로 바꾸는 메뉴. 한 번 실행하고 씬을 저장하면 된다.
    // - 씬의 "BattleBackground" Canvas(Screen Space - Camera) 아래에 키트 배경(Background + Ambient)을 첫 자식으로 만든다.
    // - 이전 배경 그림(Background_Image)은 지우지 않고 끈다.
    // - 배경에 BattleBackgroundTheme을 붙여 White·Black 그림과 머티리얼을 연결하고, GameBootstrap에 연결한다.
    // 다시 실행해도 이미 만든 배경을 다시 연결할 뿐 새로 만들지 않는다.
    internal static class BattleBackgroundInstaller
    {
        private const string CanvasName = "BattleBackground";
        private const string OldImageName = "Background_Image";
        private const string BackgroundName = "CelestialBackground";

        [MenuItem("Tools/Black Hole/Battle Background/Install in Open Scene")]
        private static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            Canvas canvas = FindCanvas(scene);

            if (canvas == null)
            {
                Debug.LogError($"[{nameof(BattleBackgroundInstaller)}] 열린 씬에 '{CanvasName}' Canvas가 없다. GameScene을 열고 다시 실행한다.");
                return;
            }

            if (!BattleBackgroundSetup.EnsureAssets())
                return;

            BattleBackgroundDriver driver = canvas.GetComponentInChildren<BattleBackgroundDriver>(true);
            if (driver == null)
                driver = BattleBackgroundSetup.Build(canvas.transform, false);

            GameObject root = driver.gameObject;
            Undo.RecordObject(root, "Install Battle Background");
            root.name = BackgroundName;

            HideOldImage(canvas.transform);

            if (!root.TryGetComponent(out BattleBackgroundTheme theme))
                theme = Undo.AddComponent<BattleBackgroundTheme>(root);

            ConnectTheme(theme, driver);
            ConnectBootstrap(scene, theme);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[{nameof(BattleBackgroundInstaller)}] 전투 배경을 설치했다. 씬을 저장한 뒤 Play에서 설정의 Dark Mode를 바꿔 확인한다.", root);
        }

        private static void HideOldImage(Transform canvas)
        {
            Transform old = canvas.Find(OldImageName);
            if (old == null || !old.gameObject.activeSelf)
                return;

            Undo.RecordObject(old.gameObject, "Hide Old Battle Background");
            old.gameObject.SetActive(false);
        }

        private static void ConnectTheme(BattleBackgroundTheme theme, BattleBackgroundDriver driver)
        {
            var serialized = new SerializedObject(theme);
            serialized.FindProperty("_driver").objectReferenceValue = driver;
            SetLook(serialized.FindProperty("_light"), false);
            SetLook(serialized.FindProperty("_dark"), true);
            serialized.ApplyModifiedProperties();
        }

        private static void SetLook(SerializedProperty look, bool dark)
        {
            look.FindPropertyRelative("Background").objectReferenceValue =
                BattleBackgroundSetup.LoadTexture(dark ? "BG_BattleBlack" : "BG_BattleWhite");
            look.FindPropertyRelative("Ambient").objectReferenceValue = BattleBackgroundSetup.LoadMaterial(dark);
        }

        private static void ConnectBootstrap(Scene scene, BattleBackgroundTheme theme)
        {
            GameBootstrap bootstrap = FindInScene<GameBootstrap>(scene);
            if (bootstrap == null)
            {
                Debug.LogWarning($"[{nameof(BattleBackgroundInstaller)}] 씬에 GameBootstrap이 없어 배경 테마를 설정에 연결하지 못했다.");
                return;
            }

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_battleBackground").objectReferenceValue = theme;
            serialized.ApplyModifiedProperties();
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.name == CanvasName)
                        return canvas;
                }
            }

            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
