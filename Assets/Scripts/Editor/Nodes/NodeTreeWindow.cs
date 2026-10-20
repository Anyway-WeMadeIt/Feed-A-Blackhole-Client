using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 노드 도구(F03, 메뉴 BlackHole > Node Tree). 노드 목록 에셋(NodeCatalog)의 배치를 격자 위에서 고친다.
    //
    // 어떤 노드가 있는지(ID)와 Rank·비용·효과는 노드 콘텐츠(시트 CSV, NodeCatalog가 가리키는 NodeContentSource)가 원본이고 여기서는 보기만 한다.
    // 이 도구가 원본인 것은 칸·선·시작 노드다. ID를 만들거나 바꾸지 않는다:
    // - 놓기: 오른쪽 "미배치 노드" 목록에서 콘텐츠의 노드를 고르고 빈 칸을 더블클릭한다. 놓으면 목록의 다음 노드로 넘어간다.
    // - 빼기: 노드를 고르고 Delete(또는 "배치에서 빼기"). 콘텐츠의 노드는 그대로라 목록으로 돌아간다.
    // - 편집: 선은 그은 것만이다. 놓기·옮기기는 선을 건드리지 않는다(좌표는 표시용, 선은 게임 규칙).
    //   잇기는 Shift+끌기, 끊기는 선을 눌러 Delete. 여러 노드를 고르면 명령이 나온다:
    //   둘 잇기, 이웃끼리 잇기(격자 이웃을 그 순간 잇는 저작 명령), 선택끼리 끊기, 선 모두 지우기. 편집 규칙은 NodeTreeAuthoring에 있다.
    // - 검사: 고칠 때마다 게임과 같은 로더(NodeContentCsv → NodeTreeLoader)로 불러 보고, 도구 검사(겹친 칸)를 더한다.
    //   진단을 누르면 그 노드로 간다.
    // - 구매 미리보기: 게임과 같은 규칙(NodeGraph의 드러남, NodePurchase의 Rank 구매)으로 노드를 사 보며 네 가지 상태와 수치 값을 본다.
    //   Gold는 무한이다. 누를 때마다 다음 Rank를 산다. Ctrl+클릭이나 마지막 Rank에서 다시 누르면 그 노드를 미리보기에서만 되돌린다(게임에는 환불이 없다).
    // 고친 내용은 Undo로 되돌릴 수 있고, 저장 버튼이나 프로젝트 저장으로 에셋에 쓴다.
    internal sealed class NodeTreeWindow : EditorWindow, INodeCanvasHost
    {
        private const long PreviewGold = long.MaxValue / 4;

        private static readonly Color NodeFill = new Color(0.25f, 0.32f, 0.45f);
        private static readonly Color StartFill = new Color(0.2f, 0.42f, 0.48f);
        private static readonly Color HiddenFill = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color RevealedFill = new Color(0.38f, 0.38f, 0.4f);
        private static readonly Color PurchasableFill = new Color(0.62f, 0.5f, 0.12f);
        private static readonly Color OwnedFill = new Color(0.2f, 0.5f, 0.25f);
        private static readonly Color PlainBorder = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color StartBorder = new Color(0.4f, 0.85f, 0.95f);
        private static readonly Color SelectedBorder = Color.white;
        private static readonly Color ErrorBorder = new Color(0.95f, 0.3f, 0.3f);
        private static readonly Color ErrorText = new Color(1f, 0.55f, 0.55f);
        private static readonly Color WarningText = new Color(1f, 0.85f, 0.45f);
        private static readonly Color PlacingFill = new Color(0.22f, 0.42f, 0.62f);

        [SerializeField] private NodeCatalog catalog;

        // 고른 노드의 ID(고른 순서). Undo가 노드 객체를 새로 만들 수 있어 ID로 기억한다.
        private readonly List<string> _selectedIds = new List<string>();
        // 미리보기에서 산 Rank(산 순서). 같은 ID가 Rank 수만큼 나온다.
        private readonly List<string> _previewOwned = new List<string>();
        private readonly HashSet<string> _nodesWithErrors = new HashSet<string>(StringComparer.Ordinal);

        private NodeGridCanvas _canvas;
        private VisualElement _panel;
        private VisualElement _diagnosticsList;
        private VisualElement _unplacedSection;
        private Label _unplacedHeader;
        private VisualElement _unplacedList;
        private string _unplacedFilter = string.Empty;
        // 다음에 빈 칸을 더블클릭하면 놓을 콘텐츠 노드의 ID. 없으면 null.
        private string _placingId;
        private (string A, string B)? _selectedLink;
        private string _message;
        private bool _preview;
        private NodeContentLoadResult _content;
        private NodeTreeLoadResult _load;
        private List<ContentDiagnostic> _toolChecks = new List<ContentDiagnostic>();
        private ProgressState _previewProgress;

        public NodeTreeData Tree => catalog != null ? catalog.Tree : null;
        public bool Editing => !_preview;

        public IReadOnlyCollection<NodeData> Selection => SelectedNodes(Tree);

        [MenuItem("BlackHole/Node Tree")]
        private static void Open() => GetWindow<NodeTreeWindow>("Node Tree");

        private void OnEnable() => Undo.undoRedoPerformed += Rebuild;

        private void OnDisable() => Undo.undoRedoPerformed -= Rebuild;

        private void CreateGUI()
        {
            if (catalog == null)
                catalog = FindCatalog();

            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            var catalogField = new ObjectField { objectType = typeof(NodeCatalog), allowSceneObjects = false, value = catalog };
            catalogField.style.minWidth = 220;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                catalog = evt.newValue as NodeCatalog;
                ClearSelection();
                _previewOwned.Clear();
                Rebuild();
                _canvas.FrameAll();
            });
            toolbar.Add(catalogField);
            toolbar.Add(new ToolbarButton(() => _canvas.FrameAll()) { text = "전체 보기 (F)" });

            var previewToggle = new ToolbarToggle { text = "구매 미리보기" };
            previewToggle.RegisterValueChangedCallback(evt =>
            {
                _preview = evt.newValue;
                _previewOwned.Clear();
                Rebuild();
            });
            toolbar.Add(previewToggle);
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(Save) { text = "저장" });
            root.Add(toolbar);

            var split = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            _canvas = new NodeGridCanvas(this);
            left.Add(_canvas);
            var help = new Label(
                "미배치 노드를 고른 뒤 빈 칸 더블클릭: 놓기 · 클릭/박스: 고르기 (Ctrl: 더하기) · 끌기: 옮기기 · Shift+끌기: 잇기 · " +
                "선 클릭 후 Delete: 끊기 · 노드 고르고 Delete: 배치에서 빼기 · 휠: 확대 · 가운데/오른쪽 끌기: 이동");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.paddingLeft = 6;
            help.style.paddingTop = 2;
            help.style.paddingBottom = 2;
            left.Add(help);

            var side = new ScrollView();
            side.style.paddingLeft = 8;
            side.style.paddingRight = 8;
            _panel = new VisualElement();
            side.Add(_panel);

            _unplacedSection = new VisualElement();
            _unplacedHeader = Header("미배치 노드");
            _unplacedSection.Add(_unplacedHeader);
            var filter = new TextField("찾기") { value = _unplacedFilter };
            filter.RegisterValueChangedCallback(evt =>
            {
                _unplacedFilter = evt.newValue;
                BuildUnplaced();
            });
            _unplacedSection.Add(filter);
            _unplacedList = new VisualElement();
            _unplacedSection.Add(_unplacedList);
            side.Add(_unplacedSection);

            side.Add(Header("검사"));
            _diagnosticsList = new VisualElement();
            side.Add(_diagnosticsList);

            split.Add(left);
            split.Add(side);
            root.Add(split);

            Rebuild();
        }

        private static NodeCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(NodeCatalog));
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<NodeCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void Save()
        {
            if (catalog != null)
                AssetDatabase.SaveAssetIfDirty(catalog);
        }

        #region 고른 것

        private List<NodeData> SelectedNodes(NodeTreeData tree)
        {
            var nodes = new List<NodeData>(_selectedIds.Count);

            if (tree == null)
                return nodes;

            foreach (string id in _selectedIds)
            {
                NodeData node = NodeTreeAuthoring.Find(tree, id);

                if (node != null)
                    nodes.Add(node);
            }

            return nodes;
        }

        private void Select(NodeData node, bool additive)
        {
            _selectedLink = null;
            _message = null;

            if (!additive)
            {
                _selectedIds.Clear();
                _selectedIds.Add(node.Id);
            }
            else if (!_selectedIds.Remove(node.Id))
            {
                _selectedIds.Add(node.Id);
            }
        }

        private void ClearSelection()
        {
            _selectedIds.Clear();
            _selectedLink = null;
            _message = null;
        }

        private bool LinkIs(NodeData a, NodeData b) =>
            _selectedLink.HasValue
            && ((_selectedLink.Value.A == a.Id && _selectedLink.Value.B == b.Id) || (_selectedLink.Value.A == b.Id && _selectedLink.Value.B == a.Id));

        #endregion

        #region 다시 그리기

        // 트리가 바뀔 때마다: 콘텐츠·배치 불러 보기 → 도구 검사 → 미리보기 상태 → 고른 것 정리 → 캔버스·패널·진단.
        private void Rebuild()
        {
            if (_canvas == null)
                return;

            _nodesWithErrors.Clear();
            _toolChecks = new List<ContentDiagnostic>();
            _content = null;
            _load = null;

            if (catalog != null)
            {
                _content = catalog.Content != null ? catalog.Content.Load() : null;

                if (_content != null && _content.Succeeded)
                    _load = NodeTreeLoader.Load(catalog.ToData(), _content.Content);

                _toolChecks = NodeTreeAuthoring.Check(catalog.Tree);

                foreach (ContentDiagnostic diagnostic in Diagnostics(_load))
                    MarkError(diagnostic);

                foreach (ContentDiagnostic diagnostic in _toolChecks)
                    MarkError(diagnostic);

                ReplayPreview();
            }

            _selectedIds.RemoveAll(id => Tree == null || NodeTreeAuthoring.Find(Tree, id) == null);

            if (_selectedLink.HasValue)
            {
                NodeData a = Tree == null ? null : NodeTreeAuthoring.Find(Tree, _selectedLink.Value.A);
                NodeData b = Tree == null ? null : NodeTreeAuthoring.Find(Tree, _selectedLink.Value.B);

                if (a == null || b == null || !NodeTreeAuthoring.AreLinked(a, b))
                    _selectedLink = null;
            }

            _canvas.Refresh();
            BuildPanel();
            BuildUnplaced();
            BuildDiagnostics();
        }

        // 고른 것만 바뀌었을 때.
        private void RefreshSelection()
        {
            _canvas.Refresh();
            BuildPanel();
        }

        // 미리보기에서 산 Rank를 산 순서대로 다시 산다. 이제는 살 수 없는 Rank(숨었거나 없어진 노드)는 빠진다.
        private void ReplayPreview()
        {
            _previewProgress = new ProgressState();
            _previewProgress.EarnGold(PreviewGold);

            if (!_preview || _load?.Tree == null)
                return;

            var kept = new List<string>();

            foreach (string id in _previewOwned)
            {
                if (NodePurchase.TryPurchase(_previewProgress, _load.Tree, id) == PurchaseResult.Purchased)
                    kept.Add(id);
            }

            _previewOwned.Clear();
            _previewOwned.AddRange(kept);
        }

        private void MarkError(ContentDiagnostic diagnostic)
        {
            string id = NodeIdOf(diagnostic.Path);

            if (id != null)
                _nodesWithErrors.Add(id);
        }

        // "Nodes[a].Links[0]" → "a". 로더는 ID가 비어 있으면 순번을 쓴다.
        private string NodeIdOf(string path)
        {
            if (!path.StartsWith("Nodes[", StringComparison.Ordinal))
                return null;

            int end = path.IndexOf(']');

            if (end < 0)
                return null;

            string key = path.Substring(6, end - 6);

            if (Tree != null && NodeTreeAuthoring.Find(Tree, key) == null && int.TryParse(key, out int index)
                && index >= 0 && index < Tree.Nodes.Count)
                return Tree.Nodes[index].Id;

            return key;
        }

        #endregion

        #region 캔버스 입력

        public Color FillOf(NodeData node)
        {
            if (!_preview)
                return node.Start ? StartFill : NodeFill;

            if (_load?.Tree == null || !_load.Tree.TryGet(node.Id, out _))
                return HiddenFill;

            switch (NodePurchase.StateOf(_previewProgress, _load.Tree, node.Id))
            {
                case NodeState.Owned: return OwnedFill;
                case NodeState.Purchasable: return PurchasableFill;
                case NodeState.Revealed: return RevealedFill;
                default: return HiddenFill;
            }
        }

        public Color BorderOf(NodeData node)
        {
            if (!_preview && _selectedIds.Contains(node.Id))
                return SelectedBorder;

            if (node.Id != null && _nodesWithErrors.Contains(node.Id))
                return ErrorBorder;

            return node.Start ? StartBorder : PlainBorder;
        }

        public bool IsSelectedLink(NodeData a, NodeData b) => !_preview && LinkIs(a, b);

        // 노드 칸의 글자: ID와 Rank 1 비용(여러 Rank면 ×Rank 수). 미리보기에서는 산 Rank / 최대 Rank.
        public string LabelOf(NodeData node)
        {
            if (_content?.Content == null || !_content.Content.TryGetNode(node.Id, out NodeDefinition definition))
                return node.Id;

            if (_preview)
                return $"{node.Id}\n{_previewProgress.RankOf(node.Id)}/{definition.MaxRank}";

            string cost = Compact(definition.RankAt(1).Cost);
            return definition.MaxRank > 1 ? $"{node.Id}\n{cost} ×{definition.MaxRank}" : $"{node.Id}\n{cost}";
        }

        public void OnEmptyCellClicked(int x, int y, int clickCount, bool additive)
        {
            if (_preview)
                return;

            if (clickCount >= 2)
            {
                if (_placingId == null)
                {
                    ShowMessage("놓을 노드를 오른쪽 '미배치 노드' 목록에서 먼저 고른다.");
                    return;
                }

                string id = _placingId;
                string next = NextUnplacedAfter(id);

                Edit("노드 놓기", tree =>
                {
                    NodeData node = NodeTreeAuthoring.Place(tree, id, x, y);

                    if (node != null)
                    {
                        Select(node, false);
                        _placingId = next;
                    }
                });

                return;
            }

            if (!additive)
                ClearSelection();

            RefreshSelection();
        }

        public void OnNodeClicked(NodeData node, bool additive)
        {
            if (_preview)
            {
                if (_load?.Tree == null)
                    return;

                // 누르면 다음 Rank를 산다. Ctrl+클릭이나 마지막 Rank에서 다시 누르면 그 노드를 미리보기에서만 되돌린다(게임에는 환불이 없다).
                PurchaseResult result = NodePurchase.Check(_previewProgress, _load.Tree, node.Id);

                if (additive || result == PurchaseResult.MaxRankReached)
                    _previewOwned.RemoveAll(id => id == node.Id);
                else if (result == PurchaseResult.Purchased)
                    _previewOwned.Add(node.Id);

                Rebuild();
                return;
            }

            Select(node, additive);
            RefreshSelection();
        }

        public void OnLinkClicked(NodeData a, NodeData b)
        {
            if (_preview)
                return;

            _selectedIds.Clear();
            _message = null;
            _selectedLink = (a.Id, b.Id);
            RefreshSelection();
        }

        public void OnBoxSelected(List<NodeData> nodes, bool additive)
        {
            if (_preview)
                return;

            if (!additive)
                ClearSelection();

            _selectedLink = null;

            foreach (NodeData node in nodes)
            {
                if (!_selectedIds.Contains(node.Id))
                    _selectedIds.Add(node.Id);
            }

            RefreshSelection();
        }

        public void OnNodesDragged(IReadOnlyCollection<NodeData> nodes, int dx, int dy)
        {
            if (_preview)
                return;

            bool moved = false;
            Edit("노드 옮기기", tree => moved = NodeTreeAuthoring.MoveBy(tree, nodes, dx, dy));

            if (!moved)
                ShowMessage("옮길 칸에 다른 노드가 있어 옮기지 않았다.");
        }

        public void OnConnect(NodeData from, NodeData to)
        {
            if (_preview)
                return;

            Edit("선 잇기", _ => NodeTreeAuthoring.Link(from, to));
        }

        public void OnDeletePressed()
        {
            if (_preview)
                return;

            if (_selectedLink.HasValue)
            {
                NodeData a = NodeTreeAuthoring.Find(Tree, _selectedLink.Value.A);
                NodeData b = NodeTreeAuthoring.Find(Tree, _selectedLink.Value.B);

                if (a != null && b != null)
                    Edit("선 끊기", _ => NodeTreeAuthoring.Unlink(a, b));

                return;
            }

            List<NodeData> selected = SelectedNodes(Tree);

            if (selected.Count == 0)
                return;

            Edit("배치에서 빼기", tree =>
            {
                foreach (NodeData node in selected)
                    NodeTreeAuthoring.Remove(tree, node);
            });
        }

        // 모든 수정은 여기를 지난다: Undo 기록 → 수정 → 에셋 표시 → 다시 그리기.
        private void Edit(string undoName, Action<NodeTreeData> change)
        {
            Undo.RecordObject(catalog, undoName);
            change(catalog.Tree);
            EditorUtility.SetDirty(catalog);
            Rebuild();
        }

        private void ShowMessage(string message)
        {
            _message = message;
            BuildPanel();
        }

        #endregion

        #region 오른쪽 패널

        private void BuildPanel()
        {
            _panel.Clear();

            if (catalog == null)
            {
                _panel.Add(new HelpBox("노드 목록 에셋(NodeCatalog)을 고르세요.", HelpBoxMessageType.Info));
                return;
            }

            if (_preview)
            {
                BuildPreviewPanel();
                return;
            }

            if (_message != null)
                _panel.Add(new HelpBox(_message, HelpBoxMessageType.Warning));

            List<NodeData> selected = SelectedNodes(Tree);

            if (_selectedLink.HasValue)
                BuildLinkPanel();
            else if (selected.Count == 1)
                BuildNodePanel(selected[0]);
            else if (selected.Count > 1)
                BuildSelectionPanel(selected);
            else
                BuildSummary();
        }

        private void BuildNodePanel(NodeData node)
        {
            _panel.Add(Header("노드"));

            var id = new TextField("ID") { value = node.Id, isReadOnly = true };
            _panel.Add(id);
            _panel.Add(Note("ID는 노드 콘텐츠(Nodes 시트)의 것이고, 산 노드를 기록하는 저장 키다. 여기서는 바꾸지 않는다."));

            var start = new Toggle("시작 노드") { value = node.Start };
            start.RegisterValueChangedCallback(evt => Edit("시작 노드 바꾸기", tree => NodeTreeAuthoring.Find(tree, node.Id).Start = evt.newValue));
            _panel.Add(start);

            _panel.Add(new Label($"칸 ({node.X}, {node.Y})"));

            _panel.Add(Header("이어진 노드"));
            bool anyLink = false;

            foreach (NodeData other in Tree.Nodes)
            {
                if (other == node || !NodeTreeAuthoring.AreLinked(node, other))
                    continue;

                anyLink = true;
                NodeData target = other;
                var row = Row();
                row.Add(Grow(new Label(target.Id)));
                row.Add(new Button(() => Edit("선 끊기", _ => NodeTreeAuthoring.Unlink(node, target))) { text = "끊기" });
                _panel.Add(row);
            }

            if (!anyLink)
                _panel.Add(Note("없음. Shift+끌기로 다른 노드와 잇는다."));

            _panel.Add(Header("Rank"));
            BuildRanks(node.Id);
            _panel.Add(Note("Rank·비용·효과는 데이터 시트(Nodes·NodeCost·NodeEffects 탭)에서 고치고, 내려받은 CSV를 Assets/Data/NodeTable에 넣는다."));

            var remove = new Button(OnDeletePressed) { text = "배치에서 빼기" };
            remove.style.marginTop = 12;
            _panel.Add(remove);
        }

        // 노드 콘텐츠의 Rank마다: 비용과 효과.
        private void BuildRanks(string id)
        {
            if (_content?.Content == null)
            {
                _panel.Add(Note("노드 콘텐츠를 불러오지 못했다. 아래 검사를 본다."));
                return;
            }

            if (!_content.Content.TryGetNode(id, out NodeDefinition definition))
            {
                _panel.Add(new HelpBox($"노드 콘텐츠(Nodes 시트)에 없는 ID다: '{id}'.", HelpBoxMessageType.Error));
                return;
            }

            foreach (NodeRankDefinition rank in definition.Ranks)
            {
                var effects = new List<string>();

                foreach (NodeEffect effect in rank.Effects)
                {
                    UpgradeStatDefinition stat = _content.Content.StatOf(effect.Stat);
                    effects.Add($"{stat.StatId} {NodeTreeAuthoring.Notation(stat.Unit, effect.Value)}");
                }

                _panel.Add(new Label($"Rank {rank.Rank} · {rank.Cost.ToString("N0", CultureInfo.InvariantCulture)} · {string.Join(", ", effects)}"));
            }
        }

        // 여러 노드를 골랐을 때: 저작 명령.
        private void BuildSelectionPanel(List<NodeData> selected)
        {
            _panel.Add(Header($"노드 {selected.Count}개 고름"));

            var connectTwo = new Button(() => Edit("둘 잇기", _ => NodeTreeAuthoring.Link(selected[0], selected[1]))) { text = "둘 잇기" };
            connectTwo.SetEnabled(selected.Count == 2);
            _panel.Add(connectTwo);

            _panel.Add(new Button(() => Command("이웃끼리 잇기", tree => NodeTreeAuthoring.LinkNeighbors(selected), "선 {0}개를 이었다.")) { text = "이웃끼리 잇기" });
            _panel.Add(Note("고른 노드 가운데 상하좌우로 붙은 쌍을 지금 잇는다. 나중에 옮겨도 선은 그대로다."));
            _panel.Add(new Button(() => Command("선택끼리 끊기", tree => NodeTreeAuthoring.UnlinkAmong(selected), "선 {0}개를 끊었다.")) { text = "선택끼리 끊기" });
            _panel.Add(new Button(() => Command("선 모두 지우기", tree => NodeTreeAuthoring.ClearLinks(tree, selected), "선 {0}개를 지웠다.")) { text = "선 모두 지우기" });
            _panel.Add(Note("선택끼리 끊기: 고른 노드 사이의 선만. 선 모두 지우기: 고른 노드에 닿은 선 전부."));

            var remove = new Button(OnDeletePressed) { text = "배치에서 빼기" };
            remove.style.marginTop = 12;
            _panel.Add(remove);
        }

        private void Command(string undoName, Func<NodeTreeData, int> change, string resultFormat)
        {
            int count = 0;
            Edit(undoName, tree => count = change(tree));
            ShowMessage(string.Format(CultureInfo.InvariantCulture, resultFormat, count));
        }

        private void BuildLinkPanel()
        {
            (string a, string b) = _selectedLink.Value;
            _panel.Add(Header("선"));
            _panel.Add(new Label($"{a} — {b}"));
            _panel.Add(new Button(OnDeletePressed) { text = "끊기 (Delete)" });
        }

        private void BuildSummary()
        {
            int starts = 0;

            foreach (NodeData node in Tree.Nodes)
            {
                if (node.Start)
                    starts++;
            }

            _panel.Add(Header("트리"));
            string content = _content?.Content != null ? $" / 콘텐츠 {_content.Content.Nodes.Count}개" : string.Empty;
            _panel.Add(new Label($"배치한 노드 {Tree.Nodes.Count}개{content} · 시작 노드 {starts}개 · 선 {NodeTreeAuthoring.Links(Tree).Count}개"));
            _panel.Add(Note("아래 '미배치 노드'에서 노드를 고르고 빈 칸을 더블클릭하면 놓는다. 노드를 여럿 고르면(박스·Ctrl+클릭) 이웃끼리 잇기 같은 명령이 나온다."));
        }

        // 미배치 노드: 콘텐츠에는 있지만 아직 놓지 않은 노드(콘텐츠 순서). 하나를 고르면 빈 칸 더블클릭으로 놓는다.
        // 편집 중이고 콘텐츠를 불러왔을 때만 보인다.
        private void BuildUnplaced()
        {
            _unplacedList.Clear();
            bool shown = catalog != null && !_preview && _content?.Content != null;
            _unplacedSection.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

            if (!shown)
                return;

            List<string> unplaced = UnplacedIds();

            if (_placingId != null && !unplaced.Contains(_placingId))
                _placingId = null;

            _unplacedHeader.text = $"미배치 노드 {unplaced.Count}개";

            if (unplaced.Count == 0)
            {
                _unplacedList.Add(Note("콘텐츠의 노드를 모두 놓았다."));
                return;
            }

            _unplacedList.Add(Note(_placingId == null
                ? "놓을 노드를 누른 뒤 빈 칸을 더블클릭한다."
                : $"놓을 노드: {_placingId}. 빈 칸을 더블클릭한다. 놓으면 목록의 다음 노드로 넘어간다. 다시 누르면 고르기를 푼다."));

            foreach (string id in unplaced)
            {
                if (_unplacedFilter.Length > 0 && id.IndexOf(_unplacedFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string target = id;
                var button = new Button(() =>
                {
                    _placingId = _placingId == target ? null : target;
                    BuildUnplaced();
                })
                {
                    text = UnplacedLabel(target),
                };

                button.style.unityTextAlign = TextAnchor.MiddleLeft;

                if (target == _placingId)
                    button.style.backgroundColor = PlacingFill;

                _unplacedList.Add(button);
            }
        }

        private List<string> UnplacedIds()
        {
            var ids = new List<string>(_content.Content.Nodes.Count);

            foreach (NodeDefinition node in _content.Content.Nodes)
                ids.Add(node.Id);

            return NodeTreeAuthoring.Unplaced(Tree, ids);
        }

        // 놓은 뒤 고를 다음 노드: 미배치 목록(콘텐츠 순서)에서 id 바로 다음. 없으면 null.
        private string NextUnplacedAfter(string id)
        {
            List<string> unplaced = UnplacedIds();
            int index = unplaced.IndexOf(id);
            return index >= 0 && index + 1 < unplaced.Count ? unplaced[index + 1] : null;
        }

        private string UnplacedLabel(string id)
        {
            if (!_content.Content.TryGetNode(id, out NodeDefinition node))
                return id;

            string cost = Compact(node.RankAt(1).Cost);
            return node.MaxRank > 1 ? $"{id}  ·  {cost} ×{node.MaxRank}" : $"{id}  ·  {cost}";
        }

        // 미리보기: 산 노드 수, 쓴 Gold, 수치별 값(기본값 → 지금 값).
        private void BuildPreviewPanel()
        {
            _panel.Add(Header("구매 미리보기"));

            if (_load?.Tree == null)
            {
                _panel.Add(new HelpBox("트리를 불러오지 못해 미리볼 수 없다. 아래 검사를 먼저 고친다.", HelpBoxMessageType.Error));
                return;
            }

            int owned = 0;
            int ranks = 0;

            foreach (NodeDefinition node in _load.Tree.Nodes)
            {
                int rank = _previewProgress.RankOf(node.Id);
                ranks += rank;

                if (rank > 0)
                    owned++;
            }

            long spent = PreviewGold - _previewProgress.Gold;
            _panel.Add(new Label($"산 노드 {owned} / {_load.Tree.Nodes.Count} · 산 Rank {ranks} · 쓴 Gold {spent.ToString("N0", CultureInfo.InvariantCulture)}"));
            _panel.Add(Note("노랑: 다음 Rank를 살 수 있음 · 초록: 마지막 Rank까지 삼 · 회색: 보이지만 못 삼 · 어두움: 숨김. " +
                "누를 때마다 다음 Rank를 산다. Ctrl+클릭이나 마지막 Rank에서 다시 누르면 그 노드를 미리보기에서만 되돌린다(게임에는 환불이 없다)."));

            UpgradeStatValues values = NodePurchase.StatsFor(_previewProgress, _load.Tree);
            _panel.Add(Header("수치 (기본값 → 지금 값)"));
            bool any = false;

            foreach (UpgradeStatDefinition stat in values.Stats)
            {
                if (values.SumOf(stat.Stat) == 0)
                    continue;

                any = true;
                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                _panel.Add(new Label($"{stat.StatId}: {Number(stat.DefaultValue)}{unit} → {Number(values.ValueOf(stat.Stat))}{unit}"));
            }

            if (!any)
                _panel.Add(Note("산 노드가 없다."));
        }

        private void BuildDiagnostics()
        {
            _diagnosticsList.Clear();

            if (catalog == null)
                return;

            if (catalog.Content == null)
            {
                _diagnosticsList.Add(new HelpBox("노드 목록(NodeCatalog)에 노드 콘텐츠(NodeContentSource)를 연결해야 한다.", HelpBoxMessageType.Error));
                return;
            }

            if (!_content.Succeeded)
            {
                _diagnosticsList.Add(new Label("노드 콘텐츠(시트 CSV)를 불러오지 못했다:") { style = { color = ErrorText } });

                foreach (ContentDiagnostic diagnostic in _content.Diagnostics)
                    _diagnosticsList.Add(DiagnosticButton(diagnostic, ErrorText));

                return;
            }

            if (_load.Unplaced.Count > 0)
                _diagnosticsList.Add(Note($"배치하지 않은 노드 {_load.Unplaced.Count}개는 게임 트리에서 빠진다."));

            if (_load.Diagnostics.Count == 0 && _toolChecks.Count == 0)
            {
                _diagnosticsList.Add(new Label("문제 없음") { style = { color = new Color(0.5f, 0.9f, 0.5f) } });
                return;
            }

            foreach (ContentDiagnostic diagnostic in _load.Diagnostics)
                _diagnosticsList.Add(DiagnosticButton(diagnostic, ErrorText));

            foreach (ContentDiagnostic diagnostic in _toolChecks)
                _diagnosticsList.Add(DiagnosticButton(diagnostic, WarningText));
        }

        private Button DiagnosticButton(ContentDiagnostic diagnostic, Color color)
        {
            var button = new Button(() =>
            {
                NodeData node = NodeTreeAuthoring.Find(Tree, NodeIdOf(diagnostic.Path));

                if (node == null)
                    return;

                Select(node, false);
                _canvas.CenterOn(node);
                BuildPanel();
            })
            {
                text = diagnostic.ToString(),
            };

            button.style.color = color;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            return button;
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.7f;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static T Grow<T>(T element) where T : VisualElement
        {
            element.style.flexGrow = 1;
            return element;
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static IReadOnlyList<ContentDiagnostic> Diagnostics(NodeTreeLoadResult load) =>
            load != null ? load.Diagnostics : Array.Empty<ContentDiagnostic>();

        // 큰 비용을 노드 칸에 맞게 줄인다: 1,500 → 1.5K, 2,600,000,000,000 → 2.6T.
        private static string Compact(long value)
        {
            string[] units = { string.Empty, "K", "M", "B", "T", "Qa", "Qi" };
            double scaled = value;
            int unit = 0;

            while (Math.Abs(scaled) >= 1000 && unit < units.Length - 1)
            {
                scaled /= 1000;
                unit++;
            }

            return scaled.ToString("0.##", CultureInfo.InvariantCulture) + units[unit];
        }

        #endregion
    }
}
