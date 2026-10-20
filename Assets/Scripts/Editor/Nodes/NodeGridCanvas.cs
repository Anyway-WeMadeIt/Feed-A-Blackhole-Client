using System;
using System.Collections.Generic;
using BlackHole.Authoring;
using BlackHole.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 노드 도구 창이 캔버스에 넘기는 것: 그릴 트리, 고른 것, 색, 입력을 받을 자리.
    internal interface INodeCanvasHost
    {
        NodeTreeData Tree { get; }
        bool Editing { get; }
        IReadOnlyCollection<NodeData> Selection { get; }
        Color FillOf(NodeData node);
        Color BorderOf(NodeData node);
        bool IsSelectedLink(NodeData a, NodeData b);
        string LabelOf(NodeData node);
        void OnEmptyCellClicked(int x, int y, int clickCount, bool additive);
        void OnNodeClicked(NodeData node, bool additive);
        void OnLinkClicked(NodeData a, NodeData b);
        void OnBoxSelected(List<NodeData> nodes, bool additive);
        void OnNodesDragged(IReadOnlyCollection<NodeData> nodes, int dx, int dy);
        void OnConnect(NodeData from, NodeData to);
        void OnDeletePressed();
    }

    // 격자 캔버스(UI Toolkit). 칸·선·노드를 그리고 입력을 창에 넘긴다. 규칙과 편집은 모른다.
    // 격자 칸은 X가 오른쪽, Y가 위쪽으로 커진다(화면 좌표와 Y가 반대다).
    //
    // 조작(편집):
    // - 노드 클릭: 고르기. Ctrl(Mac은 Cmd)+클릭: 고른 것에 더하거나 빼기. 빈 곳 끌기: 박스로 고르기.
    // - 노드 끌기: 고른 노드들을 함께 옮기기(선은 그대로). Shift+노드에서 노드로 끌기: 잇기.
    // - 선 클릭: 선 고르기(Delete로 끊는다). 빈 칸 더블클릭: 고른 미배치 노드 놓기(창이 정한다). Delete: 고른 선 끊기 또는 노드를 배치에서 빼기. F: 전체 보기.
    // - 휠: 확대. 가운데·오른쪽 버튼(또는 Alt+왼쪽) 끌기: 이동. 미리보기에서는 노드 클릭과 이동·확대만 된다.
    internal sealed class NodeGridCanvas : VisualElement
    {
        private const float CellSize = 72;
        private const float NodeSize = 52;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 2.5f;
        private const float DragThreshold = 6;
        private const float LinkPickDistance = 6;

        private static readonly Color Background = new Color(0.14f, 0.14f, 0.15f);
        private static readonly Color GridLine = new Color(1, 1, 1, 0.06f);
        private static readonly Color LinkLine = new Color(0.75f, 0.78f, 0.85f, 0.9f);
        private static readonly Color SelectedLink = new Color(1f, 0.85f, 0.3f);
        private static readonly Color Ghost = new Color(1, 1, 1, 0.5f);
        private static readonly Color ConnectLine = new Color(0.4f, 0.85f, 1f);
        private static readonly Color BoxFill = new Color(0.4f, 0.6f, 1f, 0.12f);
        private static readonly Color BoxBorder = new Color(0.5f, 0.7f, 1f, 0.8f);

        private enum Gesture
        {
            None,
            Panning,
            PressingNode,
            MovingNodes,
            Connecting,
            PressingEmpty,
            BoxSelecting,
        }

        private readonly INodeCanvasHost _host;
        private readonly Dictionary<NodeData, Label> _labels = new Dictionary<NodeData, Label>();
        private readonly List<NodeData> _stale = new List<NodeData>();
        private readonly List<NodeData> _moving = new List<NodeData>();

        private float _zoom = 1;
        // 칸 (0, 0)의 중심이 캔버스 중심에서 떨어진 거리(화면 픽셀).
        private Vector2 _pan;
        private bool _framed;

        private Gesture _gesture;
        private Vector2 _pressStart;
        private Vector2 _pointer;
        private Vector2 _panOrigin;
        private NodeData _pressed;
        private (int X, int Y) _pressCell;
        private (int X, int Y) _moveOffset;
        private bool _additive;

        public NodeGridCanvas(INodeCanvasHost host)
        {
            _host = host;
            focusable = true;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = Background;

            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private float Step => CellSize * _zoom;
        private Vector2 Center => contentRect.center;

        // 트리·고른 것·색이 바뀌었을 때 부른다.
        public void Refresh()
        {
            SyncLabels();
            MarkDirtyRepaint();
        }

        // 모든 노드가 보이게 확대·이동한다.
        public void FrameAll()
        {
            NodeTreeData tree = _host.Tree;

            if (tree == null || tree.Nodes.Count == 0 || contentRect.width <= 0)
            {
                _zoom = 1;
                _pan = Vector2.zero;
                Refresh();
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            foreach (NodeData node in tree.Nodes)
            {
                minX = Math.Min(minX, node.X);
                maxX = Math.Max(maxX, node.X);
                minY = Math.Min(minY, node.Y);
                maxY = Math.Max(maxY, node.Y);
            }

            float fit = Math.Min(contentRect.width / ((maxX - minX + 2) * CellSize), contentRect.height / ((maxY - minY + 2) * CellSize));
            _zoom = Mathf.Clamp(fit, MinZoom, 1.5f);
            _pan = -new Vector2((minX + maxX) * 0.5f * Step, -(minY + maxY) * 0.5f * Step);
            Refresh();
        }

        public void CenterOn(NodeData node)
        {
            _pan = -new Vector2(node.X * Step, -node.Y * Step);
            Refresh();
        }

        private Vector2 CellCenter(int x, int y) => Center + _pan + new Vector2(x * Step, -y * Step);

        private (int X, int Y) CellAt(Vector2 local)
        {
            Vector2 cell = (local - Center - _pan) / Step;
            return (Mathf.RoundToInt(cell.x), Mathf.RoundToInt(-cell.y));
        }

        private NodeData NodeAt(Vector2 local)
        {
            (int x, int y) = CellAt(local);
            return _host.Tree == null ? null : NodeTreeAuthoring.At(_host.Tree, x, y);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (!_framed && contentRect.width > 0)
            {
                _framed = true;
                FrameAll();
                return;
            }

            Refresh();
        }

        #region 그리기

        private void Draw(MeshGenerationContext context)
        {
            NodeTreeData tree = _host.Tree;

            if (tree == null || float.IsNaN(contentRect.width) || contentRect.width <= 0)
                return;

            Painter2D painter = context.painter2D;
            DrawGrid(painter);
            DrawLinks(painter, tree);

            float half = NodeSize * _zoom * 0.5f;
            float border = Math.Max(1.5f, 3 * _zoom);

            foreach (NodeData node in tree.Nodes)
            {
                painter.fillColor = _host.FillOf(node);
                painter.strokeColor = _host.BorderOf(node);
                painter.lineWidth = border;
                Rectangle(painter, CellCenter(node.X, node.Y), half);
                painter.Fill();
                painter.Stroke();
            }

            DrawGesture(painter, half);
        }

        private void DrawLinks(Painter2D painter, NodeTreeData tree)
        {
            float width = Math.Max(1.5f, 4 * _zoom);
            (NodeData A, NodeData B)? selected = null;

            painter.strokeColor = LinkLine;
            painter.lineWidth = width;
            painter.BeginPath();

            foreach ((NodeData a, NodeData b) in NodeTreeAuthoring.Links(tree))
            {
                if (_host.IsSelectedLink(a, b))
                    selected = (a, b);

                painter.MoveTo(CellCenter(a.X, a.Y));
                painter.LineTo(CellCenter(b.X, b.Y));
            }

            painter.Stroke();

            if (selected == null)
                return;

            painter.strokeColor = SelectedLink;
            painter.lineWidth = width * 1.8f;
            painter.BeginPath();
            painter.MoveTo(CellCenter(selected.Value.A.X, selected.Value.A.Y));
            painter.LineTo(CellCenter(selected.Value.B.X, selected.Value.B.Y));
            painter.Stroke();
        }

        private void DrawGesture(Painter2D painter, float half)
        {
            switch (_gesture)
            {
                case Gesture.MovingNodes:
                    painter.strokeColor = Ghost;
                    painter.lineWidth = 2;

                    foreach (NodeData node in _moving)
                    {
                        Rectangle(painter, CellCenter(node.X + _moveOffset.X, node.Y + _moveOffset.Y), half);
                        painter.Stroke();
                    }

                    break;

                case Gesture.Connecting:
                    painter.strokeColor = ConnectLine;
                    painter.lineWidth = Math.Max(1.5f, 3 * _zoom);
                    painter.BeginPath();
                    painter.MoveTo(CellCenter(_pressed.X, _pressed.Y));
                    painter.LineTo(_pointer);
                    painter.Stroke();
                    break;

                case Gesture.BoxSelecting:
                    Rect box = BoxRect();
                    painter.fillColor = BoxFill;
                    painter.strokeColor = BoxBorder;
                    painter.lineWidth = 1;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(box.xMin, box.yMin));
                    painter.LineTo(new Vector2(box.xMax, box.yMin));
                    painter.LineTo(new Vector2(box.xMax, box.yMax));
                    painter.LineTo(new Vector2(box.xMin, box.yMax));
                    painter.ClosePath();
                    painter.Fill();
                    painter.Stroke();
                    break;
            }
        }

        private void DrawGrid(Painter2D painter)
        {
            if (Step < 16)
                return;

            Rect rect = contentRect;
            (int left, int top) = CellAt(new Vector2(rect.xMin, rect.yMin));
            (int right, int bottom) = CellAt(new Vector2(rect.xMax, rect.yMax));

            painter.strokeColor = GridLine;
            painter.lineWidth = 1;
            painter.BeginPath();

            for (int x = left - 1; x <= right + 1; x++)
            {
                float screenX = CellCenter(x, 0).x + Step * 0.5f;
                painter.MoveTo(new Vector2(screenX, rect.yMin));
                painter.LineTo(new Vector2(screenX, rect.yMax));
            }

            for (int y = bottom - 1; y <= top + 1; y++)
            {
                float screenY = CellCenter(0, y).y + Step * 0.5f;
                painter.MoveTo(new Vector2(rect.xMin, screenY));
                painter.LineTo(new Vector2(rect.xMax, screenY));
            }

            painter.Stroke();
        }

        private static void Rectangle(Painter2D painter, Vector2 center, float half)
        {
            painter.BeginPath();
            painter.MoveTo(center + new Vector2(-half, -half));
            painter.LineTo(center + new Vector2(half, -half));
            painter.LineTo(center + new Vector2(half, half));
            painter.LineTo(center + new Vector2(-half, half));
            painter.ClosePath();
        }

        private Rect BoxRect() => Rect.MinMaxRect(
            Math.Min(_pressStart.x, _pointer.x), Math.Min(_pressStart.y, _pointer.y),
            Math.Max(_pressStart.x, _pointer.x), Math.Max(_pressStart.y, _pointer.y));

        // 노드마다 ID와 가격 글자. 너무 작게 줄이면 숨긴다.
        private void SyncLabels()
        {
            NodeTreeData tree = _host.Tree;
            _stale.Clear();

            foreach (NodeData node in _labels.Keys)
            {
                if (tree == null || !tree.Nodes.Contains(node))
                    _stale.Add(node);
            }

            foreach (NodeData node in _stale)
            {
                _labels[node].RemoveFromHierarchy();
                _labels.Remove(node);
            }

            if (tree == null)
                return;

            float size = NodeSize * _zoom;
            bool shown = _zoom >= 0.45f;

            foreach (NodeData node in tree.Nodes)
            {
                if (!_labels.TryGetValue(node, out Label label))
                {
                    label = new Label { pickingMode = PickingMode.Ignore };
                    label.style.position = Position.Absolute;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.color = Color.white;
                    Add(label);
                    _labels.Add(node, label);
                }

                Vector2 center = CellCenter(node.X, node.Y);
                label.text = _host.LabelOf(node);
                label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = center.x - size * 0.5f;
                label.style.top = center.y - size * 0.5f;
                label.style.width = size;
                label.style.height = size;
                label.style.fontSize = Math.Max(8, 11 * _zoom);
            }
        }

        #endregion

        #region 입력

        private void OnWheel(WheelEvent evt)
        {
            Vector2 mouse = evt.localMousePosition;
            Vector2 cell = (mouse - Center - _pan) / Step;
            _zoom = Mathf.Clamp(_zoom * (evt.delta.y > 0 ? 0.9f : 1.1f), MinZoom, MaxZoom);
            _pan = mouse - Center - cell * Step;
            evt.StopPropagation();
            Refresh();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            Vector2 local = evt.localPosition;
            _pressStart = local;
            _pointer = local;
            _additive = evt.actionKey;

            if (evt.button == 2 || evt.button == 1 || (evt.button == 0 && evt.altKey))
            {
                Begin(Gesture.Panning, evt.pointerId);
                _panOrigin = _pan;
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0 || _host.Tree == null)
                return;

            NodeData node = NodeAt(local);
            evt.StopPropagation();

            if (!_host.Editing)
            {
                // 미리보기·테스트 세팅: Ctrl(Mac은 Cmd)+클릭을 창에 넘긴다(Rank 되돌리기).
                if (node != null)
                    _host.OnNodeClicked(node, _additive);

                return;
            }

            if (node != null)
            {
                _pressed = node;

                if (evt.shiftKey)
                {
                    Begin(Gesture.Connecting, evt.pointerId);
                    return;
                }

                // 고른 묶음 안의 노드를 누르면 묶음을 그대로 두고(끌면 함께 옮긴다), 아니면 그 노드를 고른다.
                if (_additive || !Contains(_host.Selection, node))
                    _host.OnNodeClicked(node, _additive);

                Begin(Gesture.PressingNode, evt.pointerId);
                return;
            }

            _pressCell = CellAt(local);

            if (evt.clickCount >= 2)
            {
                _host.OnEmptyCellClicked(_pressCell.X, _pressCell.Y, evt.clickCount, _additive);
                return;
            }

            if (TryPickLink(local, out NodeData a, out NodeData b))
            {
                _host.OnLinkClicked(a, b);
                return;
            }

            Begin(Gesture.PressingEmpty, evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            _pointer = evt.localPosition;
            bool beyond = (_pointer - _pressStart).magnitude >= DragThreshold;

            switch (_gesture)
            {
                case Gesture.Panning:
                    _pan = _panOrigin + (_pointer - _pressStart);
                    Refresh();
                    break;

                case Gesture.PressingNode when beyond:
                    _gesture = Gesture.MovingNodes;
                    _moving.Clear();
                    _moving.AddRange(_host.Selection);

                    if (!_moving.Contains(_pressed))
                        _moving.Add(_pressed);

                    goto case Gesture.MovingNodes;

                case Gesture.MovingNodes:
                    (int x, int y) = CellAt(_pointer);
                    _moveOffset = (x - _pressed.X, y - _pressed.Y);
                    MarkDirtyRepaint();
                    break;

                case Gesture.PressingEmpty when beyond:
                    _gesture = Gesture.BoxSelecting;
                    MarkDirtyRepaint();
                    break;

                case Gesture.Connecting:
                case Gesture.BoxSelecting:
                    MarkDirtyRepaint();
                    break;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);

            Gesture gesture = _gesture;
            _gesture = Gesture.None;
            _pointer = evt.localPosition;

            switch (gesture)
            {
                case Gesture.MovingNodes:
                    if (_moveOffset.X != 0 || _moveOffset.Y != 0)
                        _host.OnNodesDragged(new List<NodeData>(_moving), _moveOffset.X, _moveOffset.Y);
                    else
                        MarkDirtyRepaint();
                    break;

                case Gesture.Connecting:
                    NodeData target = NodeAt(_pointer);

                    if (target != null && target != _pressed)
                        _host.OnConnect(_pressed, target);
                    else
                        MarkDirtyRepaint();
                    break;

                case Gesture.PressingNode:
                    // 끌지 않고 뗐다: 묶음 안의 노드를 눌렀어도 그 노드 하나만 고른다.
                    if (!_additive)
                        _host.OnNodeClicked(_pressed, false);
                    break;

                case Gesture.PressingEmpty:
                    _host.OnEmptyCellClicked(_pressCell.X, _pressCell.Y, 1, _additive);
                    break;

                case Gesture.BoxSelecting:
                    _host.OnBoxSelected(NodesIn(BoxRect()), _additive);
                    break;

                default:
                    MarkDirtyRepaint();
                    break;
            }

            _pressed = null;
            _moving.Clear();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                _host.OnDeletePressed();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.F)
            {
                FrameAll();
                evt.StopPropagation();
            }
        }

        private void Begin(Gesture gesture, int pointerId)
        {
            _gesture = gesture;
            this.CapturePointer(pointerId);
        }

        // 노드 중심이 박스 안에 든 노드.
        private List<NodeData> NodesIn(Rect box)
        {
            var nodes = new List<NodeData>();

            foreach (NodeData node in _host.Tree.Nodes)
            {
                if (box.Contains(CellCenter(node.X, node.Y)))
                    nodes.Add(node);
            }

            return nodes;
        }

        // 누른 곳에서 가장 가까운 선(가까운 거리 안에서).
        private bool TryPickLink(Vector2 local, out NodeData a, out NodeData b)
        {
            a = null;
            b = null;
            float best = LinkPickDistance;

            foreach ((NodeData from, NodeData to) in NodeTreeAuthoring.Links(_host.Tree))
            {
                float distance = DistanceToSegment(local, CellCenter(from.X, from.Y), CellCenter(to.X, to.Y));

                if (distance <= best)
                {
                    best = distance;
                    a = from;
                    b = to;
                }
            }

            return a != null;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float length = segment.sqrMagnitude;
            float t = length <= 0 ? 0 : Mathf.Clamp01(Vector2.Dot(point - start, segment) / length);
            return (point - (start + t * segment)).magnitude;
        }

        private static bool Contains(IReadOnlyCollection<NodeData> nodes, NodeData node)
        {
            foreach (NodeData each in nodes)
            {
                if (each == node)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
