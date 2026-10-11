using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 테스트 세팅(메뉴 BlackHole > Test Setup). 정확한 노드 Rank와 이정표 단계·블랙홀 Level을 정하고 그 상태로 바로 플레이한다.
    // 노드와 이정표 단계만으로 적·Breaker·스탯이 모두 정해지므로, 창은 그 둘(과 판 안의 시작 Level)만 고치고
    // 나머지는 게임과 같은 길로 판을 조립해 "확정 정보"로 보여 준다(TestSetupPreview).
    //
    // - 노드: 트리에서 클릭하면 Rank +1(마지막 Rank에서 누르면 0), Ctrl+클릭 Rank −1, Delete는 고른 노드를 0으로.
    //   게임의 드러남 규칙은 따지지 않는다(정확한 상태를 만드는 도구다). 게임 순서로는 살 수 없는 노드는 주황 테두리로 알린다.
    // - 이정표 단계 → 시작·목표 Level이 정해지고, 시작 Level은 그 사이에서 고른다(EXP를 채워 원래 길로 Level업한다).
    // - 저장: Assets/Playtest/Scenarios의 시나리오 JSON(정확한 노드 목록). 개발 패널의 시나리오 탭에서도 같은 파일을 쓴다.
    // - ▶ 플레이: 세팅을 맡기고 플레이 모드에 들어가면 판이 바로 그 세팅으로 시작한다. 플레이 중에는 "지금 판에 적용"으로 다시 시작한다.
    // - 밸런스 프로필: 고른 프로필을 적용한 값으로 미리보고 플레이한다(바꾸면 다음 플레이부터).
    // - 메모: 느낌·고칠 방향은 따로 "플레이 메모" 창(PlaytestNotesWindow)에 적는다. 플레이 중이 아니면 이 창의 세팅이 메모 대상이다.
    // 창의 세팅은 Undo(Ctrl+Z)로 되돌린다.
    internal sealed class TestSetupWindow : EditorWindow, INodeCanvasHost
    {
        private const string ScenariosFolder = PlaytestLibrary.ScenariosFolder;
        private const int MaxSearchResults = 40;

        private static readonly Color OffFill = new Color(0.3f, 0.3f, 0.32f);
        private static readonly Color PartFill = new Color(0.2f, 0.42f, 0.5f);
        private static readonly Color MaxFill = new Color(0.2f, 0.5f, 0.25f);
        private static readonly Color MissingFill = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color PlainBorder = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color StartBorder = new Color(0.4f, 0.85f, 0.95f);
        private static readonly Color FocusBorder = Color.white;
        private static readonly Color UnreachableBorder = new Color(1f, 0.6f, 0.15f);
        private static readonly Color DirtyText = new Color(1f, 0.85f, 0.45f);

        [Serializable]
        private sealed class RankEntry
        {
            public string id;
            public int rank;
        }

        // 세팅. 창이 직렬화하므로 Undo와 플레이 모드 진입(도메인 다시 불러오기)을 넘긴다.
        [SerializeField] private string _filePath = string.Empty;
        [SerializeField] private string _setupName = "new-setup";
        [SerializeField] private string _note = string.Empty;
        [SerializeField] private int _stage;
        [SerializeField] private int _level;
        [SerializeField] private long _gold;
        [SerializeField] private int _seed = 1;
        [SerializeField] private string _category = "기본";
        [SerializeField] private string _expected = "";
        [SerializeField] private string _savedFingerprint = "";
        [SerializeField] private string _categoryFilter = "전체";
        [SerializeField] private string _nodeGroup = "전체";
        [SerializeField] private bool _freezeTime;
        [SerializeField] private List<RankEntry> _ranks = new List<RankEntry>();
        // 마지막으로 저장하거나 불러온 내용. 지금 세팅과 다르면 제목에 *를 붙인다.
        [SerializeField] private string _savedJson = string.Empty;
        [SerializeField] private string _focusedId;
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private long _budget = 60000;

        // 열려 있는 창(하나). 플레이 메모 창이 메모 대상 세팅을 여기서 읽는다.
        private static TestSetupWindow _open;

        // 세팅이 바뀌어 다시 계산했다(플레이 메모 창이 대상과 목록을 고친다).
        internal static event Action SetupChanged;

        private GameContentSetup _contentSetup;
        private LoadedContent _loaded;
        private PlaytestSession _playtest;
        private TestSetupReport _report;
        private string _message;

        private NodeGridCanvas _canvas;
        private Label _title;
        private VisualElement _fileBox;
        private VisualElement _profileBox;
        private VisualElement _playBox;
        private VisualElement _fieldsBox;
        private VisualElement _nodesBox;
        private VisualElement _searchResults;
        private VisualElement _focusBox;
        private VisualElement _messagesBox;
        private VisualElement _reportBox;

        public NodeTreeData Tree => _contentSetup != null && _contentSetup.Nodes != null ? _contentSetup.Nodes.Tree : null;
        public bool Editing => false;

        public IReadOnlyCollection<NodeData> Selection
        {
            get
            {
                NodeData node = Tree == null || _focusedId == null ? null : NodeTreeAuthoring.Find(Tree, _focusedId);
                return node != null ? new[] { node } : Array.Empty<NodeData>();
            }
        }

        [MenuItem("BlackHole/Test Setup")]
        private static void Open() => GetWindow<TestSetupWindow>("Test Setup");

        private void OnEnable()
        {
            _open = this;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            LiveDataSignal.Changed += OnLiveDataChanged;

            // 플레이에 들어가지 못해(컴파일 오류 등) 남은 세팅이 다음 보통 플레이에서 실행되지 않게 지운다.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                TestSetupLaunch.Take();
        }

        private void OnDisable()
        {
            if (_open == this)
                _open = null;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            LiveDataSignal.Changed -= OnLiveDataChanged;
        }

        // 수치 파일이 바뀌었다(LiveDataWatcher가 게임 로더로 검사한 뒤). 창에 다시 들어오지 않아도 바로 다시 계산한다.
        private void OnLiveDataChanged(LiveDataChange change)
        {
            if (_canvas == null)
                return;

            if (!change.ContentChanged)
            {
                // 시나리오 파일만 바뀌었다: 불러오기 목록만 고친다(열어 둔 세팅은 그대로).
                BuildFile();
                return;
            }

            if (!change.Valid)
            {
                var shown = new List<string>();
                for (int i = 0; i < change.Errors.Count && i < 5; i++)
                    shown.Add(change.Errors[i]);

                _message = $"수치 오류 {change.Errors.Count}개({change.Files}) — 확정 정보는 이전 값이다.\n" + string.Join("\n", shown);
                BuildMessages();
                return;
            }

            string before = _playtest != null ? _playtest.Fingerprint : string.Empty;
            LoadContent();
            Recompute();

            if (before != change.Fingerprint)
            {
                _message = $"수치 바뀜: {change.Files} · 지문 {before} → {change.Fingerprint}";
                BuildMessages();
            }
        }

        // 시트(CSV)나 프로필을 고치고 돌아오면 다시 불러온다.
        private void OnFocus()
        {
            if (_canvas == null)
                return;

            LoadContent();
            Recompute();
        }

        private void OnUndoRedo()
        {
            if (_canvas == null)
                return;

            BuildFields();
            Recompute();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // 플레이에 들어가지 못했으면(컴파일 오류 등) 맡긴 세팅이 다음 플레이에 남지 않게 지운다.
            if (change == PlayModeStateChange.EnteredEditMode)
                TestSetupLaunch.Take();

            if (_canvas != null)
            {
                BuildProfile();
                BuildPlay();
            }
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => _canvas.FrameAll()) { text = "전체 보기 (F)" });
            toolbar.Add(new ToolbarButton(() => { LoadContent(); Recompute(); }) { text = "다시 불러오기" });
            toolbar.Add(new ToolbarButton(PlaytestNotesWindow.Open) { text = "메모 창" });
            toolbar.Add(new ToolbarButton(BalanceProfileWindow.Open) { text = "프로필 창" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            _title = new Label();
            _title.style.unityTextAlign = TextAnchor.MiddleRight;
            _title.style.paddingRight = 6;
            toolbar.Add(_title);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(1, 420, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            _canvas = new NodeGridCanvas(this);
            left.Add(_canvas);
            var help = new Label(
                "클릭: Rank +1 (마지막 Rank에서 누르면 0) · Ctrl+클릭: Rank −1 · Delete: 고른 노드 0 · 휠: 확대 · 가운데/오른쪽 끌기: 이동 · F: 전체 보기 · " +
                "초록: 마지막 Rank · 청록: 일부 Rank · 주황 테두리: 게임 순서로는 못 삼");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.paddingLeft = 6;
            help.style.paddingTop = 2;
            help.style.paddingBottom = 2;
            left.Add(help);

            var side = new ScrollView();
            side.style.paddingLeft = 8;
            side.style.paddingRight = 8;
            _fileBox = Box(side);
            _profileBox = Box(side);
            _playBox = Box(side);
            _messagesBox = Box(side);
            side.Add(Header("세팅"));
            _fieldsBox = Box(side);
            side.Add(Header("노드"));
            _nodesBox = Box(side);
            var search = new TextField("찾기") { value = _search };
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue;
                BuildSearch();
            });
            side.Add(search);
            _searchResults = Box(side);
            _focusBox = Box(side);
            side.Add(Header("확정 정보"));
            side.Add(Note("노드와 이정표 단계로 정해진 이 판의 값이다. 게임과 같은 길로 판을 조립해 읽었다."));
            _reportBox = Box(side);

            split.Add(left);
            split.Add(side);
            root.Add(split);

            LoadContent();
            BuildFile();
            BuildFields();
            Recompute();
        }

        #region 콘텐츠

        // 게임과 같은 로더로 콘텐츠를 불러온다. 고른 밸런스 프로필을 적용하고, 검증에 실패하면 원본으로 다시 불러온다.
        private void LoadContent()
        {
            _contentSetup = FindAsset<GameContentSetup>();
            _loaded = null;
            _playtest = PlaytestSession.Create(FindAsset<PlaytestLibrary>(), new ContentTag());

            if (_contentSetup == null)
            {
                _message = "게임 콘텐츠 세트(GameContentSetup) 에셋을 찾지 못했다.";
                return;
            }

            _loaded = GameContentLoader.Load(_contentSetup, _playtest.Patch);

            if (_loaded == null && _playtest.AppliedProfile != null)
            {
                _playtest.RejectProfile();
                _loaded = GameContentLoader.Load(_contentSetup);
            }

            if (_loaded == null)
                _message = "콘텐츠를 불러오지 못했다. 콘솔의 [콘텐츠]·[노드 콘텐츠] 오류를 본다.";
            else if (_message != null && _message.StartsWith("콘텐츠를", StringComparison.Ordinal))
                _message = null;

            if (_profileBox != null)
            {
                BuildProfile();
                BuildPlay();
            }
        }

        private static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private HqGrowthDefinition Growth => _loaded?.Content.Growth;
        private NodeTree NodeTree => _loaded?.NodeTree;

        #endregion

        #region 세팅 ↔ 시나리오

        private int RankOf(string id)
        {
            foreach (RankEntry entry in _ranks)
            {
                if (entry.id == id)
                    return entry.rank;
            }

            return 0;
        }

        private int MaxRankOf(string id) =>
            NodeTree != null && NodeTree.TryGet(id, out NodeDefinition node) ? node.MaxRank : 0;

        private void SetRank(string id, int rank)
        {
            _ranks.RemoveAll(entry => entry.id == id);

            if (rank > 0)
                _ranks.Add(new RankEntry { id = id, rank = rank });
        }

        // 지금 세팅을 시나리오(저장 형식)로. 노드는 트리 순서로 적는다(파일 비교가 쉽게).
        private PlaytestScenario ToScenario()
        {
            var scenario = new PlaytestScenario
            {
                name = _setupName,
                note = _note,
                category = _category,
                expected = _expected,
                contentFingerprint = _savedFingerprint,
                growthStage = _stage,
                gold = _gold,
                startLevel = _level,
                seed = _seed,
                freezeTime = _freezeTime,
            };

            var written = new HashSet<string>(StringComparer.Ordinal);

            if (NodeTree != null)
            {
                foreach (NodeDefinition node in NodeTree.Nodes)
                {
                    int rank = RankOf(node.Id);

                    if (rank > 0 && written.Add(node.Id))
                        scenario.nodes.Add(new PlaytestScenario.Node { nodeId = node.Id, rank = rank });
                }
            }

            // 지금 콘텐츠에 없는 노드도 그대로 남긴다(지우지 않는다). 확정 정보가 "맞춰 씀"으로 알린다.
            foreach (RankEntry entry in _ranks)
            {
                if (entry.rank > 0 && written.Add(entry.id))
                    scenario.nodes.Add(new PlaytestScenario.Node { nodeId = entry.id, rank = entry.rank });
            }

            return scenario;
        }

        // 시나리오를 창의 세팅으로. 자동 구매가 있으면 게임 규칙으로 산 결과(정확한 Rank)로 푼다.
        private void FromScenario(PlaytestScenario scenario, string path)
        {
            _filePath = path ?? string.Empty;
            _setupName = scenario.name;
            _note = scenario.note ?? string.Empty;
            _category = string.IsNullOrEmpty(scenario.category) ? "기본" : scenario.category;
            _expected = scenario.expected ?? "";
            _savedFingerprint = scenario.contentFingerprint ?? "";
            _stage = scenario.growthStage;
            _gold = scenario.gold;
            _seed = scenario.seed;
            _freezeTime = scenario.freezeTime;
            _ranks.Clear();
            _message = null;

            if (scenario.autoBuyBudget > 0 && NodeTree != null)
            {
                TestSetupReport resolved = TestSetupPreview.Build(scenario, _loaded.Content, NodeTree);

                if (resolved.Succeeded)
                {
                    foreach (NodeDefinition node in NodeTree.Nodes)
                        SetRank(node.Id, resolved.Progress.RankOf(node.Id));

                    _message = $"자동 구매({scenario.autoBuyBudget:N0} Gold)를 산 노드 목록으로 풀었다. 저장하면 정확한 노드 목록으로 바뀐다.";
                }
            }
            else
            {
                foreach (PlaytestScenario.Node node in scenario.nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.nodeId))
                        SetRank(node.nodeId, node.rank);
                }
            }

            _level = scenario.startLevel > 0 ? scenario.startLevel : StartLevel(_stage);
            ClampLevel();
            _savedJson = path != null ? JsonUtility.ToJson(scenario) : string.Empty;
        }

        private int StartLevel(int stage) => Growth != null ? Growth.StartLevelAt(Math.Min(stage, Growth.MaxStage)) : 0;

        // 이 단계에서 시작할 수 있는 마지막 Level: 목표 Level 바로 아래(목표가 없으면 사다리 끝).
        private int LastLevel(int stage)
        {
            if (Growth == null)
                return 0;

            int goal = Growth.GoalLevelAt(Math.Min(stage, Growth.MaxStage));
            return goal != HqGrowthDefinition.NoGoal ? goal - 1 : Growth.MaxLevel;
        }

        private void ClampLevel()
        {
            if (Growth == null)
                return;

            _stage = Mathf.Clamp(_stage, HqGrowthDefinition.StartStage, Growth.MaxStage);
            _level = Mathf.Clamp(_level, StartLevel(_stage), Mathf.Max(StartLevel(_stage), LastLevel(_stage)));
        }

        private bool IsDirty => JsonUtility.ToJson(ToScenario()) != _savedJson;

        #endregion

        #region 다시 계산·그리기

        // 세팅이 바뀔 때마다: 확정 정보 → 캔버스 → 노드 요약·찾기·고른 노드·알림·확정 정보 칸.
        private void Recompute()
        {
            if (_canvas == null)
                return;

            _report = _loaded != null ? TestSetupPreview.Build(ToScenario(), _loaded.Content, NodeTree) : null;
            if (_report != null && !string.IsNullOrEmpty(_savedFingerprint) && _savedFingerprint != _playtest?.Fingerprint)
                _report.Warnings.Add("저장 당시와 콘텐츠 수치가 다릅니다. 같은 노드 조합도 전력이 달라질 수 있습니다.");
            _canvas.Refresh();
            UpdateTitle();
            BuildNodes();
            BuildSearch();
            BuildFocus();
            BuildMessages();
            BuildReport();
            SetupChanged?.Invoke();
        }

        private void UpdateTitle()
        {
            if (_title == null)
                return;

            bool dirty = IsDirty;
            string file = string.IsNullOrEmpty(_filePath) ? "저장 안 함" : Path.GetFileName(_filePath);
            string fingerprint = _playtest != null && _playtest.Fingerprint.Length > 0 ? $"  ·  지문 {_playtest.Fingerprint}" : string.Empty;
            _title.text = $"{_setupName}{(dirty ? " *" : string.Empty)}  ·  {file}{fingerprint}";
            _title.style.color = dirty ? new StyleColor(DirtyText) : new StyleColor(StyleKeyword.Null);
            titleContent = new GUIContent(dirty ? "Test Setup *" : "Test Setup");
        }

        private void Change(string undoName, Action change, bool rebuildFields = false)
        {
            Undo.RecordObject(this, undoName);
            change();
            ClampLevel();

            if (rebuildFields)
                BuildFields();

            Recompute();
        }

        #endregion

        #region 캔버스

        public Color FillOf(NodeData node)
        {
            int max = MaxRankOf(node.Id);

            if (max == 0)
                return MissingFill;

            int rank = RankOf(node.Id);
            return rank <= 0 ? OffFill : rank >= max ? MaxFill : PartFill;
        }

        public Color BorderOf(NodeData node)
        {
            if (node.Id == _focusedId)
                return FocusBorder;

            if (_report != null && _report.Unreachable.Contains(node.Id))
                return UnreachableBorder;

            return node.Start ? StartBorder : PlainBorder;
        }

        public bool IsSelectedLink(NodeData a, NodeData b) => false;

        public string LabelOf(NodeData node)
        {
            int max = MaxRankOf(node.Id);
            return max == 0 ? node.Id : $"{node.Id}\n{RankOf(node.Id)}/{max}";
        }

        // 클릭: Rank +1(마지막 Rank에서 누르면 0). Ctrl+클릭: Rank −1.
        public void OnNodeClicked(NodeData node, bool additive)
        {
            int max = MaxRankOf(node.Id);
            _focusedId = node.Id;

            if (max == 0)
            {
                Recompute();
                return;
            }

            int rank = RankOf(node.Id);
            int next = additive ? Math.Max(0, rank - 1) : rank >= max ? 0 : rank + 1;
            Change("노드 Rank", () => SetRank(node.Id, next));
        }

        public void OnDeletePressed()
        {
            if (_focusedId != null && RankOf(_focusedId) > 0)
                Change("노드 Rank 0", () => SetRank(_focusedId, 0));
        }

        public void OnEmptyCellClicked(int x, int y, int clickCount, bool additive) { }
        public void OnLinkClicked(NodeData a, NodeData b) { }
        public void OnBoxSelected(List<NodeData> nodes, bool additive) { }
        public void OnNodesDragged(IReadOnlyCollection<NodeData> nodes, int dx, int dy) { }
        public void OnConnect(NodeData from, NodeData to) { }

        #endregion

        #region 파일

        private void BuildFile()
        {
            _fileBox.Clear();
            _fileBox.Add(Header("세팅 파일"));

            var row = Row();
            row.Add(new Button(NewSetup) { text = "새로" });

            List<string> files = ScenarioFiles();
            var categories = new List<string> { "전체" };
            foreach (string path in files)
            {
                var item = PlaytestScenario.Parse(File.ReadAllText(path), out _);
                string group = string.IsNullOrEmpty(item?.category) ? "기본" : item.category;
                if (!categories.Contains(group)) categories.Add(group);
            }
            if (!categories.Contains(_categoryFilter)) _categoryFilter = "전체";
            var filter = new DropdownField("분류", categories, categories.IndexOf(_categoryFilter));
            filter.RegisterValueChangedCallback(evt => { _categoryFilter = evt.newValue; BuildFile(); });
            _fileBox.Add(filter);
            if (_categoryFilter != "전체") files.RemoveAll(path => {
                var item = PlaytestScenario.Parse(File.ReadAllText(path), out _);
                return (string.IsNullOrEmpty(item?.category) ? "기본" : item.category) != _categoryFilter;
            });
            var choices = new List<string> { "불러오기…" };
            foreach (string path in files)
                choices.Add(Path.GetFileNameWithoutExtension(path));

            var open = new DropdownField(choices, 0);
            open.style.flexGrow = 1;
            open.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue) - 1;

                if (index >= 0)
                    Load(files[index]);
            });
            row.Add(open);
            row.Add(new Button(Save) { text = "저장" });
            row.Add(new Button(SaveAs) { text = "다른 이름으로" });
            row.Add(new Button(Revert) { text = "되돌리기" });
            _fileBox.Add(row);
            _fileBox.Add(Note($"{ScenariosFolder}의 시나리오 JSON이다. 개발 패널의 시나리오 탭에서도 같은 파일이 보인다."));
        }

        private static List<string> ScenarioFiles()
        {
            var files = new List<string>();

            if (!Directory.Exists(ScenariosFolder))
                return files;

            foreach (string path in Directory.GetFiles(ScenariosFolder, "*.json"))
                files.Add(path.Replace('\\', '/'));

            files.Sort(StringComparer.Ordinal);
            return files;
        }

        private void NewSetup()
        {
            Undo.RecordObject(this, "새 세팅");
            FromScenario(new PlaytestScenario { name = "new-setup" }, null);
            BuildFields();
            Recompute();
        }

        private void Load(string path)
        {
            PlaytestScenario scenario = PlaytestScenario.Parse(File.ReadAllText(path), out string error);

            if (scenario == null)
            {
                _message = $"{Path.GetFileName(path)}을 읽지 못했다: {error}";
                BuildMessages();
                BuildFile();
                return;
            }

            if (_loaded == null || NodeTree == null)
            {
                _message = "콘텐츠를 먼저 불러와야 세팅을 검사할 수 있습니다.";
                BuildMessages();
                return;
            }
            TestSetupReport check = TestSetupPreview.Build(scenario, _loaded.Content, NodeTree);
            if (!check.Succeeded)
            {
                _message = "세팅을 바꾸지 않았습니다: " + string.Join(" / ", check.Errors);
                BuildMessages();
                return;
            }
            Undo.RecordObject(this, "세팅 불러오기");
            FromScenario(scenario, path);
            BuildFile();
            BuildFields();
            Recompute();
        }

        private void Revert()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                NewSetup();
                return;
            }

            Load(_filePath);
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                SaveAs();
                return;
            }

            Write(_filePath);
        }

        private void SaveAs()
        {
            Directory.CreateDirectory(ScenariosFolder);
            string path = EditorUtility.SaveFilePanelInProject("테스트 세팅 저장", _setupName, "json", "시나리오 JSON으로 저장한다.", ScenariosFolder);

            if (!string.IsNullOrEmpty(path))
                Write(path);
        }

        private void Write(string path)
        {
            PlaytestScenario scenario = ToScenario();

            if (string.IsNullOrEmpty(scenario.name) || scenario.name.Length > PlaytestScenario.MaxNameLength)
            {
                _message = $"세팅 이름은 1~{PlaytestScenario.MaxNameLength}자다.";
                BuildMessages();
                return;
            }

            if (_report == null || !_report.Succeeded)
            {
                _message = "유효한 세팅만 저장할 수 있습니다. 콘텐츠·노드 오류를 먼저 확인하세요.";
                BuildMessages();
                return;
            }
            if (scenario.seed == 0 && !EditorUtility.DisplayDialog("무작위 시드", "시드가 0이면 매번 배치가 달라집니다. 재현용은 고정 시드를 사용하세요.", "그대로 저장", "취소")) return;
            _savedFingerprint = _playtest?.Fingerprint ?? "";
            scenario.contentFingerprint = _savedFingerprint;
            File.WriteAllText(path, JsonUtility.ToJson(scenario, true) + "\n");
            AssetDatabase.ImportAsset(path);
            Undo.RecordObject(this, "세팅 저장");
            _filePath = path;
            _savedJson = JsonUtility.ToJson(scenario);
            _message = $"{path}에 저장했다.";
            BuildFile();
            Recompute();
        }

        #endregion

        #region 프로필·플레이

        private void BuildProfile()
        {
            _profileBox.Clear();
            _profileBox.Add(Header("밸런스 프로필"));

            var names = new List<string> { "원본" };
            foreach (ProfileOption option in _playtest.Profiles)
            {
                if (option.Profile != null)
                    names.Add(option.Profile.name);
            }

            string current = _playtest.AppliedProfile != null ? _playtest.AppliedProfile.name : "원본";
            int index = Math.Max(0, names.IndexOf(current));
            var field = new DropdownField(names, index);
            field.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            field.RegisterValueChangedCallback(evt =>
            {
                PlaytestSession.SelectProfile(evt.newValue == "원본" ? string.Empty : evt.newValue);
                LoadContent();
                Recompute();
            });
            _profileBox.Add(field);

            if (_playtest.ProfileProblem != null)
                _profileBox.Add(new HelpBox(_playtest.ProfileProblem, HelpBoxMessageType.Warning));

            _profileBox.Add(Note(EditorApplication.isPlaying
                ? "플레이 중에는 바꿀 수 없다. 개발 패널의 프로필 탭에서 바꾸면 장면을 다시 불러온다."
                : "고른 프로필의 값으로 미리보고 플레이한다. 프로필은 Assets/Playtest/Profiles의 JSON이다."));
        }

        private void BuildPlay()
        {
            _playBox.Clear();
            _playBox.Add(Header("플레이"));

            if (!EditorApplication.isPlaying)
            {
                var play = new Button(PlayInEditor) { text = "▶ 이 세팅으로 플레이" };
                play.style.height = 28;
                play.SetEnabled(_loaded != null && !EditorApplication.isPlayingOrWillChangePlaymode);
                _playBox.Add(play);
                _playBox.Add(Note("플레이 모드에 들어가 타이틀을 건너뛰고 이 세팅으로 판을 바로 시작한다. 시나리오처럼 이번 실행은 저장하지 않는다."));
                return;
            }

            var row = Row();
            row.Add(Grow(new Button(() => ApplyInPlay(true)) { text = "↻ 지금 판에 적용" }));
            row.Add(Grow(new Button(() => ApplyInPlay(false)) { text = "업그레이드 화면으로" }));
            row.Add(Grow(new Button(CaptureFromGame) { text = "지금 게임에서 가져오기" }));
            _playBox.Add(row);
            _playBox.Add(Note("판 구성은 판을 시작할 때 정해진다. 노드·단계를 바꿨으면 \"지금 판에 적용\"으로 판을 다시 시작한다."));
        }

        private void PlayInEditor()
        {
            if (FindAnyObjectByType<GameBootstrap>() == null)
            {
                _message = "열린 장면에 GameBootstrap이 없다. GameScene을 열고 다시 누른다.";
                BuildMessages();
                return;
            }

            TestSetupLaunch.Request(ToScenario());
            EditorApplication.EnterPlaymode();
        }

        private void ApplyInPlay(bool startBattle)
        {
            PlaytestPanel panel = FindAnyObjectByType<PlaytestPanel>();

            if (panel == null)
            {
                _message = "개발 패널을 찾지 못했다(게임이 아직 시작하지 않았거나 콘텐츠를 불러오지 못했다).";
                BuildMessages();
                return;
            }

            var errors = new List<string>();
            _message = panel.RunSetup(ToScenario(), startBattle, errors)
                ? (startBattle ? "이 세팅으로 판을 다시 시작했다." : "이 세팅의 진행 상태로 업그레이드 화면에 갔다.")
                : "세팅을 넣지 못했다: " + string.Join(" / ", errors);
            BuildMessages();
        }

        // 지금 게임의 진행 상태(성장도·Gold·산 노드)와 진행 중인 판의 Level을 창의 세팅으로 가져온다.
        private void CaptureFromGame()
        {
            PlaytestPanel panel = FindAnyObjectByType<PlaytestPanel>();

            if (panel == null || panel.Progress == null || NodeTree == null)
            {
                _message = "가져올 게임 진행을 찾지 못했다.";
                BuildMessages();
                return;
            }

            ProgressState progress = panel.Progress;
            Change("게임에서 가져오기", () =>
            {
                _ranks.Clear();

                foreach (NodeDefinition node in NodeTree.Nodes)
                    SetRank(node.Id, progress.RankOf(node.Id));

                _stage = progress.GrowthStage;
                _gold = progress.Gold;
                _level = panel.BattleLevel ?? StartLevel(_stage);
                _seed = panel.BattleSeed;
                _message = "지금 게임의 진행을 가져왔다.";
            }, rebuildFields: true);
        }

        #endregion

        #region 세팅 칸

        private void BuildFields()
        {
            _fieldsBox.Clear();
            ClampLevel();

            var name = new TextField("이름") { value = _setupName };
            name.isDelayed = true;
            name.RegisterValueChangedCallback(evt => Change("세팅 이름", () => _setupName = evt.newValue));
            _fieldsBox.Add(name);

            var note = new TextField("설명") { value = _note, multiline = true, tooltip = "이 세팅을 왜 만들었나(시나리오 JSON의 note). 플레이 느낌은 메모 창에 적는다." };
            note.isDelayed = true;
            note.RegisterValueChangedCallback(evt => Change("세팅 설명", () => _note = evt.newValue));
            _fieldsBox.Add(note);
            var category = new TextField("분류") { value = _category, isDelayed = true };
            category.RegisterValueChangedCallback(evt => Change("분류", () => _category = evt.newValue));
            _fieldsBox.Add(category);
            var expected = new TextField("확인할 결과") { value = _expected, multiline = true, isDelayed = true };
            expected.RegisterValueChangedCallback(evt => Change("확인할 결과", () => _expected = evt.newValue));
            _fieldsBox.Add(expected);
            _fieldsBox.Add(Note("저장은 확정된 노드 목록을 남깁니다. 예산 구매는 현재 가격으로 목록을 만드는 도구입니다."));

            if (Growth == null)
            {
                _fieldsBox.Add(new HelpBox("콘텐츠를 불러오지 못해 이정표 단계를 고를 수 없다.", HelpBoxMessageType.Error));
                return;
            }

            var stages = new List<string>();
            for (int stage = HqGrowthDefinition.StartStage; stage <= Growth.MaxStage; stage++)
                stages.Add(StageLabel(stage));

            var stageField = new DropdownField("이정표 단계", stages, _stage - HqGrowthDefinition.StartStage);
            stageField.RegisterValueChangedCallback(evt =>
            {
                int stage = stages.IndexOf(evt.newValue) + HqGrowthDefinition.StartStage;
                Change("이정표 단계", () =>
                {
                    _stage = stage;
                    _level = StartLevel(stage);
                }, rebuildFields: true);
            });
            _fieldsBox.Add(stageField);

            int first = StartLevel(_stage);
            int last = Mathf.Max(first, LastLevel(_stage));
            var level = new SliderInt($"블랙홀 Level ({first}~{last})", first, last) { value = _level, showInputField = true };
            level.RegisterValueChangedCallback(evt => Change("블랙홀 Level", () => _level = evt.newValue));
            _fieldsBox.Add(level);

            var gold = new LongField("Gold") { value = _gold, isDelayed = true };
            gold.RegisterValueChangedCallback(evt => Change("Gold", () => _gold = Math.Max(0, evt.newValue)));
            _fieldsBox.Add(gold);

            var seed = new IntegerField("시드 (0: 매번 다름)") { value = _seed, isDelayed = true };
            seed.RegisterValueChangedCallback(evt => Change("시드", () => _seed = evt.newValue));
            _fieldsBox.Add(seed);

            var freeze = new Toggle("시간 고정") { value = _freezeTime };
            freeze.RegisterValueChangedCallback(evt => Change("시간 고정", () => _freezeTime = evt.newValue));
            _fieldsBox.Add(freeze);

            _fieldsBox.Add(Note("Level은 판 안의 진행이다. 판을 시작한 뒤 EXP를 채워 원래 길로 Level업한다(성장 공급·성장 시간이 따라온다). " +
                                "Gold는 결산(이정표 보상)과 업그레이드 화면에만 쓰인다."));
        }

        private string StageLabel(int stage)
        {
            int goal = Growth.GoalLevelAt(stage);
            string from = stage == HqGrowthDefinition.StartStage ? "처음" : $"Lv{Growth.Milestones[stage - 1].Level} 이정표 지남";
            string to = goal != HqGrowthDefinition.NoGoal ? $"목표 Lv{goal}" : "목표 없음";
            return $"{stage} · {from} → {to}";
        }

        #endregion

        #region 노드 칸

        private void BuildNodes()
        {
            _nodesBox.Clear();

            if (_report != null && _report.Succeeded)
                _nodesBox.Add(new Label($"산 노드 {_report.OwnedNodes}개 · Rank 합 {_report.OwnedRanks} · 총 비용 {_report.TotalCost.ToString("N0", CultureInfo.InvariantCulture)} Gold"));

            var row = Row();
            row.Add(Grow(new Button(() => Change("모두 0", () => _ranks.Clear())) { text = "모두 0" }));
            row.Add(Grow(new Button(SetAllMax) { text = "모두 마지막 Rank" }));
            _nodesBox.Add(row);
            var groups = new List<string> { "전체" };
            if (NodeTree != null) foreach (NodeDefinition node in NodeTree.Nodes)
            {
                string group = NodeFamily(node.Id);
                if (!groups.Contains(group)) groups.Add(group);
            }
            groups.Sort(StringComparer.Ordinal);
            if (!groups.Contains(_nodeGroup)) _nodeGroup = "전체";
            var groupField = new DropdownField("노드 계열 (ID 기준)", groups, groups.IndexOf(_nodeGroup));
            groupField.RegisterValueChangedCallback(evt => { _nodeGroup = evt.newValue; BuildSearch(); });
            _nodesBox.Add(groupField);

            var buyRow = Row();
            var budget = new LongField("예산") { value = _budget };
            budget.style.flexGrow = 1;
            budget.RegisterValueChangedCallback(evt => _budget = Math.Max(0, evt.newValue));
            buyRow.Add(budget);
            buyRow.Add(new Button(BuyWithBudget) { text = "싼 것부터 더 사기" });
            _nodesBox.Add(buyRow);
            _nodesBox.Add(Note("예산으로 지금 산 노드에 더해 살 수 있는 노드를 싼 것부터 산다(게임 규칙). 기준 세팅을 빨리 만든 뒤 노드를 하나씩 고친다."));
        }

        private void SetAllMax()
        {
            if (NodeTree == null)
                return;

            Change("모두 마지막 Rank", () =>
            {
                foreach (NodeDefinition node in NodeTree.Nodes)
                    SetRank(node.Id, node.MaxRank);
            });
        }

        private void BuyWithBudget()
        {
            if (NodeTree == null || _budget <= 0)
                return;

            PlaytestScenario scenario = ToScenario();
            scenario.autoBuyBudget = _budget;
            TestSetupReport resolved = TestSetupPreview.Build(scenario, _loaded.Content, NodeTree);

            if (!resolved.Succeeded)
            {
                _message = "사지 못했다: " + string.Join(" / ", resolved.Errors);
                BuildMessages();
                return;
            }

            Change("예산으로 사기", () =>
            {
                foreach (NodeDefinition node in NodeTree.Nodes)
                    SetRank(node.Id, resolved.Progress.RankOf(node.Id));

                _message = resolved.Warnings.Find(w => w.StartsWith("자동 구매", StringComparison.Ordinal));
            });
        }

        private static string NodeFamily(string id) => id.Contains("-") ? id.Substring(0, id.LastIndexOf('-')) : id;

        private void BuildSearch()
        {
            _searchResults.Clear();

            if (NodeTree == null || (string.IsNullOrWhiteSpace(_search) && _nodeGroup == "전체"))
                return;

            int shown = 0;

            foreach (NodeDefinition node in NodeTree.Nodes)
            {
                if ((_nodeGroup != "전체" && NodeFamily(node.Id) != _nodeGroup) || node.Id.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (++shown > MaxSearchResults)
                {
                    _searchResults.Add(Note($"{MaxSearchResults}개까지만 보인다. 더 좁혀 찾는다."));
                    break;
                }

                string id = node.Id;
                int rank = RankOf(id);
                var row = Row();
                row.Add(new Button(() => Change("노드 Rank", () => SetRank(id, Math.Max(0, RankOf(id) - 1)))) { text = "−" });
                var label = new Label($"{id}  {rank}/{node.MaxRank}");
                label.style.flexGrow = 1;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                if (rank > 0)
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(label);
                row.Add(new Button(() => Change("노드 Rank", () => SetRank(id, Math.Min(node.MaxRank, RankOf(id) + 1)))) { text = "+" });
                row.Add(new Button(() => FocusNode(id)) { text = "보기" });
                _searchResults.Add(row);
            }

            if (shown == 0)
                _searchResults.Add(Note("맞는 노드가 없다."));
        }

        private void FocusNode(string id)
        {
            _focusedId = id;
            NodeData node = Tree == null ? null : NodeTreeAuthoring.Find(Tree, id);

            if (node != null)
                _canvas.CenterOn(node);

            _canvas.Refresh();
            BuildFocus();
        }

        // 고른 노드: Rank마다 비용과 효과(지금 Rank까지는 굵게).
        private void BuildFocus()
        {
            _focusBox.Clear();

            if (_focusedId == null || NodeTree == null)
                return;

            _focusBox.Add(Header($"고른 노드 · {_focusedId}"));

            if (!NodeTree.TryGet(_focusedId, out NodeDefinition node))
            {
                _focusBox.Add(new HelpBox("이 노드는 지금 콘텐츠에 없다.", HelpBoxMessageType.Warning));
                return;
            }

            int owned = RankOf(_focusedId);
            _focusBox.Add(new Label($"Rank {owned} / {node.MaxRank}"));

            foreach (NodeRankDefinition rank in node.Ranks)
            {
                var effects = new List<string>();

                foreach (NodeEffect effect in rank.Effects)
                {
                    UpgradeStatDefinition stat = NodeTree.Content.StatOf(effect.Stat);
                    effects.Add($"{stat.StatId} {NodeTreeAuthoring.Notation(stat.Unit, effect.Value)}");
                }

                var label = new Label($"Rank {rank.Rank} · {rank.Cost.ToString("N0", CultureInfo.InvariantCulture)} · {string.Join(", ", effects)}");
                label.style.whiteSpace = WhiteSpace.Normal;
                if (rank.Rank <= owned)
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                _focusBox.Add(label);
            }

            if (_report != null && _report.Unreachable.Contains(_focusedId))
                _focusBox.Add(Note("게임에서는 이 노드를 이 순서로 살 수 없다(시작 노드에서 산 노드로 이어지지 않는다). 테스트에는 그대로 쓴다."));
        }

        #endregion

        #region 메모 대상

        // 플레이 메모 창이 플레이 중이 아닐 때 쓰는 메모 대상: 열린 테스트 세팅 창의 세팅. 창이 없거나 세팅을 넣지 못했으면 null.
        internal static FeelNote NoteForOpenSetup() => _open != null ? _open.NoteFromWindow() : null;

        // 창의 세팅으로 만든 메모(판 없이). 노드는 실제로 적용된 Rank(확정 정보의 진행 상태)를 쓴다. 느낌 칸은 부르는 쪽이 채운다.
        private FeelNote NoteFromWindow()
        {
            if (_report == null || !_report.Succeeded || NodeTree == null || _playtest == null)
                return null;

            var note = new FeelNote
            {
                Source = "notes",
                SetupName = _setupName,
                GrowthStage = _report.Progress.GrowthStage,
                StartLevel = _level,
                Seed = _seed,
                Gold = _gold,
                ReachableInGame = _report.Unreachable.Count == 0,
                Profile = _playtest.AppliedProfile != null ? _playtest.AppliedProfile.name : string.Empty,
                Fingerprint = _playtest.Fingerprint,
                BaseFingerprint = _playtest.BaseFingerprint,
                Stats = _report.Stats,
            };

            foreach (NodeDefinition node in NodeTree.Nodes)
            {
                int rank = _report.Progress.RankOf(node.Id);

                if (rank > 0)
                    note.Nodes.Add((node.Id, rank));
            }

            note.DescribeNodes(NodeTree);
            return note;
        }

        #endregion

        #region 알림·확정 정보

        private void BuildMessages()
        {
            _messagesBox.Clear();

            if (!string.IsNullOrEmpty(_message))
                _messagesBox.Add(new HelpBox(_message, HelpBoxMessageType.Info));

            if (_report == null)
                return;

            foreach (string error in _report.Errors)
                _messagesBox.Add(new HelpBox(error, HelpBoxMessageType.Error));

            foreach (string warning in _report.Warnings)
            {
                // 자동 구매 결과는 위 알림이 이미 말한다.
                if (!warning.StartsWith("자동 구매", StringComparison.Ordinal))
                    _messagesBox.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
            }
        }

        private void BuildReport()
        {
            _reportBox.Clear();

            if (_report == null || !_report.Succeeded)
            {
                _reportBox.Add(Note("세팅을 넣지 못해 확정 정보가 없다. 위의 오류를 본다."));
                return;
            }

            foreach (TestSetupReport.Section section in _report.Sections)
            {
                var foldout = new Foldout { text = section.Title, value = true };

                foreach ((string label, string value) in section.Rows)
                {
                    var row = Row();
                    var name = new Label(label);
                    name.style.width = 150;
                    name.style.flexShrink = 0;
                    name.style.opacity = 0.75f;
                    var text = new Label(value);
                    text.style.whiteSpace = WhiteSpace.Normal;
                    text.style.flexShrink = 1;
                    text.selection.isSelectable = true;
                    row.Add(name);
                    row.Add(text);
                    foldout.Add(row);
                }

                _reportBox.Add(foldout);
            }
        }

        #endregion

        #region 모양

        private static VisualElement Box(VisualElement parent)
        {
            var box = new VisualElement();
            parent.Add(box);
            return box;
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

        #endregion
    }
}
