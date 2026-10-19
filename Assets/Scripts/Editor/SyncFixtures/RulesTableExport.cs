using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackHole.ContentExport;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 전투 규칙 기대값 표 내보내기(계약 1.7a, W4-1a): BlackHole > Sync Fixtures > Export Rules Table...
    // 지금 콘텐츠로 표(rules-expected.json)를 만들어 서버 저장소의 src/test/resources/fixtures에 쓴다. 서버 테스트(W4-1b)가 읽는다.
    // 표를 만드는 코드는 RulesTableBuilder(Unity 무관)다. 여기는 콘텐츠를 읽고 노드 상태(loadout)를 정해 넘기고 파일을 쓴다.
    //
    // 노드 상태 6종(계약 C33): empty, partial-10/25/50/75, all.
    // - partial: 싼 노드부터 사는 정책(CheapestFirstBuyer)으로 Gold 제한 없이 끝까지 산 구매 순서의 앞쪽 p%.
    // - all: 모든 노드를 마지막 Rank까지(노출 여부와 무관).
    internal static class RulesTableExport
    {
        public const string FileName = "rules-expected.json";

        private const string FolderKey = "BlackHole.RulesTableExport.Folder";
        private const string ServerFixturesPath = "../Feed-A-Blackhole-Server/src/test/resources/fixtures";
        private static readonly int[] PartialPercents = { 10, 25, 50, 75 };

        // 구매 순서를 얻을 때의 Gold. 전부 사도 남을 만큼 크다.
        private const long UnlimitedGold = long.MaxValue / 2;

        private static readonly UTF8Encoding Utf8NoBom = new(false);

        [MenuItem("BlackHole/Sync Fixtures/Export Rules Table...")]
        private static void ExportMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("기대값 표를 쓸 폴더(서버 저장소의 src/test/resources/fixtures)", DefaultFolder(), "");

            if (string.IsNullOrEmpty(folder))
                return;

            EditorPrefs.SetString(FolderKey, folder);
            string path = ExportTo(folder);

            if (path != null)
                EditorUtility.DisplayDialog("기대값 표 내보내기", $"내보냈다.\n\n{path}\n\n서버 저장소에서 바뀐 파일을 커밋한다.", "확인");
        }

        // folder에 표를 쓰고 파일 경로를 돌려준다. 실패하면 오류를 남기고 null(콘솔 참고).
        public static string ExportTo(string folder)
        {
            try
            {
                string table = BuildTable(out string version);

                if (table == null)
                    return null;

                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, FileName);
                File.WriteAllBytes(path, Utf8NoBom.GetBytes(table));
                Debug.Log($"[기대값 표] 내보냈다. 콘텐츠 버전 {version}, {table.Length:N0}자 → {path}");
                return path;
            }
            catch (Exception error)
            {
                Debug.LogError($"[기대값 표] 내보내지 못했다: {error}");
                return null;
            }
        }

        // 지금 콘텐츠의 표 글자. 콘텐츠를 불러오지 못하면 null.
        // 같은 입력으로 두 번 만들어 같은 글자인지 확인한다(같은 콘텐츠는 같은 바이트여야 한다).
        public static string BuildTable(out string contentVersion)
        {
            contentVersion = null;
            LoadedContent loaded = SyncFixturesMenu.LoadContent(out string error);

            if (loaded == null)
            {
                Debug.LogError("[기대값 표] " + error);
                return null;
            }

            if (string.IsNullOrEmpty(loaded.ServerContentVersion))
            {
                Debug.LogError("[기대값 표] 서버 콘텐츠 버전을 계산하지 못했다.");
                return null;
            }

            contentVersion = loaded.ServerContentVersion;
            string first = Build(loaded);
            string second = Build(loaded);

            if (!string.Equals(first, second, StringComparison.Ordinal))
                throw new InvalidOperationException("같은 콘텐츠로 만든 표가 두 번 다르다. 표 생성에 결정적이지 않은 부분이 있다.");

            return first;
        }

        private static string Build(LoadedContent loaded)
        {
            List<RulesTableLoadout> loadouts = LoadoutsOf(loaded.Content, loaded.NodeTree);
            return RulesTableBuilder.Build(loaded.Content, loaded.NodeTree, loaded.ServerContentVersion, loadouts);
        }

        private static List<RulesTableLoadout> LoadoutsOf(GameContent content, NodeTree tree)
        {
            var empty = new List<(string NodeId, int Rank)>();
            var loadouts = new List<RulesTableLoadout> { new("empty", empty) };

            // 싼 노드부터 끝까지 산 구매 순서. 앞 상태가 뒤 상태에 포함된다.
            ProgressState progress = RulesTableBuilder.NewProgress(tree, content.Growth, HqGrowthDefinition.StartStage, UnlimitedGold, empty);
            List<PurchaseRecord> bought = CheapestFirstBuyer.BuyAll(progress, tree);

            foreach (int percent in PartialPercents)
            {
                int count = RulesTableBuilder.PartialCount(bought.Count, percent);
                loadouts.Add(new RulesTableLoadout($"partial-{percent}", RanksAfter(bought, count)));
            }

            var all = new List<(string NodeId, int Rank)>();

            foreach (NodeDefinition node in tree.Nodes)
                all.Add((node.Id, node.MaxRank));

            loadouts.Add(new RulesTableLoadout("all", all));
            return loadouts;
        }

        // 구매 기록의 앞 count개를 산 상태(노드마다 마지막으로 산 Rank, 처음 산 순서).
        private static List<(string NodeId, int Rank)> RanksAfter(List<PurchaseRecord> bought, int count)
        {
            var order = new List<string>();
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                PurchaseRecord record = bought[i];

                if (!ranks.ContainsKey(record.NodeId))
                    order.Add(record.NodeId);

                ranks[record.NodeId] = record.Rank;
            }

            var result = new List<(string NodeId, int Rank)>(order.Count);

            foreach (string nodeId in order)
                result.Add((nodeId, ranks[nodeId]));

            return result;
        }

        // 지난번 폴더, 없으면 클라이언트 저장소 옆의 서버 저장소 픽스처 폴더, 그 상위(src/test/resources), 그것도 없으면 에셋 폴더.
        private static string DefaultFolder()
        {
            string saved = EditorPrefs.GetString(FolderKey, "");

            if (Directory.Exists(saved))
                return saved;

            string fixtures = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ServerFixturesPath));

            if (Directory.Exists(fixtures))
                return fixtures;

            string resources = Path.GetDirectoryName(fixtures);
            return Directory.Exists(resources) ? resources : Application.dataPath;
        }
    }
}
