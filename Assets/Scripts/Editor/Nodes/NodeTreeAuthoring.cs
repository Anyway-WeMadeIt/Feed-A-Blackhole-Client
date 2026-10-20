using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;

namespace BlackHole.Authoring
{
    // 노드 도구의 편집 규칙. 배치의 저작 형식(NodeTreeData)을 고친다. 엔진을 모르고, 게임 빌드에는 들어가지 않는다(에디터 전용 어셈블리).
    //
    // 노드 ID는 노드 콘텐츠(Nodes 시트)가 정한다. 이 도구는 ID를 만들거나 바꾸지 않는다:
    // 콘텐츠에 있는 ID를 빈 칸에 놓고(Place), 옮기고, 잇고, 배치에서 뺀다(Remove — 콘텐츠의 노드는 그대로 남아 다시 놓을 수 있다).
    // 선은 제작자가 그은 것만이다. 좌표는 표시용이고 선은 게임 규칙이므로, 놓기와 옮기기는 선을 건드리지 않는다.
    // 격자 이웃 잇기(LinkNeighbors)는 저작 명령이다: 고른 노드 가운데 상하좌우로 붙은 쌍을 그 순간 잇는다.
    // 그 뒤에 노드를 옮겨도 선은 그대로다. 자동 연결은 규칙의 원천이 아니라 손을 덜어 주는 기능이다.
    // 처음 놓은 노드는 시작 노드가 된다. 비용·효과는 노드 콘텐츠의 것이라 여기서 다루지 않는다.
    public static class NodeTreeAuthoring
    {
        public static NodeData At(NodeTreeData tree, int x, int y)
        {
            foreach (NodeData node in tree.Nodes)
            {
                if (node != null && node.X == x && node.Y == y)
                    return node;
            }

            return null;
        }

        public static NodeData Find(NodeTreeData tree, string id)
        {
            foreach (NodeData node in tree.Nodes)
            {
                if (node != null && node.Id == id)
                    return node;
            }

            return null;
        }

        // 아직 놓지 않은 노드 id를 빈 칸에 놓는다. 선은 긋지 않는다. 칸이 차 있으면 null이다.
        public static NodeData Place(NodeTreeData tree, string id, int x, int y)
        {
            if (At(tree, x, y) != null)
                return null;

            var node = new NodeData { Id = id, Start = tree.Nodes.Count == 0, X = x, Y = y };
            tree.Nodes.Add(node);
            return node;
        }

        // ids 가운데 아직 놓지 않은 것(ids 순서).
        public static List<string> Unplaced(NodeTreeData tree, IEnumerable<string> ids)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);

            foreach (NodeData node in tree.Nodes)
                placed.Add(node.Id);

            var unplaced = new List<string>();

            foreach (string id in ids)
            {
                if (!placed.Contains(id))
                    unplaced.Add(id);
            }

            return unplaced;
        }

        // 노드들을 함께 (dx, dy)칸 옮긴다. 선은 그대로다.
        // 옮길 칸에 함께 옮기지 않는 노드가 있으면 아무것도 옮기지 않고 false다.
        public static bool MoveBy(NodeTreeData tree, IReadOnlyCollection<NodeData> nodes, int dx, int dy)
        {
            if (dx == 0 && dy == 0)
                return true;

            var moving = new HashSet<NodeData>(nodes);

            foreach (NodeData node in moving)
            {
                NodeData occupant = At(tree, node.X + dx, node.Y + dy);

                if (occupant != null && !moving.Contains(occupant))
                    return false;
            }

            foreach (NodeData node in moving)
            {
                node.X += dx;
                node.Y += dy;
            }

            return true;
        }

        // 노드를 배치에서 빼고, 다른 노드에 적힌 그 노드와의 선도 지운다. 콘텐츠의 노드는 그대로라 다시 놓을 수 있다.
        public static void Remove(NodeTreeData tree, NodeData node)
        {
            tree.Nodes.Remove(node);

            foreach (NodeData other in tree.Nodes)
            {
                if (other != null)
                    LinksOf(other).RemoveAll(link => link == node.Id);
            }
        }

        // 선은 방향이 없다. 어느 쪽에 적혀 있어도 이어진 것이다.
        public static bool AreLinked(NodeData a, NodeData b) =>
            LinksOf(a).Contains(b.Id) || LinksOf(b).Contains(a.Id);

        // 잇는다. 새로 이었으면 true, 이미 이어져 있었으면 false다.
        public static bool Link(NodeData a, NodeData b)
        {
            if (a == b)
                throw new ArgumentException("자기 자신과 이을 수 없다.", nameof(b));

            if (AreLinked(a, b))
                return false;

            LinksOf(a).Add(b.Id);
            return true;
        }

        // 끊는다. 이어져 있었으면 true다.
        public static bool Unlink(NodeData a, NodeData b)
        {
            int removed = LinksOf(a).RemoveAll(link => link == b.Id) + LinksOf(b).RemoveAll(link => link == a.Id);
            return removed > 0;
        }

        // 저작 명령 "이웃끼리 잇기": 고른 노드 가운데 상하좌우로 붙은 쌍을 잇는다. 고르지 않은 노드와는 잇지 않는다. 새로 이은 수를 돌려준다.
        public static int LinkNeighbors(IReadOnlyCollection<NodeData> nodes)
        {
            var byCell = new Dictionary<(int, int), NodeData>();

            foreach (NodeData node in nodes)
            {
                if (!byCell.ContainsKey((node.X, node.Y)))
                    byCell.Add((node.X, node.Y), node);
            }

            int added = 0;

            foreach (NodeData node in nodes)
            {
                // 오른쪽과 위쪽만 보면 붙은 쌍을 한 번씩 본다.
                if (byCell.TryGetValue((node.X + 1, node.Y), out NodeData right) && right != node && Link(node, right))
                    added++;

                if (byCell.TryGetValue((node.X, node.Y + 1), out NodeData up) && up != node && Link(node, up))
                    added++;
            }

            return added;
        }

        // 저작 명령 "선택끼리 끊기": 고른 노드 사이의 선을 끊는다. 고르지 않은 노드와의 선은 남는다. 끊은 수를 돌려준다.
        public static int UnlinkAmong(IReadOnlyList<NodeData> nodes)
        {
            int removed = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (nodes[i] != nodes[j] && Unlink(nodes[i], nodes[j]))
                        removed++;
                }
            }

            return removed;
        }

        // 저작 명령 "선 모두 지우기": 고른 노드에 닿은 선을 모두 끊는다. 없는 노드를 가리키던 선도 지운다. 끊은 수를 돌려준다.
        public static int ClearLinks(NodeTreeData tree, IReadOnlyCollection<NodeData> nodes)
        {
            int removed = 0;

            foreach (NodeData node in nodes)
            {
                foreach (NodeData other in tree.Nodes)
                {
                    if (other != null && other != node && Unlink(node, other))
                        removed++;
                }

                LinksOf(node).Clear();
            }

            return removed;
        }

        // 그릴 선: 양 끝이 모두 있는 선을 한 번씩.
        public static List<(NodeData A, NodeData B)> Links(NodeTreeData tree)
        {
            var pairs = new List<(NodeData, NodeData)>();
            var seen = new HashSet<(NodeData, NodeData)>();

            foreach (NodeData node in tree.Nodes)
            {
                if (node == null)
                    continue;

                foreach (string link in LinksOf(node))
                {
                    NodeData other = Find(tree, link);

                    if (other == null || other == node || seen.Contains((other, node)) || !seen.Add((node, other)))
                        continue;

                    pairs.Add((node, other));
                }
            }

            return pairs;
        }

        // 도구만 보는 검사. 게임 규칙의 검사는 NodeTreeLoader가 한다.
        // - 한 칸에 노드 둘: 화면에서 겹친다.
        public static List<ContentDiagnostic> Check(NodeTreeData tree)
        {
            var diagnostics = new List<ContentDiagnostic>();
            var cells = new Dictionary<(int, int), NodeData>();

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                NodeData node = tree.Nodes[i];

                if (node == null)
                    continue;

                string at = At(i, node.Id);

                if (cells.TryGetValue((node.X, node.Y), out NodeData first))
                    diagnostics.Add(new ContentDiagnostic(at, $"칸 ({node.X}, {node.Y})에서 '{first.Id}'와 겹친다."));
                else
                    cells.Add((node.X, node.Y), node);
            }

            return diagnostics;
        }

        // 효과 값의 표기(시트의 "표시" 칸과 같다): Flat +3, Percent +25%. 값은 시트 단위 그대로다.
        public static string Notation(UpgradeStatUnit unit, float value) =>
            (value >= 0 ? "+" : string.Empty) + Number(value) + (unit == UpgradeStatUnit.Percent ? "%" : string.Empty);

        private static List<string> LinksOf(NodeData node) => node.Links ??= new List<string>();

        // NodeTreeLoader와 같은 경로 모양이다. 진단을 누르면 도구가 이 경로로 노드를 찾는다.
        private static string At(int index, string id) =>
            string.IsNullOrWhiteSpace(id) ? $"Nodes[{index}]" : $"Nodes[{id}]";

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
