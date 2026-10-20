using System.IO;
using BlackHole.ContentExport;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 서버용 콘텐츠 내보내기(계약 1장, W0-1): BlackHole > Server Content > Export...
    // 지금 콘텐츠(GameContentSetup과 노드 시트·배치)를 서버 저장소 형식의 묶음으로 내보낸다.
    // 내보낸 폴더(서버 저장소의 src/main/resources/content)를 그대로 커밋하면 서버가 같은 콘텐츠와 같은 버전을 쓴다.
    //
    // 게임이 불러올 수 없는 콘텐츠(검사 실패)는 내보내지 않는다.
    internal static class ServerContentExport
    {
        private const string FolderKey = "BlackHole.ServerContentExport.Folder";
        private const string ServerContentPath = "../Feed-A-Blackhole-Server/src/main/resources/content";

        [MenuItem("BlackHole/Server Content/Export...")]
        private static void ExportMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("서버 콘텐츠를 내보낼 폴더(서버 저장소의 src/main/resources/content)",
                DefaultFolder(), "");

            if (string.IsNullOrEmpty(folder))
                return;

            EditorPrefs.SetString(FolderKey, folder);
            ServerContentBundle bundle = ExportTo(folder);

            if (bundle != null)
            {
                EditorUtility.DisplayDialog("서버 콘텐츠 내보내기",
                    $"내보냈다.\n\n콘텐츠 버전: {bundle.ContentVersion}\n폴더: {folder}\n\n서버 저장소에서 바뀐 파일을 커밋한다.", "확인");
            }
        }

        [MenuItem("BlackHole/Server Content/Log Content Version")]
        private static void LogVersionMenu()
        {
            ServerContentBundle bundle = Build();

            if (bundle != null)
                Debug.Log($"[서버 콘텐츠] 지금 콘텐츠의 버전: {bundle.ContentVersion}");
        }

        // folder 아래에 묶음 파일과 목차를 쓴다. 목차는 마지막에 쓴다. 실패하면 null(이유는 콘솔에 있다).
        public static ServerContentBundle ExportTo(string folder)
        {
            ServerContentBundle bundle = Build();

            if (bundle == null)
                return null;

            foreach (BundleFile file in bundle.Files)
                Write(folder, file);

            Write(folder, bundle.Manifest);
            Debug.Log($"[서버 콘텐츠] 내보냈다. 버전 {bundle.ContentVersion}, 파일 {bundle.Files.Count + 1}개 → {folder}");
            return bundle;
        }

        // 지금 콘텐츠의 묶음(메모리). 콘텐츠가 검사를 통과하지 못하면 null.
        public static ServerContentBundle Build()
        {
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                Debug.LogError("[서버 콘텐츠] 게임 콘텐츠 세트(GameContentSetup)를 찾지 못했다.");
                return null;
            }

            // 게임과 같은 검사를 거친다. 실패하면 이유가 콘솔에 남는다.
            if (GameContentLoader.Load(setup) == null)
            {
                Debug.LogError("[서버 콘텐츠] 콘텐츠가 검사를 통과하지 못해 내보내지 않는다. 위의 오류를 본다.", setup);
                return null;
            }

            // 게임 실행 중의 버전 계산(LoadedContent.ServerContentVersion)과 같은 입력을 쓴다.
            return ServerContentExporter.Export(ServerContent.SourceOf(setup));
        }

        private static void Write(string folder, BundleFile file)
        {
            string path = Path.Combine(folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, file.Bytes);
        }

        // 지난번 폴더, 없으면 클라이언트 저장소 옆의 서버 저장소.
        private static string DefaultFolder()
        {
            string saved = EditorPrefs.GetString(FolderKey, "");

            if (Directory.Exists(saved))
                return saved;

            string sibling = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ServerContentPath));
            return Directory.Exists(sibling) ? sibling : Application.dataPath;
        }

        private static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
