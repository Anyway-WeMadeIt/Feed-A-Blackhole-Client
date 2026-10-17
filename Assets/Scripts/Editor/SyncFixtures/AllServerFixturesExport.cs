using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using BlackHole.ContentExport;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 서버 저장소로 보내는 파일을 한 번에 내보낸다(계약 C42): BlackHole > Sync Fixtures > Export All Server Fixtures...
    // 콘텐츠 묶음(W0-1), 기대값 표(W4-1a), 캠페인 로그(W4-2a)를 같은 콘텐츠로 순서대로 내보내고, 세 파일의 콘텐츠 버전이 같은지 확인한다.
    // 밸런스를 바꿀 때 일부만 다시 내보내 버전이 어긋나는 일을 막는다.
    //
    // 서버 저장소의 파일이 바뀐다: src/main/resources/content(묶음), src/test/resources/fixtures(표, 캠페인 로그).
    internal static class AllServerFixturesExport
    {
        private const string FolderKey = "BlackHole.AllServerFixturesExport.Root";
        private const string ServerRootPath = "../Feed-A-Blackhole-Server";
        private const string ContentPath = "src/main/resources/content";
        private const string FixturesPath = "src/test/resources/fixtures";

        private static readonly Regex VersionPattern = new("\"contentVersion\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);

        [MenuItem("BlackHole/Sync Fixtures/Export All Server Fixtures...")]
        private static void ExportMenu() => ExportFromMenu(compress: false);

        [MenuItem("BlackHole/Sync Fixtures/Export All Server Fixtures (gzip campaign)...")]
        private static void ExportGzipMenu() => ExportFromMenu(compress: true);

        private static void ExportFromMenu(bool compress)
        {
            string root = EditorUtility.OpenFolderPanel("서버 저장소 루트(Feed-A-Blackhole-Server)", DefaultRoot(), "");

            if (string.IsNullOrEmpty(root))
                return;

            if (!EditorUtility.DisplayDialog("서버 픽스처 내보내기",
                    $"서버 저장소의 파일을 바꾼다.\n\n- {ContentPath}\n- {FixturesPath}\n\n{root}\n\n계속할까?", "내보내기", "취소"))
                return;

            EditorPrefs.SetString(FolderKey, root);
            string report = ExportTo(root, compress);
            EditorUtility.DisplayDialog("서버 픽스처 내보내기", report ?? "실패했다. 콘솔의 오류를 본다.", "확인");
        }

        // 서버 저장소 루트 아래에 세 파일을 내보내고 요약을 돌려준다. 하나라도 실패하거나 버전이 어긋나면 null(이유는 콘솔에 있다).
        public static string ExportTo(string serverRoot, bool compressCampaign = false)
        {
            string contentFolder = Path.Combine(serverRoot, ContentPath.Replace('/', Path.DirectorySeparatorChar));
            string fixturesFolder = Path.Combine(serverRoot, FixturesPath.Replace('/', Path.DirectorySeparatorChar));

            ServerContentBundle bundle = ServerContentExport.ExportTo(contentFolder);

            if (bundle == null)
                return Fail("콘텐츠 묶음");

            string tablePath = RulesTableExport.ExportTo(fixturesFolder);

            if (tablePath == null)
                return Fail("기대값 표");

            string campaignPath = CampaignLogExport.ExportTo(fixturesFolder, compressCampaign);

            if (campaignPath == null)
                return Fail("캠페인 로그");

            string tableVersion = VersionOf(File.ReadAllText(tablePath, Encoding.UTF8));
            string campaignVersion = VersionOf(ReadText(campaignPath));

            if (bundle.ContentVersion != tableVersion || bundle.ContentVersion != campaignVersion)
            {
                Debug.LogError($"[서버 픽스처] 콘텐츠 버전이 어긋난다. 묶음 {bundle.ContentVersion}, 기대값 표 {tableVersion}, 캠페인 로그 {campaignVersion}.");
                return null;
            }

            string report = $"세 파일을 내보냈다. 콘텐츠 버전 {bundle.ContentVersion}\n\n- {contentFolder}\n- {tablePath}\n- {campaignPath}";
            Debug.Log("[서버 픽스처] " + report);
            return report;
        }

        // 파일(또는 그 글자)의 맨 앞 contentVersion 값. 없으면 null. 캠페인 로그는 로그마다 contentVersion이 있지만 맨 앞은 파일의 것이다.
        public static string VersionOf(string json)
        {
            Match match = VersionPattern.Match(json ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                return Encoding.UTF8.GetString(bytes);

            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static string Fail(string what)
        {
            Debug.LogError($"[서버 픽스처] {what}을(를) 내보내지 못해 멈춘다. 이미 내보낸 파일은 그대로 남아 있다. 위의 오류를 본다.");
            return null;
        }

        private static string DefaultRoot()
        {
            string saved = EditorPrefs.GetString(FolderKey, "");

            if (Directory.Exists(saved))
                return saved;

            string sibling = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ServerRootPath));
            return Directory.Exists(sibling) ? sibling : Application.dataPath;
        }
    }
}
