using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlackHole.Core
{
    // 노드 콘텐츠 시트 4개(CSV)를 행 데이터(NodeContentData)로 읽는다. 형식(머리칸·글자·숫자)만 보고, 규칙은 NodeContentLoader가 본다.
    // 칸은 머리칸 이름으로 찾는다: 분석용 칸(원작 이름·검사 등)이 더 있어도, 칸 순서가 바뀌어도 된다. 읽는 칸이 모두 빈 행은 건너뛴다.
    // 진단 위치는 시트 좌표다("NodeCost!C12").
    public static class NodeContentCsv
    {
        public const string StatsTab = "UpgradeStats";
        public const string NodesTab = "Nodes";
        public const string CostTab = "NodeCost";
        public const string EffectsTab = "NodeEffects";

        private static readonly string[] _statsColumns = { "StatId", "ValueType", "Unit", "DefaultValue", "Aggregation", "Min", "Max", "Enabled" };
        private static readonly string[] _nodesColumns = { "NodeId", "Rank 수" };
        private static readonly string[] _costColumns = { "NodeId", "Rank", "Cost" };
        private static readonly string[] _effectsColumns = { "NodeId", "Rank", "StatId", "Value", "단위" };

        // 시트 4개를 읽어 불러온다. null은 시트가 없다(연결되지 않음)는 뜻이다.
        // 형식 오류가 하나라도 있으면 규칙 검사 없이 실패한다.
        public static NodeContentLoadResult Load(string statsCsv, string nodesCsv, string costCsv, string effectsCsv)
        {
            var diagnostics = new List<ContentDiagnostic>();
            NodeContentData data = Read(statsCsv, nodesCsv, costCsv, effectsCsv, diagnostics);

            return diagnostics.Count > 0
                ? new NodeContentLoadResult(null, diagnostics)
                : NodeContentLoader.Load(data);
        }

        // 형식 오류는 into에 더하고, 그 칸은 0이나 빈 글자로 둔다. into가 늘었으면 결과를 쓰지 않는다.
        public static NodeContentData Read(string statsCsv, string nodesCsv, string costCsv, string effectsCsv, List<ContentDiagnostic> into)
        {
            var data = new NodeContentData();
            Sheet stats = Sheet.Open(StatsTab, statsCsv, _statsColumns, into);
            Sheet nodes = Sheet.Open(NodesTab, nodesCsv, _nodesColumns, into);
            Sheet costs = Sheet.Open(CostTab, costCsv, _costColumns, into);
            Sheet effects = Sheet.Open(EffectsTab, effectsCsv, _effectsColumns, into);

            if (stats != null)
            {
                foreach (int r in stats.Rows())
                {
                    data.Stats.Add(new UpgradeStatRowData
                    {
                        Row = Sheet.SheetRow(r),
                        StatId = stats.Text(r, "StatId"),
                        ValueType = stats.Text(r, "ValueType"),
                        Unit = stats.Text(r, "Unit"),
                        DefaultValue = stats.Float(r, "DefaultValue"),
                        Aggregation = stats.Text(r, "Aggregation"),
                        Min = stats.OptionalFloat(r, "Min"),
                        Max = stats.OptionalFloat(r, "Max"),
                        Enabled = stats.Bool(r, "Enabled"),
                    });
                }
            }

            if (nodes != null)
            {
                foreach (int r in nodes.Rows())
                {
                    data.Nodes.Add(new NodeRowData
                    {
                        Row = Sheet.SheetRow(r),
                        NodeId = nodes.Text(r, "NodeId"),
                        RankCount = nodes.Int(r, "Rank 수"),
                    });
                }
            }

            if (costs != null)
            {
                foreach (int r in costs.Rows())
                {
                    data.Costs.Add(new NodeCostRowData
                    {
                        Row = Sheet.SheetRow(r),
                        NodeId = costs.Text(r, "NodeId"),
                        Rank = costs.Int(r, "Rank"),
                        Cost = costs.Long(r, "Cost"),
                    });
                }
            }

            if (effects != null)
            {
                foreach (int r in effects.Rows())
                {
                    data.Effects.Add(new NodeEffectRowData
                    {
                        Row = Sheet.SheetRow(r),
                        NodeId = effects.Text(r, "NodeId"),
                        Rank = effects.Int(r, "Rank"),
                        StatId = effects.Text(r, "StatId"),
                        Value = effects.Float(r, "Value"),
                        Unit = effects.Text(r, "단위"),
                    });
                }
            }

            return data;
        }

        // 시트 하나: 머리칸 이름 → 칸 번호. 칸을 읽지 못하면 그 좌표로 진단을 더한다.
        private sealed class Sheet
        {
            private readonly string _tab;
            private readonly List<string[]> _rows;
            private readonly Dictionary<string, int> _columns;
            private readonly string[] _required;
            private readonly List<ContentDiagnostic> _into;

            private Sheet(string tab, List<string[]> rows, Dictionary<string, int> columns, string[] required, List<ContentDiagnostic> into)
            {
                _tab = tab;
                _rows = rows;
                _columns = columns;
                _required = required;
                _into = into;
            }

            // 머리칸에 required가 모두 있어야 연다. 없으면 진단을 더하고 null이다.
            public static Sheet Open(string tab, string csv, string[] required, List<ContentDiagnostic> into)
            {
                if (csv == null)
                {
                    into.Add(new ContentDiagnostic(tab, "시트(CSV)가 없다."));
                    return null;
                }

                List<string[]> rows = Csv.Parse(csv);

                if (rows.Count == 0)
                {
                    into.Add(new ContentDiagnostic(tab, $"비어 있다. 첫 행에 머리칸({string.Join(", ", required)})이 필요하다."));
                    return null;
                }

                var columns = new Dictionary<string, int>(StringComparer.Ordinal);

                for (int c = 0; c < rows[0].Length; c++)
                {
                    string title = rows[0][c].Trim();

                    if (title.Length > 0 && !columns.ContainsKey(title))
                        columns.Add(title, c);
                }

                bool complete = true;

                foreach (string title in required)
                {
                    if (!columns.ContainsKey(title))
                    {
                        into.Add(new ContentDiagnostic($"{tab} 1행", $"머리칸 '{title}'이 없다."));
                        complete = false;
                    }
                }

                return complete ? new Sheet(tab, rows, columns, required, into) : null;
            }

            // 표의 행 번호 → 시트 행 번호(머리칸이 1행).
            public static int SheetRow(int r) => r + 1;

            // 머리칸 아래의 행 가운데, 읽는 칸이 모두 비지 않은 행.
            public IEnumerable<int> Rows()
            {
                for (int r = 1; r < _rows.Count; r++)
                {
                    foreach (string column in _required)
                    {
                        if (Text(r, column).Length > 0)
                        {
                            yield return r;
                            break;
                        }
                    }
                }
            }

            public string Text(int r, string column)
            {
                int c = _columns[column];
                string[] row = _rows[r];
                return c < row.Length ? row[c].Trim() : string.Empty;
            }

            public int Int(int r, string column)
            {
                string text = Text(r, column);

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    return value;

                Fail(r, column, text, "정수");
                return 0;
            }

            // 천 단위 쉼표("2,600,000")를 허용한다.
            public long Long(int r, string column)
            {
                string text = Text(r, column);

                if (long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long value))
                    return value;

                Fail(r, column, text, "정수");
                return 0;
            }

            // 소수는 점(.)으로 적는다. % 기호는 받지 않는다 — 값은 단위 칸이 정하고, Percent의 25는 25%다.
            public float Float(int r, string column)
            {
                string text = Text(r, column);

                if (float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float value)
                    && !float.IsNaN(value) && !float.IsInfinity(value))
                    return value;

                Fail(r, column, text, "숫자(소수는 점으로, % 기호 없이)");
                return 0;
            }

            // 빈 칸은 null이다.
            public float? OptionalFloat(int r, string column) =>
                Text(r, column).Length == 0 ? (float?)null : Float(r, column);

            public bool Bool(int r, string column)
            {
                string text = Text(r, column);

                if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase))
                    return false;

                Fail(r, column, text, "TRUE 또는 FALSE");
                return false;
            }

            private void Fail(int r, string column, string text, string expected) =>
                _into.Add(new ContentDiagnostic(Cell(r, column),
                    text.Length == 0 ? $"비어 있다. {expected}가 필요하다." : $"{expected}가 필요하다. 받은 값: '{text}'."));

            private string Cell(int r, string column) => $"{_tab}!{Letter(_columns[column])}{SheetRow(r)}";

            // 칸 번호(0부터) → 시트 열 이름(A, B, …, Z, AA, …).
            private static string Letter(int column)
            {
                string letters = string.Empty;

                for (int n = column + 1; n > 0; n = (n - 1) / 26)
                    letters = (char)('A' + (n - 1) % 26) + letters;

                return letters;
            }
        }
    }
}
