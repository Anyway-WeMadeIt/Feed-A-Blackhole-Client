using System;
using System.Collections.Generic;
using System.Text;
using BlackHole.Core;

namespace BlackHole.ContentExport
{
    // 클라이언트 콘텐츠를 서버 저장소 형식의 묶음으로 바꾼다(계약 1.2~1.4). Unity에 의존하지 않는다.
    // 같은 콘텐츠면 언제나 같은 바이트를 낸다(콘텐츠 버전이 바이트로 계산된다).
    //
    // - nodes/*.csv: 시트 CSV에서 서버가 읽는 칸과 Memo만 남기고 칸 이름을 영문으로 바꾼다. 칸 안의 값은 그대로다.
    //   (서버 tools/import_node_content.py가 하던 변환과 같다.)
    // - nodes/layout.json: 노드 배치. 노드 한 개가 한 줄이다.
    // - battle/battle.json: 서버 결산 검증에 필요한 전투 콘텐츠만.
    public static class ServerContentExporter
    {
        public static readonly IReadOnlyList<string> FilePaths = Array.AsReadOnly(new[]
        {
            "nodes/UpgradeStats.csv",
            "nodes/Nodes.csv",
            "nodes/NodeCost.csv",
            "nodes/NodeEffects.csv",
            "nodes/layout.json",
            "battle/battle.json",
        });

        private static readonly UTF8Encoding _utf8NoBom = new(false);

        public static ServerContentBundle Export(ServerContentSource source)
        {
            var files = new List<BundleFile>
            {
                Text(FilePaths[0], NodeSheets.UpgradeStats(source.UpgradeStatsCsv)),
                Text(FilePaths[1], NodeSheets.Nodes(source.NodesCsv)),
                Text(FilePaths[2], NodeSheets.NodeCost(source.NodeCostCsv)),
                Text(FilePaths[3], NodeSheets.NodeEffects(source.NodeEffectsCsv)),
                Text(FilePaths[4], LayoutJson(source.Layout)),
                Text(FilePaths[5], BattleJson.Write(source.Battle, source.Nodes)),
            };

            return new ServerContentBundle(files.AsReadOnly());
        }

        // {"Nodes": [ 노드 한 줄씩 ]}. 키 순서는 Id, Start, X, Y, Links.
        private static string LayoutJson(NodeTreeData layout)
        {
            var lines = new List<string>(layout.Nodes.Count);

            foreach (NodeData node in layout.Nodes)
            {
                var links = new List<object>();

                foreach (string link in node.Links)
                    links.Add(link);

                lines.Add("    " + PyJson.WriteInline(new JsonObject
                {
                    { "Id", node.Id },
                    { "Start", node.Start },
                    { "X", node.X },
                    { "Y", node.Y },
                    { "Links", links },
                }));
            }

            return "{\n  \"Nodes\": [\n" + string.Join(",\n", lines) + "\n  ]\n}\n";
        }

        private static BundleFile Text(string path, string text) => new(path, _utf8NoBom.GetBytes(text));
    }
}
