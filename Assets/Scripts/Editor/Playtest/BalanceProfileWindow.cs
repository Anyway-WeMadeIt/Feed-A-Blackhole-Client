using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 밸런스 프로필(메뉴 BlackHole > Balance Profile). QA 메모를 보며 노드 수치를 바꿔 프로필로 저장하고 바로 플레이한다.
    // 원본 노드 시트(CSV)는 건드리지 않는다. 바꾼 값은 Assets/Playtest/Profiles/<이름>.json의 패치다(BalanceProfilePatcher).
    // - 프로필: 있는 프로필을 고르거나 새로 만든다. 이름을 바꿔 저장하면 복사본이 된다.
    // - 근거 메모: 고른 메모의 느낌·고칠 방향을 보고, 저장할 때 이번에 바꾼 값마다 이유와 메모 이름이 붙는다.
    // - 노드 수치: 수치(StatId)별 묶음 안에 노드·Rank마다 원본 → 새 값. 묶음마다 "보이는 칸에 원본 × 배율".
    //   범위: 전체 · 바꾼 것만 · 고른 메모의 노드 · 시나리오의 노드. 메모·시나리오에서 산 Rank보다 높은 칸은 흐리게.
    // - 그 밖의 패치: 노드 효과가 아닌 값(비용·적·Breaker·판 시간 등)은 경로와 값으로 고친다.
    // - 저장: 게임과 같은 로더로 검사한 뒤 쓴다. 이 프로필로 적은 메모가 있는데 값이 바뀌었으면 새 이름(…-v2)으로 저장한다.
    //   "저장하면 이 프로필로 플레이"가 켜져 있으면 다음 플레이 프로필로 고르고, 플레이 중이면 같은 세팅으로 다시 시작한다.
    // 고친 값은 Undo(Ctrl+Z)로 되돌린다(저장하면 그 전 Undo는 지운다). 고치기 규칙은 BalanceProfileEdit에 있다.
    internal sealed class BalanceProfileWindow : EditorWindow
    {
        private const string ProfilesFolder = PlaytestLibrary.ProfilesFolder;
        private const string NewProfileLabel = "(새 프로필)";
        private const string NewProfileName = "new-profile";
        private const string NoMemoLabel = "(고르지 않음)";
        private const string RangeAll = "전체";
        private const string RangeChanged = "바꾼 것만";
        private const string RangeMemo = "고른 메모의 노드";
        private const string ScenarioPrefix = "시나리오: ";
        private const int MaxMemoChoices = 60;
        private const int MaxErrorsShown = 6;

        private static readonly Color ChangedFill = new Color(0.55f, 0.42f, 0.12f, 0.35f);
        private static readonly Color ChangedText = new Color(1f, 0.85f, 0.45f);
        private static readonly Color MemoBorder = new Color(0.5f, 0.5f, 0.55f);

        // 편집 상태. 창이 직렬화하므로 Undo와 플레이 진입(도메인 다시 불러오기)을 넘긴다.
        [SerializeField] private BalanceProfile _draft = new BalanceProfile { name = NewProfileName };
        // 불러온 그대로(새 프로필이면 빈 것). 바뀐 값·이번에 바꾼 패치를 이것과 견준다.
        [SerializeField] private BalanceProfile _base = new BalanceProfile();
        // 불러온 프로필 이름과 파일. 새 프로필이면 빈 글자.
        [SerializeField] private string _baseName = string.Empty;
        [SerializeField] private string _baseFile = string.Empty;
        [SerializeField] private string _reason = string.Empty;
        [SerializeField] private List<string> _memos = new List<string>();
        [SerializeField] private string _focusMemo = string.Empty;
        [SerializeField] private string _range = RangeAll;
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private bool _playAfterSave = true;
        [SerializeField] private string _addPath = string.Empty;

        // 원본(패치 전) 데이터. 칸의 원본 값과 경로 검사에 쓴다.
        private GameContentSetup _setup;
        private NodeContentData _nodes;
        private ContentData _content;
        private List<BalanceProfileEdit.EffectRow> _rows = new List<BalanceProfileEdit.EffectRow>();
        private readonly HashSet<string> _effectPaths = new HashSet<string>(StringComparer.Ordinal);
        private PlaytestSession _playtest;
        private string _loadProblem;
        private string _status;
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);
        private (DateTime Write, long Length) _memoStamp;

        private VisualElement _profileBox;
        private VisualElement _memoBox;
        private VisualElement _rangeBox;
        private VisualElement _rowsBox;
        private VisualElement _otherBox;
        private Label _summary;
        private Label _saveTarget;
        private Label _statusLabel;
        private readonly List<GroupView> _groups = new List<GroupView>();

        // 그린 묶음 하나(수치 하나)와 그 칸들. 값을 고치면 다시 그리지 않고 이것만 고친다.
        private sealed class GroupView
        {
            public string StatId;
            public Foldout Foldout;
            public readonly List<RowView> Rows = new List<RowView>();
        }

        private sealed class RowView
        {
            public BalanceProfileEdit.EffectRow Row;
            public VisualElement Line;
            public DoubleField Field;
            public Label Delta;
        }

        [MenuItem("BlackHole/Balance Profile")]
        internal static void Open() => GetWindow<BalanceProfileWindow>("밸런스 프로필");

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            LiveDataSignal.Changed += OnLiveDataChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            LiveDataSignal.Changed -= OnLiveDataChanged;
        }

        private void OnUndoRedo()
        {
            if (_rowsBox != null)
                BuildAll();
        }

        // 수치 파일(노드 CSV·프로필)이 바뀌었다. 고치던 것이 없으면 불러온 프로필을 디스크 내용으로 다시 읽는다.
        private void OnLiveDataChanged(LiveDataChange change)
        {
            if (_rowsBox == null || !change.ContentChanged)
                return;

            bool dirty = Dirty;
            LoadContent();

            if (!dirty && _baseName.Length > 0 && FindProfile(_baseName) is ProfileOption option)
                Take(option);

            BuildAll();
        }

        // 메모를 쓰고 돌아오면 메모 목록을 다시 읽는다. 메모 파일이 그대로면 다시 그리지 않는다(누른 칸이 사라지지 않게).
        private void OnFocus()
        {
            if (_memoBox == null || PlaytestNotes.Stamp() == _memoStamp)
                return;

            string range = _range;
            BuildMemo();
            BuildRange();
            if (_range != range)
                BuildRows();
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => { LoadContent(); BuildAll(); }) { text = "다시 불러오기" });
            toolbar.Add(new ToolbarButton(PlaytestNotesWindow.Open) { text = "메모 창" });
            toolbar.Add(new ToolbarButton(() => GetWindow<TestSetupWindow>("Test Setup")) { text = "테스트 세팅 창" });
            rootVisualElement.Add(toolbar);

            var root = new ScrollView();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 4;
            rootVisualElement.Add(root);

            _profileBox = Box(root);
            _memoBox = Box(root);
            root.Add(Header("노드 수치"));
            _rangeBox = Box(root);
            _rowsBox = Box(root);
            _otherBox = Box(root);
            BuildSave(root);

            LoadContent();
            BuildAll();
        }

        #region 데이터

        // 원본 노드 시트와 콘텐츠를 읽고, 프로필·시나리오 목록을 다시 모은다.
        private void LoadContent()
        {
            _playtest = PlaytestSession.Create(FindAsset<PlaytestLibrary>(), new ContentTag());
            _setup = FindAsset<GameContentSetup>();
            _nodes = null;
            _content = null;
            _loadProblem = null;
            _rows = new List<BalanceProfileEdit.EffectRow>();
            _effectPaths.Clear();

            if (_setup == null || _setup.MissingReferences().Count > 0 || _setup.Nodes == null || _setup.Nodes.Content == null)
            {
                _loadProblem = "게임 콘텐츠 세트(GameContentSetup)나 노드 콘텐츠(NodeContentSource)를 찾지 못했다. 연결을 확인한다.";
                return;
            }

            var errors = new List<ContentDiagnostic>();
            NodeContentData nodes = _setup.Nodes.Content.Read(errors);

            if (errors.Count > 0)
            {
                _loadProblem = $"노드 시트(CSV)를 읽지 못했다({errors.Count}개): {errors[0]}";
                return;
            }

            _nodes = nodes;
            _content = _setup.ToData();
            _rows = BalanceProfileEdit.EffectRows(nodes);

            foreach (BalanceProfileEdit.EffectRow row in _rows)
                _effectPaths.Add(row.Path);
        }

        private static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private ProfileOption FindProfile(string name) =>
            _playtest?.Profiles.Find(option => option.Profile != null && option.Profile.name == name);

        // 불러온 프로필로 편집을 시작한다(null이면 새 프로필).
        private void Take(ProfileOption option)
        {
            if (option == null)
            {
                _base = new BalanceProfile();
                _draft = new BalanceProfile { name = NewProfileName };
                _baseName = string.Empty;
                _baseFile = string.Empty;
                return;
            }

            _base = BalanceProfileEdit.Clone(option.Profile);
            _draft = BalanceProfileEdit.Clone(option.Profile);
            _baseName = option.Profile.name;
            _baseFile = option.File.Source == PlaytestFiles.AssetSource
                ? ProfilesFolder + "/" + option.File.FileName
                : string.Empty;
        }

        private bool ValuesChanged => BalanceProfileEdit.ValuesKey(_draft) != BalanceProfileEdit.ValuesKey(_base);

        // 저장하지 않은 변경이 있나(값·이름·설명).
        private bool Dirty =>
            ValuesChanged
            || (_baseName.Length > 0 && (_draft.name ?? string.Empty) != _baseName)
            || (_draft.note ?? string.Empty) != (_base.note ?? string.Empty);

        private static int MemoCount(string profile)
        {
            if (string.IsNullOrEmpty(profile))
                return 0;

            int count = 0;
            foreach (FeelNoteView note in PlaytestNotes.ReadAll(out _))
            {
                if (note.Profile == profile)
                    count++;
            }

            return count;
        }

        // 쓰인 프로필 이름: 읽은 프로필 이름과 프로필 폴더의 파일 이름.
        private HashSet<string> TakenNames()
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);

            if (_playtest != null)
            {
                foreach (ProfileOption option in _playtest.Profiles)
                {
                    if (option.Profile != null)
                        taken.Add(option.Profile.name);
                }
            }

            if (Directory.Exists(ProfilesFolder))
            {
                foreach (string file in Directory.GetFiles(ProfilesFolder, "*.json"))
                    taken.Add(Path.GetFileNameWithoutExtension(file));
            }

            return taken;
        }

        // 저장할 파일. 불러온 프로필을 같은 이름으로 저장하면 그 파일, 아니면 <이름>.json.
        private string PathFor(string name) =>
            name == _baseName && _baseFile.Length > 0 ? _baseFile : $"{ProfilesFolder}/{name}.json";

        // 범위가 메모·시나리오면 그 노드와 산 Rank. 전체·바꾼 것만이면 null.
        private Dictionary<string, int> RangeNodes()
        {
            var nodes = new Dictionary<string, int>(StringComparer.Ordinal);

            if (_range == RangeMemo)
            {
                FeelNoteView memo = FindMemo(_focusMemo);
                if (memo != null)
                {
                    foreach ((string nodeId, int rank) in memo.Nodes)
                        nodes[nodeId] = rank;
                }

                return nodes;
            }

            if (_range.StartsWith(ScenarioPrefix, StringComparison.Ordinal))
            {
                string name = _range.Substring(ScenarioPrefix.Length);
                ScenarioOption option = _playtest?.Scenarios.Find(o => o.Scenario != null && o.Scenario.name == name);
                if (option?.Scenario.nodes != null)
                {
                    foreach (PlaytestScenario.Node node in option.Scenario.nodes)
                    {
                        if (node != null && !string.IsNullOrEmpty(node.nodeId))
                            nodes[node.nodeId] = node.rank;
                    }
                }

                return nodes;
            }

            return null;
        }

        private static FeelNoteView FindMemo(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (FeelNoteView note in PlaytestNotes.ReadAll(out _))
            {
                if (note.Name == name)
                    return note;
            }

            return null;
        }

        #endregion

        #region 그리기

        private void BuildAll()
        {
            BuildProfile();
            BuildMemo();
            BuildRange();
            BuildRows();
            BuildOther();
            UpdateSummary();
        }

        private void BuildProfile()
        {
            _profileBox.Clear();
            _profileBox.Add(Header("프로필"));

            if (_loadProblem != null)
                _profileBox.Add(new HelpBox(_loadProblem, HelpBoxMessageType.Error));

            var names = new List<string> { NewProfileLabel };
            if (_playtest != null)
            {
                foreach (ProfileOption option in _playtest.Profiles)
                {
                    if (option.Profile != null && !names.Contains(option.Profile.name))
                        names.Add(option.Profile.name);
                }
            }

            var pick = new DropdownField("편집할 프로필", names, Math.Max(0, names.IndexOf(_baseName.Length > 0 ? _baseName : NewProfileLabel)));
            pick.RegisterValueChangedCallback(evt =>
            {
                if (Dirty && !EditorUtility.DisplayDialog("밸런스 프로필", "저장하지 않은 변경이 있다. 버리고 다른 프로필을 열까?", "버리기", "취소"))
                {
                    pick.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                Undo.RecordObject(this, "프로필 열기");
                Take(evt.newValue == NewProfileLabel ? null : FindProfile(evt.newValue));
                _status = null;
                BuildAll();
            });
            _profileBox.Add(pick);

            var nameField = new TextField("이름") { value = _draft.name, tooltip = "영문·숫자·. _ - 로 58자까지. 다른 이름으로 저장하면 복사본이 된다." };
            nameField.RegisterValueChangedCallback(evt =>
            {
                _draft.name = evt.newValue;
                UpdateSummary();
            });
            _profileBox.Add(nameField);

            var noteField = new TextField("설명") { value = _draft.note ?? string.Empty, multiline = true, tooltip = "이 프로필이 무엇을 시험하나(한두 줄)." };
            noteField.RegisterValueChangedCallback(evt =>
            {
                _draft.note = evt.newValue;
                UpdateSummary();
            });
            _profileBox.Add(noteField);

            string selected = PlayerPrefs.GetString(PlaytestSession.ProfileKey, string.Empty);
            _profileBox.Add(Note(
                $"이 프로필로 적은 메모 {MemoCount(_baseName)}개 · 다음 플레이 프로필: {(selected.Length > 0 ? selected : FeelNotes.BaseProfileLabel)}"));
        }

        private void BuildMemo()
        {
            _memoBox.Clear();
            _memoStamp = PlaytestNotes.Stamp();
            _memoBox.Add(Header("근거 메모"));
            _memoBox.Add(Note("QA 메모를 고르면 느낌·고칠 방향이 보이고 범위가 그 메모의 노드로 좁혀진다. 저장할 때 이번에 바꾼 값마다 아래 이유와 근거 메모 이름이 붙는다."));

            IReadOnlyList<FeelNoteView> all = PlaytestNotes.ReadAll(out _);
            var names = new List<string> { NoMemoLabel };
            for (int i = all.Count - 1; i >= 0 && names.Count <= MaxMemoChoices; i--)
            {
                if (!string.IsNullOrEmpty(all[i].Name))
                    names.Add(all[i].Name);
            }

            if (_focusMemo.Length > 0 && !names.Contains(_focusMemo))
                names.Add(_focusMemo);

            var pick = new DropdownField("메모", names, Math.Max(0, names.IndexOf(_focusMemo.Length > 0 ? _focusMemo : NoMemoLabel)));
            pick.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(this, "근거 메모");
                _focusMemo = evt.newValue == NoMemoLabel ? string.Empty : evt.newValue;

                if (_focusMemo.Length > 0)
                {
                    if (!_memos.Contains(_focusMemo))
                        _memos.Add(_focusMemo);
                    _range = RangeMemo;
                }
                else if (_range == RangeMemo)
                {
                    _range = RangeAll;
                }

                BuildMemo();
                BuildRange();
                BuildRows();
            });
            _memoBox.Add(pick);

            FeelNoteView memo = FindMemo(_focusMemo);
            if (memo != null)
                _memoBox.Add(MemoCard(memo));
            else if (_focusMemo.Length > 0)
                _memoBox.Add(Note($"메모 '{_focusMemo}'를 찾지 못했다(PlaytestData/notes)."));

            var chips = Row();
            chips.style.flexWrap = Wrap.Wrap;
            chips.Add(RowLabel("근거", 52));
            if (_memos.Count == 0)
                chips.Add(Dim(new Label("없음")));
            foreach (string memoName in _memos)
            {
                string removed = memoName;
                chips.Add(new Button(() =>
                {
                    Undo.RecordObject(this, "근거 메모 빼기");
                    _memos.Remove(removed);
                    BuildMemo();
                })
                {
                    text = memoName + "  ×",
                    tooltip = "근거에서 뺀다",
                });
            }
            _memoBox.Add(chips);

            var reason = new TextField("이유") { value = _reason, tooltip = "왜 바꾸나(한 줄). 저장할 때 이번에 바꾼 값에 붙고, 저장하면 비운다." };
            reason.RegisterValueChangedCallback(evt => _reason = evt.newValue);
            _memoBox.Add(reason);
        }

        private static VisualElement MemoCard(FeelNoteView memo)
        {
            var box = new VisualElement();
            box.style.marginTop = 2;
            box.style.marginBottom = 4;
            box.style.paddingLeft = 6;
            box.style.borderLeftWidth = 2;
            box.style.borderLeftColor = MemoBorder;

            var parts = new List<string>
            {
                $"시나리오 {(string.IsNullOrEmpty(memo.SetupName) ? "(이름 없음)" : memo.SetupName)}",
                $"프로필 {(string.IsNullOrEmpty(memo.Profile) ? FeelNotes.BaseProfileLabel : memo.Profile)}",
                $"노드 {memo.Nodes.Count}개",
            };
            if (memo.Difficulty.HasValue)
                parts.Add(FeelNotes.DifficultyLabel(memo.Difficulty.Value));
            if (memo.Fun.HasValue)
                parts.Add($"재미 {memo.Fun.Value}");
            foreach (string tag in memo.Tags)
                parts.Add("#" + FeelNotes.TagLabel(tag));
            if (memo.HasBattle)
                parts.Add($"Lv {memo.BattleLevel} · {memo.BattleElapsed:0.#}초");

            box.Add(Wrapped(string.Join(" · ", parts)));
            if (memo.Text.Length > 0)
                box.Add(Wrapped("느낌: " + memo.Text));
            if (memo.Intent.Length > 0)
                box.Add(Wrapped("고칠 방향: " + memo.Intent));
            return box;
        }

        private void BuildRange()
        {
            _rangeBox.Clear();

            var choices = new List<string> { RangeAll, RangeChanged };
            if (_focusMemo.Length > 0)
                choices.Add(RangeMemo);
            if (_playtest != null)
            {
                foreach (ScenarioOption option in _playtest.Scenarios)
                {
                    if (option.Scenario?.nodes != null && option.Scenario.nodes.Count > 0)
                        choices.Add(ScenarioPrefix + option.Scenario.name);
                }
            }

            if (!choices.Contains(_range))
                _range = RangeAll;

            var range = new DropdownField("범위", choices, choices.IndexOf(_range));
            range.RegisterValueChangedCallback(evt =>
            {
                _range = evt.newValue;
                BuildRows();
            });
            _rangeBox.Add(range);

            var search = new TextField("찾기") { value = _search, tooltip = "노드 ID나 StatId의 일부" };
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue;
                BuildRows();
            });
            _rangeBox.Add(search);
        }

        // 보이는 칸을 수치별 묶음으로 다시 그린다.
        private void BuildRows()
        {
            _rowsBox.Clear();
            _groups.Clear();

            if (_nodes == null)
                return;

            Dictionary<string, int> bought = RangeNodes();
            string search = _search.Trim();
            bool narrowed = bought != null || search.Length > 0 || _range == RangeChanged;
            var visible = new List<BalanceProfileEdit.EffectRow>();

            foreach (BalanceProfileEdit.EffectRow row in _rows)
            {
                if (_range == RangeChanged && BalanceProfileEdit.Find(_draft, row.Path) == null)
                    continue;
                if (bought != null && !bought.ContainsKey(row.NodeId))
                    continue;
                if (search.Length > 0
                    && row.NodeId.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && row.StatId.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                visible.Add(row);
            }

            string scope = bought != null ? $" · 노드 {bought.Count}개 · 산 Rank보다 높은 칸은 흐리게" : string.Empty;
            _rowsBox.Add(Note($"칸 {visible.Count}개{scope} · 값은 원본 시트(CSV) 기준이다. 칸을 고치고 Enter."));

            GroupView group = null;
            foreach (BalanceProfileEdit.EffectRow row in visible)
            {
                if (group == null || group.StatId != row.StatId)
                {
                    group = new GroupView { StatId = row.StatId };
                    _groups.Add(group);
                }

                group.Rows.Add(new RowView { Row = row });
            }

            foreach (GroupView view in _groups)
                _rowsBox.Add(GroupElement(view, bought, narrowed));
        }

        private VisualElement GroupElement(GroupView group, Dictionary<string, int> bought, bool narrowed)
        {
            bool changed = group.Rows.Exists(view => BalanceProfileEdit.Find(_draft, view.Row.Path) != null);
            var foldout = new Foldout { value = narrowed || changed || _expanded.Contains(group.StatId) };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout)
                    return;
                if (evt.newValue)
                    _expanded.Add(group.StatId);
                else
                    _expanded.Remove(group.StatId);
            });
            group.Foldout = foldout;

            var bulk = Row();
            bulk.style.marginBottom = 2;
            var factor = new DoubleField("원본 ×") { value = 1, tooltip = "보이는 칸을 원본 × 이 배율로. 여러 번 눌러도 곱이 쌓이지 않는다." };
            factor.labelElement.style.minWidth = 48;
            factor.style.width = 130;
            bulk.Add(factor);
            bulk.Add(new Button(() => Scale(group, factor.value)) { text = $"보이는 {group.Rows.Count}칸에 적용" });
            bulk.Add(new Button(() => Scale(group, 1)) { text = "보이는 칸 원래대로" });
            foldout.Add(bulk);

            foreach (RowView view in group.Rows)
                foldout.Add(RowElement(view, bought));

            UpdateGroup(group);
            return foldout;
        }

        private VisualElement RowElement(RowView view, Dictionary<string, int> bought)
        {
            BalanceProfileEdit.EffectRow row = view.Row;
            var line = Row();
            line.style.paddingLeft = 2;
            line.style.paddingTop = 1;
            line.style.paddingBottom = 1;

            var name = new Label($"{row.NodeId}  R{row.Rank}/{row.MaxRank}");
            name.style.width = 230;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            line.Add(name);

            var original = new Label("원본 " + BalanceProfileEdit.Format(row.Original, row.Percent));
            original.style.width = 110;
            original.style.unityTextAlign = TextAnchor.MiddleLeft;
            line.Add(original);

            var field = new DoubleField { value = BalanceProfileEdit.ValueOf(_draft, row.Path, row.Original), isDelayed = true };
            field.style.width = 90;
            field.RegisterValueChangedCallback(evt => SetValue(view, evt.newValue));
            line.Add(field);

            var delta = new Label();
            delta.style.width = 80;
            delta.style.unityTextAlign = TextAnchor.MiddleLeft;
            delta.style.paddingLeft = 4;
            line.Add(delta);

            var reset = new Button(() => SetValue(view, row.Original)) { text = "↺", tooltip = "원본으로" };
            line.Add(reset);

            // 메모·시나리오에서 산 Rank보다 높은 칸: 그 판에는 영향이 없다.
            if (bought != null && bought.TryGetValue(row.NodeId, out int rank) && row.Rank > rank)
                line.style.opacity = 0.45f;

            view.Line = line;
            view.Field = field;
            view.Delta = delta;
            UpdateRow(view);
            return line;
        }

        private void UpdateRow(RowView view)
        {
            BalanceProfileEdit.EffectRow row = view.Row;
            double value = BalanceProfileEdit.ValueOf(_draft, row.Path, row.Original);
            bool changed = BalanceProfileEdit.Find(_draft, row.Path) != null;

            view.Field.SetValueWithoutNotify(value);
            view.Delta.text = BalanceProfileEdit.Delta(value, row.Original, row.Percent);
            view.Delta.style.color = changed ? new StyleColor(ChangedText) : new StyleColor(StyleKeyword.Null);
            view.Line.style.backgroundColor = changed ? new StyleColor(ChangedFill) : new StyleColor(StyleKeyword.Null);
        }

        private void UpdateGroup(GroupView group)
        {
            int changed = 0;
            foreach (RowView view in group.Rows)
            {
                if (BalanceProfileEdit.Find(_draft, view.Row.Path) != null)
                    changed++;
            }

            BalanceProfileEdit.EffectRow first = group.Rows[0].Row;
            string unit = first.Percent ? " (%)" : first.IsInt ? " (정수)" : string.Empty;
            group.Foldout.text = $"{group.StatId}{unit} · {group.Rows.Count}칸" + (changed > 0 ? $" · 바꿈 {changed}" : string.Empty);
        }

        // 노드 효과가 아닌 패치(비용·적·Breaker 등): 경로와 값.
        private void BuildOther()
        {
            _otherBox.Clear();
            _otherBox.Add(Header("그 밖의 패치"));
            _otherBox.Add(Note("노드 효과가 아닌 값. 경로 예: node/breaker.radius-01/1/cost, enemy/asteroid/tier/1/hp, breaker/damage, battle/timeLimit (문법은 BalanceProfilePatcher 머리 주석)."));

            foreach (BalanceProfile.Patch patch in _draft.patches)
            {
                if (patch == null || _effectPaths.Contains(patch.path ?? string.Empty))
                    continue;

                BalanceProfile.Patch target = patch;
                var line = Row();
                var path = new Label(patch.path);
                path.style.flexGrow = 1;
                path.style.unityTextAlign = TextAnchor.MiddleLeft;
                line.Add(path);

                var original = new Label(OriginalOf(patch.path));
                original.style.width = 130;
                original.style.unityTextAlign = TextAnchor.MiddleLeft;
                line.Add(original);

                var field = new DoubleField { value = patch.value, isDelayed = true };
                field.style.width = 120;
                field.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(this, "프로필 값 바꾸기");
                    target.value = evt.newValue;
                    UpdateSummary();
                });
                line.Add(field);

                line.Add(new Button(() =>
                {
                    Undo.RecordObject(this, "패치 빼기");
                    _draft.patches.Remove(target);
                    BuildOther();
                    UpdateSummary();
                })
                {
                    text = "×",
                    tooltip = "이 패치를 뺀다(원본 값으로)",
                });
                _otherBox.Add(line);
            }

            var add = Row();
            var input = new TextField("경로 추가") { value = _addPath };
            input.style.flexGrow = 1;
            input.RegisterValueChangedCallback(evt => _addPath = evt.newValue);
            add.Add(input);
            add.Add(new Button(AddPath) { text = "추가" });
            _otherBox.Add(add);
        }

        private string OriginalOf(string path)
        {
            if (_content == null || _nodes == null)
                return string.Empty;

            return BalanceProfilePatcher.TryRead(path, _content, _nodes, out double value, out string error)
                ? "원본 " + value.ToString("0.####", CultureInfo.InvariantCulture)
                : error;
        }

        private void AddPath()
        {
            string path = _addPath.Trim();

            if (_content == null || _nodes == null)
            {
                SetStatus(_loadProblem);
                return;
            }

            if (!BalanceProfilePatcher.TryRead(path, _content, _nodes, out double value, out string error))
            {
                SetStatus($"경로 '{path}': {error}");
                return;
            }

            if (BalanceProfileEdit.Find(_draft, path) == null)
            {
                Undo.RecordObject(this, "패치 추가");
                _draft.patches.Add(new BalanceProfile.Patch { path = path, value = value });
            }

            _addPath = string.Empty;
            SetStatus($"'{path}'를 넣었다(지금 값 {value.ToString("0.####", CultureInfo.InvariantCulture)}). 값을 고친다.");
            BuildRows();
            BuildOther();
            UpdateSummary();
        }

        private void BuildSave(VisualElement root)
        {
            root.Add(Header("저장"));
            _summary = Wrapped(string.Empty);
            root.Add(_summary);
            _saveTarget = Wrapped(string.Empty);
            root.Add(_saveTarget);

            var play = new Toggle("저장하면 이 프로필로 플레이")
            {
                value = _playAfterSave,
                tooltip = "다음 플레이 프로필로 고른다. 플레이 중이면 같은 세팅·같은 시드로 판을 다시 시작한다.",
            };
            play.RegisterValueChangedCallback(evt => _playAfterSave = evt.newValue);
            root.Add(play);

            var buttons = Row();
            var save = new Button(Save) { text = "저장" };
            save.style.height = 26;
            save.style.flexGrow = 1;
            buttons.Add(save);
            var revert = new Button(Revert) { text = "불러온 상태로" };
            revert.style.height = 26;
            buttons.Add(revert);
            root.Add(buttons);

            _statusLabel = Wrapped(_status ?? string.Empty);
            _statusLabel.style.marginBottom = 12;
            root.Add(_statusLabel);
        }

        private void UpdateSummary()
        {
            if (_summary == null)
                return;

            int effects = 0;
            foreach (BalanceProfile.Patch patch in _draft.patches)
            {
                if (patch != null && _effectPaths.Contains(patch.path ?? string.Empty))
                    effects++;
            }

            int changed = 0;
            foreach (BalanceProfile.Patch patch in _draft.patches)
            {
                BalanceProfile.Patch before = BalanceProfileEdit.Find(_base, patch?.path);
                if (patch != null && (before == null || !BalanceProfileEdit.Same(before.value, patch.value)))
                    changed++;
            }

            _summary.text = $"패치 {_draft.patches.Count}개(노드 효과 {effects} · 그 밖 {_draft.patches.Count - effects}) · "
                            + (_baseName.Length > 0 ? $"불러온 뒤 바뀐 값 {changed}개" : "새 프로필");

            string name = (_draft.name ?? string.Empty).Trim();
            int memos = MemoCount(_baseName);
            string target = BalanceProfileEdit.SaveName(name, _baseName, ValuesChanged, memos, TakenNames(), out bool renamed);
            string where = BalanceProfile.IsValidName(target) ? PathFor(target) : "(이름이 맞지 않다)";
            _saveTarget.text = renamed
                ? $"저장 → {where}\n'{_baseName}'로 적은 메모 {memos}개가 있어, 값이 바뀌면 새 이름으로 저장한다(그 메모들은 이전 값의 기록으로 남는다)."
                : $"저장 → {where}";

            titleContent = new GUIContent(Dirty ? "밸런스 프로필 *" : "밸런스 프로필");
        }

        private void SetStatus(string text)
        {
            _status = text;
            if (_statusLabel != null)
                _statusLabel.text = text ?? string.Empty;
        }

        #endregion

        #region 고치기·저장

        private void SetValue(RowView view, double value)
        {
            BalanceProfileEdit.EffectRow row = view.Row;
            if (row.IsInt)
                value = Math.Round(value, MidpointRounding.AwayFromZero);

            Undo.RecordObject(this, "프로필 값 바꾸기");
            BalanceProfileEdit.Set(_draft, row.Path, value, row.Original);
            UpdateRow(view);
            UpdateGroup(GroupOf(view));
            UpdateSummary();
        }

        private void Scale(GroupView group, double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor))
                return;

            Undo.RecordObject(this, "프로필 값 묶음 배율");
            foreach (RowView view in group.Rows)
            {
                BalanceProfileEdit.EffectRow row = view.Row;
                BalanceProfileEdit.Set(_draft, row.Path, BalanceProfileEdit.Scaled(row.Original, factor, row.IsInt), row.Original);
                UpdateRow(view);
            }

            UpdateGroup(group);
            UpdateSummary();
            SetStatus(BalanceProfileEdit.Same(factor, 1)
                ? $"{group.StatId}: 보이는 {group.Rows.Count}칸을 원본으로 되돌렸다."
                : $"{group.StatId}: 보이는 {group.Rows.Count}칸을 원본 × {factor.ToString("0.####", CultureInfo.InvariantCulture)}로 바꿨다.");
        }

        private GroupView GroupOf(RowView view) => _groups.Find(group => group.Rows.Contains(view));

        private void Revert()
        {
            if (!Dirty)
                return;

            Undo.RecordObject(this, "불러온 상태로");
            _draft = BalanceProfileEdit.Clone(_base);
            if (_baseName.Length == 0)
                _draft.name = NewProfileName;
            SetStatus("불러온 상태로 되돌렸다.");
            BuildAll();
        }

        private void Save()
        {
            if (_nodes == null)
            {
                SetStatus(_loadProblem);
                return;
            }

            string name = (_draft.name ?? string.Empty).Trim();
            if (!BalanceProfile.IsValidName(name))
            {
                SetStatus($"이름은 영문·숫자·. _ - 로 {BalanceProfile.MaxNameLength}자까지다. 받은 값: '{name}'.");
                return;
            }

            if (_draft.patches.Count == 0)
            {
                SetStatus("바꾼 값이 없다(프로필에는 패치가 하나 이상 있어야 한다).");
                return;
            }

            HashSet<string> taken = TakenNames();
            string target = BalanceProfileEdit.SaveName(name, _baseName, ValuesChanged, MemoCount(_baseName), taken, out bool renamed);
            string path = PathFor(target);

            // 불러온 것이 아닌 다른 프로필을 덮어쓴다.
            if (target != _baseName && (taken.Contains(target) || File.Exists(path)))
            {
                int others = MemoCount(target);
                string warning = others > 0 ? $"\n그 프로필로 적은 메모 {others}개가 이전 값의 기록이 된다." : string.Empty;
                if (!EditorUtility.DisplayDialog("밸런스 프로필", $"'{target}' 프로필이 이미 있다. 덮어쓸까?{warning}", "덮어쓰기", "취소"))
                    return;
            }

            BalanceProfile saving = BalanceProfileEdit.Clone(_draft);
            saving.name = target;
            BalanceProfileEdit.Annotate(saving, _base, _reason, _memos);
            string json = PlaytestJson.Write(BalanceProfileEdit.ToJson(saving), true) + "\n";

            if (BalanceProfile.Parse(json, out string parseError) == null)
            {
                SetStatus("저장하지 않았다: " + parseError);
                return;
            }

            if (!Validate(saving, out List<string> errors))
            {
                var shown = errors.GetRange(0, Math.Min(errors.Count, MaxErrorsShown));
                SetStatus($"게임 로더 검사를 통과하지 못해 저장하지 않았다(오류 {errors.Count}개):\n" + string.Join("\n", shown));
                return;
            }

            // 다음 플레이 프로필을 먼저 고른다: 파일을 쓰면 수치 감시가 고른 프로필로 다시 검사한다.
            if (_playAfterSave)
                PlaytestSession.SelectProfile(target);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProfilesFolder);
                File.WriteAllText(path, json);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                SetStatus($"파일을 쓰지 못했다({path}): {exception.Message}");
                return;
            }

            AssetDatabase.ImportAsset(path);

            // 저장 전 Undo는 지운다: 되돌리면 저장 전의 "불러온 상태"까지 돌아가 이름 규칙이 어긋난다.
            Undo.ClearUndo(this);
            _base = BalanceProfileEdit.Clone(saving);
            _draft = BalanceProfileEdit.Clone(saving);
            _baseName = target;
            _baseFile = path;
            _reason = string.Empty;

            string played = string.Empty;
            if (_playAfterSave)
            {
                played = $" · 다음 플레이 프로필 {target}";
                PlaytestPanel panel = EditorApplication.isPlaying ? FindAnyObjectByType<PlaytestPanel>() : null;
                if (panel != null && panel.Ready)
                {
                    panel.RestartWithProfile(target);
                    played += " · 지금 판을 다시 시작했다";
                }
            }

            LoadContent();
            BuildAll();
            SetStatus((renamed ? $"메모가 있는 '{name}' 대신 새 이름으로 저장했다: " : "저장했다: ") + path + played);
        }

        // 게임과 같은 로더로 이 프로필을 적용해 불러 본다. 로더가 콘솔에 남기는 오류를 모은다.
        private bool Validate(BalanceProfile profile, out List<string> errors)
        {
            var logged = new List<string>();
            IReadOnlyList<string> patchErrors = null;

            void Collect(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                    logged.Add(message);
            }

            LoadedContent loaded;
            Application.logMessageReceived += Collect;
            try
            {
                loaded = GameContentLoader.Load(_setup, (content, nodes) => patchErrors = BalanceProfilePatcher.Apply(profile, content, nodes));
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            errors = patchErrors != null && patchErrors.Count > 0 ? new List<string>(patchErrors) : logged;
            return loaded != null && patchErrors != null && patchErrors.Count == 0;
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

        private static Label Wrapped(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static Label Dim(Label label)
        {
            label.style.opacity = 0.6f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static Label RowLabel(string text, float width)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        #endregion
    }
}
