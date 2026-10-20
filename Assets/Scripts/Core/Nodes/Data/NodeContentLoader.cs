using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // NodeContentData(시트 4개의 행) → NodeContent.
    // 행 순서에 뜻을 두지 않는다: 수치는 StatId로, 노드는 NodeId로, Rank는 (NodeId, Rank)로 짝짓는다.
    //
    // 콘텐츠 규칙은 모두 여기서 본다. 정의(UpgradeStatDefinition·NodeDefinition·NodeRankDefinition·NodeEffect)는 구조만 지킨다.
    // 1. 수치: StatId 유일, 코드의 수치(UpgradeStat)와 이름이 맞음, 모든 UpgradeStat이 있음, ValueType·Unit 이름, Aggregation은 Add뿐,
    //    Min ≤ Max, DefaultValue는 Min·Max 안, Int 수치는 정수.
    // 2. 노드: NodeId 유일, Rank 수 1 이상.
    // 3. 비용: Nodes 시트에 있는 노드, Rank는 1 ~ Rank 수, (노드, Rank)마다 하나, 0보다 큼.
    // 4. 효과: 노드·Rank는 비용과 같은 기준, UpgradeStats 시트에 있는 StatId, 단위가 수치의 단위와 같음, Int 수치면 정수.
    // 5. 빠짐: 모든 노드의 모든 Rank에 비용과 효과가 있음.
    // 오류는 한 번에 모두 모은다. 하나라도 있으면 결과가 없다(부분 통과 금지).
    // 숫자가 유한한지는 CSV 읽기(NodeContentCsv)가 본다. 수치 합이 Max를 넘는 것은 오류가 아니다 — 계산할 때 자른다.
    public static class NodeContentLoader
    {
        public const string AddAggregation = "Add";

        public static NodeContentLoadResult Load(NodeContentData data)
        {
            var diagnostics = new List<ContentDiagnostic>();

            var statIds = new HashSet<string>(StringComparer.Ordinal);
            var statsById = new Dictionary<string, UpgradeStatDefinition>(StringComparer.Ordinal);
            List<UpgradeStatDefinition> stats = LoadStats(data.Stats, statIds, statsById, diagnostics);

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            List<NodeEntry> nodes = LoadNodes(data.Nodes, nodeIds, diagnostics);
            var nodesById = new Dictionary<string, NodeEntry>(StringComparer.Ordinal);

            foreach (NodeEntry node in nodes)
                nodesById.Add(node.Id, node);

            Dictionary<(string, int), long> costs = LoadCosts(data.Costs, nodeIds, nodesById, diagnostics);

            var effectRanks = new HashSet<(string, int)>();
            Dictionary<(string, int), List<NodeEffect>> effects =
                LoadEffects(data.Effects, nodeIds, nodesById, statIds, statsById, effectRanks, diagnostics);

            List<NodeDefinition> definitions = Assemble(nodes, costs, effects, effectRanks, diagnostics);

            if (diagnostics.Count > 0)
                return new NodeContentLoadResult(null, diagnostics);

            NodeContent content = new(stats, definitions);
            return new NodeContentLoadResult(content, diagnostics);
        }

        // 1. 수치. 틀린 수치도 ID는 statIds에 넣는다 — 그 수치를 쓰는 효과마다 "없는 StatId"가 겹쳐 나오지 않게.
        private static List<UpgradeStatDefinition> LoadStats(List<UpgradeStatRowData> rows, HashSet<string> statIds,
            Dictionary<string, UpgradeStatDefinition> statsById, List<ContentDiagnostic> into)
        {
            var stats = new List<UpgradeStatDefinition>(rows.Count);
            var firstAt = new Dictionary<string, string>(StringComparer.Ordinal);
            // 시트에 행이 있는 수치. 행이 틀렸어도 넣는다 — 그 수치에 "시트에 없다"가 겹쳐 나오지 않게.
            var listed = new HashSet<UpgradeStat>();

            foreach (UpgradeStatRowData row in rows)
            {
                string at = At(NodeContentCsv.StatsTab, row.Row);
                string id = row.StatId;

                if (id.Length == 0)
                {
                    into.Add(new ContentDiagnostic(at, "StatId가 비어 있다."));
                    continue;
                }

                if (firstAt.TryGetValue(id, out string first))
                {
                    into.Add(new ContentDiagnostic(at, $"StatId '{id}'가 {first}에도 있다."));
                    continue;
                }

                firstAt.Add(id, at);
                statIds.Add(id);
                int before = into.Count;

                if (!TryName(Pascal(id), out UpgradeStat code))
                    into.Add(new ContentDiagnostic(at, $"코드에 없는 수치다: '{id}'(UpgradeStat.{Pascal(id)}). 수치는 전투 코드가 읽어야 효과가 있다."));
                else if (!listed.Add(code))
                    into.Add(new ContentDiagnostic(at, $"StatId '{id}'가 다른 행과 같은 수치(UpgradeStat.{code})를 가리킨다."));

                if (!TryName(row.ValueType, out UpgradeStatValueType valueType))
                    into.Add(new ContentDiagnostic(at, $"ValueType은 Float·Int 가운데 하나다. 받은 값: '{row.ValueType}'."));

                if (!TryName(row.Unit, out UpgradeStatUnit unit))
                    into.Add(new ContentDiagnostic(at, $"Unit은 Flat·Percent 가운데 하나다. 받은 값: '{row.Unit}'."));

                if (row.Aggregation != AddAggregation)
                    into.Add(new ContentDiagnostic(at, $"Aggregation은 {AddAggregation}만 쓸 수 있다. 받은 값: '{row.Aggregation}'."));

                float min = row.Min ?? float.NegativeInfinity;
                float max = row.Max ?? float.PositiveInfinity;

                if (min > max)
                    into.Add(new ContentDiagnostic(at, $"Min({min})이 Max({max})보다 크다."));
                else if (row.DefaultValue < min || row.DefaultValue > max)
                    into.Add(new ContentDiagnostic(at, $"DefaultValue({row.DefaultValue})가 Min·Max [{min}, {max}] 밖이다."));

                if (valueType == UpgradeStatValueType.Int && (!IsWhole(row.DefaultValue) || (row.Min.HasValue && !IsWhole(min))
                    || (row.Max.HasValue && !IsWhole(max))))
                    into.Add(new ContentDiagnostic(at, "Int 수치는 DefaultValue·Min·Max가 정수여야 한다."));

                if (into.Count > before)
                    continue;

                var stat = new UpgradeStatDefinition(code, id, valueType, unit, row.DefaultValue, min, max, row.Enabled);
                stats.Add(stat);
                statsById.Add(id, stat);
            }

            foreach (UpgradeStat stat in (UpgradeStat[])Enum.GetValues(typeof(UpgradeStat)))
            {
                if (!listed.Contains(stat))
                    into.Add(new ContentDiagnostic(NodeContentCsv.StatsTab, $"수치 UpgradeStat.{stat}가 시트에 없다."));
            }

            return stats;
        }

        // 시트 이름 → UpgradeStat 이름: 점으로 나뉜 부분마다 첫 글자를 대문자로 붙인다(breaker.critChance → BreakerCritChance).
        private static string Pascal(string statId)
        {
            string[] parts = statId.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return string.Concat(parts);
        }

        // 2. 노드. Rank 수가 틀린 노드도 ID는 nodeIds에 넣는다 — 그 노드의 비용·효과 행마다 "없는 NodeId"가 겹쳐 나오지 않게.
        private static List<NodeEntry> LoadNodes(List<NodeRowData> rows, HashSet<string> nodeIds, List<ContentDiagnostic> into)
        {
            var nodes = new List<NodeEntry>(rows.Count);
            var firstAt = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (NodeRowData row in rows)
            {
                string at = At(NodeContentCsv.NodesTab, row.Row);
                string id = row.NodeId;

                if (id.Length == 0)
                {
                    into.Add(new ContentDiagnostic(at, "NodeId가 비어 있다."));
                    continue;
                }

                if (firstAt.TryGetValue(id, out string first))
                {
                    into.Add(new ContentDiagnostic(at, $"NodeId '{id}'가 {first}에도 있다."));
                    continue;
                }

                firstAt.Add(id, at);
                nodeIds.Add(id);

                if (row.RankCount < 1)
                {
                    into.Add(new ContentDiagnostic(at, $"'{id}'의 Rank 수는 1 이상이다. 받은 값: {row.RankCount}."));
                    continue;
                }

                nodes.Add(new NodeEntry(id, row.RankCount, at));
            }

            return nodes;
        }

        // 3. 비용. 0 이하인 비용도 자리는 채운다 — 5단계가 "비용 없음"을 겹쳐 알리지 않게. 진단이 있으니 결과는 나오지 않는다.
        private static Dictionary<(string, int), long> LoadCosts(List<NodeCostRowData> rows, HashSet<string> nodeIds,
            Dictionary<string, NodeEntry> nodes, List<ContentDiagnostic> into)
        {
            var costs = new Dictionary<(string, int), long>();
            var firstAt = new Dictionary<(string, int), string>();

            foreach (NodeCostRowData row in rows)
            {
                string at = At(NodeContentCsv.CostTab, row.Row);
                string id = row.NodeId;

                if (!IsRankOf(id, row.Rank, at, nodeIds, nodes, into))
                    continue;

                if (firstAt.TryGetValue((id, row.Rank), out string first))
                {
                    into.Add(new ContentDiagnostic(at, $"'{id}' Rank {row.Rank}의 비용이 {first}에도 있다."));
                    continue;
                }

                if (row.Cost <= 0)
                    into.Add(new ContentDiagnostic(at, $"'{id}' Rank {row.Rank}의 비용은 0보다 커야 한다. 받은 값: {row.Cost}."));

                firstAt.Add((id, row.Rank), at);
                costs.Add((id, row.Rank), row.Cost);
            }

            return costs;
        }

        // 4. 효과. 같은 (노드, Rank)에 효과가 여럿이어도 된다.
        // ranks: 효과 행이 하나라도 가리킨 (노드, Rank). 그 행이 다른 이유로 틀렸어도 넣는다 — 5단계가 "효과 없음"을 겹쳐 알리지 않게.
        private static Dictionary<(string, int), List<NodeEffect>> LoadEffects(List<NodeEffectRowData> rows, HashSet<string> nodeIds,
            Dictionary<string, NodeEntry> nodes, HashSet<string> statIds, Dictionary<string, UpgradeStatDefinition> stats,
            HashSet<(string, int)> ranks, List<ContentDiagnostic> into)
        {
            var effects = new Dictionary<(string, int), List<NodeEffect>>();

            foreach (NodeEffectRowData row in rows)
            {
                string at = At(NodeContentCsv.EffectsTab, row.Row);
                string id = row.NodeId;

                if (!IsRankOf(id, row.Rank, at, nodeIds, nodes, into))
                    continue;

                ranks.Add((id, row.Rank));

                if (!statIds.Contains(row.StatId))
                {
                    into.Add(new ContentDiagnostic(at, $"UpgradeStats 시트에 없는 StatId다: '{row.StatId}'."));
                    continue;
                }

                // 수치 정의 자체가 틀렸으면(1단계에서 이미 알림) 단위·정수 검사는 건너뛴다.
                if (stats.TryGetValue(row.StatId, out UpgradeStatDefinition stat) && !Fits(stat, row, at, into))
                    continue;

                if (!effects.TryGetValue((id, row.Rank), out List<NodeEffect> list))
                    effects.Add((id, row.Rank), list = new List<NodeEffect>());

                // 수치 정의가 틀렸으면 1단계가 이미 알렸고 결과가 없다.
                if (stat != null)
                    list.Add(new NodeEffect(stat.Stat, row.Value));
            }

            return effects;
        }

        // 5. 노드마다 Rank 1부터 Rank 수까지 비용과 효과를 모아 정의를 만든다. Nodes 시트 순서를 지킨다.
        private static List<NodeDefinition> Assemble(List<NodeEntry> nodes, Dictionary<(string, int), long> costs,
            Dictionary<(string, int), List<NodeEffect>> effects, HashSet<(string, int)> effectRanks, List<ContentDiagnostic> into)
        {
            var definitions = new List<NodeDefinition>(nodes.Count);

            foreach (NodeEntry node in nodes)
            {
                var ranks = new List<NodeRankDefinition>(node.RankCount);
                var noCost = new List<int>();
                var noEffect = new List<int>();

                for (int rank = 1; rank <= node.RankCount; rank++)
                {
                    bool hasCost = costs.TryGetValue((node.Id, rank), out long cost);
                    bool hasEffects = effects.TryGetValue((node.Id, rank), out List<NodeEffect> list);

                    if (!hasCost)
                        noCost.Add(rank);

                    if (!effectRanks.Contains((node.Id, rank)))
                        noEffect.Add(rank);

                    if (!hasCost || !hasEffects)
                        continue;

                    // 같은 Rank의 효과 순서가 시트 행 순서에 닿지 않게 정렬한다.
                    list.Sort(CompareEffects);
                    ranks.Add(new NodeRankDefinition(rank, cost, list.AsReadOnly()));
                }

                if (noCost.Count > 0)
                    into.Add(new ContentDiagnostic(node.At, $"'{node.Id}'의 {RankList(noCost)} 비용이 {NodeContentCsv.CostTab} 시트에 없다."));

                if (noEffect.Count > 0)
                    into.Add(new ContentDiagnostic(node.At, $"'{node.Id}'의 {RankList(noEffect)} 효과가 {NodeContentCsv.EffectsTab} 시트에 없다."));

                if (ranks.Count == node.RankCount)
                    definitions.Add(new NodeDefinition(node.Id, ranks.AsReadOnly()));
            }

            return definitions;
        }

        // 비용·효과 행이 가리키는 (노드, Rank)가 있는가. Rank 수가 틀린 노드(2단계에서 이미 알림)를 가리키면 조용히 false다.
        private static bool IsRankOf(string id, int rank, string at, HashSet<string> nodeIds, Dictionary<string, NodeEntry> nodes,
            List<ContentDiagnostic> into)
        {
            if (!nodeIds.Contains(id))
            {
                into.Add(new ContentDiagnostic(at, $"{NodeContentCsv.NodesTab} 시트에 없는 NodeId다: '{id}'."));
                return false;
            }

            if (!nodes.TryGetValue(id, out NodeEntry node))
                return false;

            if (rank < 1 || rank > node.RankCount)
            {
                into.Add(new ContentDiagnostic(at, $"'{id}'의 Rank는 1부터 {node.RankCount}까지다(Nodes 시트 Rank 수). 받은 값: {rank}."));
                return false;
            }

            return true;
        }

        // 효과 값이 수치 정의에 맞는가: 단위가 같고, Int 수치면 정수다.
        private static bool Fits(UpgradeStatDefinition stat, NodeEffectRowData row, string at, List<ContentDiagnostic> into)
        {
            int before = into.Count;

            if (!TryName(row.Unit, out UpgradeStatUnit unit))
                into.Add(new ContentDiagnostic(at, $"단위는 Flat·Percent 가운데 하나다. 받은 값: '{row.Unit}'."));
            else if (unit != stat.Unit)
                into.Add(new ContentDiagnostic(at, $"단위가 수치 정의와 다르다: '{stat.StatId}'는 {stat.Unit}인데 {unit}로 적었다."));

            if (stat.ValueType == UpgradeStatValueType.Int && !IsWhole(row.Value))
                into.Add(new ContentDiagnostic(at, $"'{stat.StatId}'는 정수 수치다. 받은 값: {row.Value}."));

            return into.Count == before;
        }

        // 이름 칸을 enum으로 읽는다. 대소문자까지 같아야 하고, 숫자("0")는 받지 않는다.
        private static bool TryName<T>(string text, out T value) where T : struct, Enum
        {
            foreach (T candidate in (T[])Enum.GetValues(typeof(T)))
            {
                if (candidate.ToString() == text)
                {
                    value = candidate;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool IsWhole(float value) => value == Math.Floor(value);

        private static int CompareEffects(NodeEffect a, NodeEffect b)
        {
            int byStat = a.Stat.CompareTo(b.Stat);
            return byStat != 0 ? byStat : a.Value.CompareTo(b.Value);
        }

        private static string RankList(List<int> ranks) => "Rank " + string.Join(", ", ranks);

        private static string At(string tab, int row) => $"{tab} {row}행";

        private readonly struct NodeEntry
        {
            public readonly string Id;
            public readonly int RankCount;
            public readonly string At;

            public NodeEntry(string id, int rankCount, string at)
            {
                Id = id;
                RankCount = rankCount;
                At = at;
            }
        }
    }
}
