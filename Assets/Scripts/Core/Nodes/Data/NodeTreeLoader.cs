using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리 = 배치(NodeTreeData, 노드 도구) + 콘텐츠(NodeContent, 시트). 노드 ID로 짝짓는다.
    // 배치는 칸·선·시작 노드를, 콘텐츠는 노드 ID·Rank·비용·효과를 가진다.
    //
    // 세 단계에 걸쳐 로드.
    // 1. 배치 노드 하나씩: 콘텐츠(Nodes 시트)에 있는 노드.
    // 2. 노드 사이: ID 유일, 선이 가리키는 노드가 배치에 있고 자기 자신이 아님.
    // 3. 그래프 전체: 배치된 노드가 있으면 시작 노드가 하나 이상이고, 모든 배치 노드가 시작 노드에서 선을 따라 닿음.
    // 콘텐츠에만 있는 노드(아직 배치하지 않음)는 오류가 아니다 — 트리에서 빠지고 결과의 Unplaced로 알린다.
    public static class NodeTreeLoader
    {
        public static NodeTreeLoadResult Load(NodeTreeData data, NodeContent content)
        {
            var diagnostics = new List<ContentDiagnostic>();

            if (data == null)
            {
                diagnostics.Add(new ContentDiagnostic(string.Empty, "노드 트리 배치 데이터가 null이다."));
                return Fail(diagnostics);
            }

            List<NodeData> items = data.Nodes ?? new List<NodeData>();
            var nodes = new List<NodeDefinition>(items.Count);

            for (int i = 0; i < items.Count; i++)
                nodes.Add(LoadNode(items[i], At(i, items[i]?.Id), content, diagnostics));

            if (diagnostics.Count > 0)
                return Fail(diagnostics);

            NodeGraph graph = LoadGraph(items, diagnostics);

            if (diagnostics.Count > 0)
                return Fail(diagnostics);

            VerifyReachable(graph, diagnostics);

            if (diagnostics.Count > 0)
                return Fail(diagnostics);

            var placed = new HashSet<string>(graph.Nodes, StringComparer.Ordinal);
            var unplaced = new List<string>();

            foreach (NodeDefinition node in content.Nodes)
            {
                if (!placed.Contains(node.Id))
                    unplaced.Add(node.Id);
            }

            NodeTree tree = new(graph, nodes, content);
            return new NodeTreeLoadResult(tree, diagnostics, unplaced);
        }

        // 배치 노드 하나: 콘텐츠의 같은 ID 노드를 가져온다.
        private static NodeDefinition LoadNode(NodeData item, string at, NodeContent content, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic(at, "노드 데이터가 null이다."));
                return null;
            }

            if (!content.TryGetNode(item.Id, out NodeDefinition node))
            {
                into.Add(new ContentDiagnostic(at, $"노드 콘텐츠(Nodes 시트)에 없는 노드다: '{item.Id}'."));
                return null;
            }

            return node;
        }

        // 그래프 칸만 읽는다. 선은 방향이 없다: 한쪽에만 적어도, 양쪽에 적어도 같은 선 하나다.
        private static NodeGraph LoadGraph(List<NodeData> items, List<ContentDiagnostic> into)
        {
            var ids = new List<string>(items.Count);
            var starts = new HashSet<string>(StringComparer.Ordinal);
            var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                string id = items[i].Id;

                if (neighbors.ContainsKey(id))
                {
                    into.Add(new ContentDiagnostic($"Nodes[{i}]", $"노드 ID '{id}'가 중복됐다."));
                    continue;
                }

                ids.Add(id);
                neighbors.Add(id, new HashSet<string>(StringComparer.Ordinal));

                if (items[i].Start)
                    starts.Add(id);
            }

            for (int i = 0; i < items.Count; i++)
            {
                string id = items[i].Id;
                List<string> links = items[i].Links ?? new List<string>();

                for (int k = 0; k < links.Count; k++)
                {
                    string at = $"{At(i, id)}.Links[{k}]";

                    if (string.IsNullOrWhiteSpace(links[k]))
                        into.Add(new ContentDiagnostic(at, "이을 노드 ID가 비어 있다."));
                    else if (links[k] == id)
                        into.Add(new ContentDiagnostic(at, "자기 자신과 이을 수 없다."));
                    else if (!neighbors.ContainsKey(links[k]))
                        into.Add(new ContentDiagnostic(at, $"정의되지 않은 노드 ID다: '{links[k]}'."));
                    else
                    {
                        neighbors[id].Add(links[k]);
                        neighbors[links[k]].Add(id);
                    }
                }
            }

            var readOnly = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, HashSet<string>> pair in neighbors)
                readOnly.Add(pair.Key, new List<string>(pair.Value).AsReadOnly());

            return new NodeGraph(ids, starts, readOnly);
        }

        private static void VerifyReachable(NodeGraph graph, List<ContentDiagnostic> into)
        {
            if (graph.Nodes.Count == 0)
                return;

            bool anyStart = false;

            foreach (string id in graph.Nodes)
                anyStart |= graph.IsStart(id);

            if (!anyStart)
            {
                into.Add(new ContentDiagnostic("Nodes", "시작 노드가 하나 이상 필요하다."));
                return;
            }

            foreach (string id in graph.Unreachable())
                into.Add(new ContentDiagnostic($"Nodes[{id}]", "시작 노드에서 선을 따라 닿지 않는다."));
        }

        private static string At(int index, string id) =>
            string.IsNullOrWhiteSpace(id) ? $"Nodes[{index}]" : $"Nodes[{id}]";

        private static NodeTreeLoadResult Fail(List<ContentDiagnostic> diagnostics)
        {
            List<string> unplaced = new();
            return new NodeTreeLoadResult(null, diagnostics, unplaced);
        }
    }
}
