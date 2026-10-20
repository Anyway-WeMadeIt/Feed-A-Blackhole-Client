using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.ContentExport
{
    // 노드 시트 CSV를 서버 형식으로 바꾼다(계약 1.2). 서버가 읽는 칸과 Memo만 남기고 칸 이름을 영문으로 바꾼다.
    // 칸 안의 값은 바꾸지 않는다(천 단위 쉼표 같은 표기도 그대로). 서버가 읽는 칸이 모두 빈 행은 뺀다(시트 끝의 빈 행).
    internal static class NodeSheets
    {
        // (서버 칸 이름, 시트 칸 이름 또는 null(빈 칸))
        private static readonly (string Server, string Sheet)[] _statsColumns =
        {
            ("StatId", "StatId"), ("ValueType", "ValueType"), ("Unit", "Unit"), ("DefaultValue", "DefaultValue"),
            ("Aggregation", "Aggregation"), ("Min", "Min"), ("Max", "Max"), ("Memo", "Memo"),
        };

        private static readonly (string Server, string Sheet)[] _nodesColumns =
        {
            ("NodeId", "NodeId"), ("RankCount", "Rank 수"), ("Memo", "Memo"),
        };

        private static readonly (string Server, string Sheet)[] _costColumns =
        {
            ("NodeId", "NodeId"), ("Rank", "Rank"), ("Cost", "Cost"), ("Memo", null),
        };

        private static readonly (string Server, string Sheet)[] _effectsColumns =
        {
            ("NodeId", "NodeId"), ("Rank", "Rank"), ("StatId", "StatId"), ("Value", "Value"), ("Unit", "단위"), ("Memo", null),
        };

        public static string UpgradeStats(string csv) =>
            Convert(csv, _statsColumns, new[] { "StatId", "ValueType", "Unit", "DefaultValue", "Aggregation", "Min", "Max" });

        public static string Nodes(string csv) => Convert(csv, _nodesColumns, new[] { "NodeId", "Rank 수" });

        public static string NodeCost(string csv) => Convert(csv, _costColumns, new[] { "NodeId", "Rank", "Cost" });

        public static string NodeEffects(string csv) =>
            Convert(csv, _effectsColumns, new[] { "NodeId", "Rank", "StatId", "Value", "단위" });

        private static string Convert(string csv, (string Server, string Sheet)[] columns, string[] readColumns)
        {
            List<string[]> rows = Csv.Parse(csv);

            if (rows.Count == 0)
                throw new InvalidOperationException("시트 CSV가 비어 있다.");

            // 같은 이름의 칸이 여럿이면 마지막 칸을 쓴다(Python csv.DictReader와 같다).
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] header = rows[0];

            for (int i = 0; i < header.Length; i++)
                index[header[i]] = i;

            foreach ((string _, string sheet) in columns)
            {
                if (sheet != null && !index.ContainsKey(sheet))
                    throw new InvalidOperationException($"시트 CSV에 '{sheet}' 칸이 없다.");
            }

            var output = new List<IReadOnlyList<string>>(rows.Count);
            var serverHeader = new string[columns.Length];

            for (int i = 0; i < columns.Length; i++)
                serverHeader[i] = columns[i].Server;

            output.Add(serverHeader);

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];

                if (IsBlank(row, readColumns, index))
                    continue;

                var cells = new string[columns.Length];

                for (int i = 0; i < columns.Length; i++)
                    cells[i] = columns[i].Sheet == null ? "" : CellOf(row, index[columns[i].Sheet]);

                output.Add(cells);
            }

            return Csv.Write(output);
        }

        private static bool IsBlank(string[] row, string[] readColumns, Dictionary<string, int> index)
        {
            foreach (string column in readColumns)
            {
                if (CellOf(row, index[column]).Trim().Length > 0)
                    return false;
            }

            return true;
        }

        private static string CellOf(string[] row, int column) => column < row.Length ? row[column] ?? "" : "";
    }
}
