using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 플레이 메모(메뉴 BlackHole > Playtest Notes). 플레이해 보고 마음에 안 드는 점(느낌)과 어떻게 고칠지(고칠 방향)를 적는다.
    // 세팅을 만드는 일은 테스트 세팅 창, 느낌을 적고 읽는 일은 이 창이 맡는다.
    // - 대상: 플레이 중이면 지금 판(개발 패널이 실제로 플레이한 세팅·판 상태), 아니면 테스트 세팅 창에 열린 세팅.
    // - 저장: 대상의 세팅·수치 지문(·판 상태)과 함께 PlaytestData/notes/<저장 이름>.json 파일 하나(PlaytestNotes). '고칠 방향'은 JSON 키 intent다.
    // - 저장 이름: 시나리오_프로필_번호. 같은 시나리오·프로필로 저장할 때마다 번호가 하나씩 오른다(PlaytestNotes.NextName).
    // - 목록: 대상과 같은 시나리오·프로필의 최근 메모. 지금과 다른 수치(지문)로 적은 메모는 흐리게 "이전 수치"로 보인다.
    internal sealed class PlaytestNotesWindow : EditorWindow
    {
        private const int NoChoice = int.MinValue;
        private const int MaxNotesShown = 10;
        private const double PlayCheckSeconds = 1;
        private static readonly Color ChosenColor = new Color(0.24f, 0.45f, 0.72f);

        // 쓰는 중인 메모. 창을 다시 불러도(플레이 진입) 남는다. 저장하면 비운다.
        [SerializeField] private int _difficulty = NoChoice;
        [SerializeField] private int _fun = NoChoice;
        [SerializeField] private List<string> _tags = new List<string>();
        [SerializeField] private string _text = string.Empty;
        [SerializeField] private string _fix = string.Empty;

        private Label _target;
        private VisualElement _choices;
        private TextField _textField;
        private TextField _fixField;
        private Label _status;
        private VisualElement _list;
        private (DateTime Write, long Length) _notesStamp;
        private string _shownKey;
        private double _nextPlayCheck;

        [MenuItem("BlackHole/Playtest Notes")]
        internal static void Open() => GetWindow<PlaytestNotesWindow>("플레이 메모");

        private void OnEnable()
        {
            TestSetupWindow.SetupChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            TestSetupWindow.SetupChanged -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change) => Refresh();

        // 메모 파일이 바뀌면 목록을, 플레이 중에는 1초마다 대상(판이 바뀌었나)을 다시 본다. 1초에 10번 불린다.
        private void OnInspectorUpdate()
        {
            if (_list == null)
                return;

            if (PlaytestNotes.Stamp() != _notesStamp)
            {
                Refresh();
                return;
            }

            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= _nextPlayCheck)
            {
                _nextPlayCheck = EditorApplication.timeSinceStartup + PlayCheckSeconds;
                if (GroupOf(Target(out _)) != _shownKey)
                    Refresh();
            }
        }

        private void CreateGUI()
        {
            var root = new ScrollView();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 4;
            rootVisualElement.Add(root);

            root.Add(Header("대상"));
            _target = Wrapped(string.Empty);
            root.Add(_target);

            root.Add(Header("메모 쓰기"));
            root.Add(Note("마음에 안 드는 점과 어떻게 고치면 좋을지 적는다. 저장하면 대상의 세팅·수치 지문(플레이 중이면 판 상태도)과 함께 PlaytestData/notes/<저장 이름>.json 파일 하나로 쌓인다."));
            _choices = new VisualElement();
            root.Add(_choices);
            BuildChoices();

            _textField = new TextField("느낌") { value = _text, multiline = true, tooltip = "무엇이 마음에 안 드나. 본 그대로 적는다." };
            _textField.RegisterValueChangedCallback(evt => _text = evt.newValue);
            root.Add(_textField);

            _fixField = new TextField("고칠 방향") { value = _fix, multiline = true, tooltip = "어떻게 고치면 좋겠나. 무엇을 얼마나 바꿀지." };
            _fixField.RegisterValueChangedCallback(evt => _fix = evt.newValue);
            root.Add(_fixField);

            var save = new Button(Save) { text = "메모 저장" };
            save.style.height = 24;
            save.style.marginTop = 4;
            root.Add(save);

            _status = Wrapped(string.Empty);
            root.Add(_status);

            root.Add(Header("이 시나리오·프로필의 메모"));
            _list = new VisualElement();
            root.Add(_list);

            Refresh();
        }

        // 지금 메모 대상. 플레이 중이면 개발 패널의 지금 판, 아니면 테스트 세팅 창의 세팅. 없으면 null이고 where에 이유가 있다.
        private static FeelNote Target(out string where)
        {
            if (EditorApplication.isPlaying)
            {
                PlaytestPanel panel = FindAnyObjectByType<PlaytestPanel>();

                if (panel == null || !panel.Ready)
                {
                    where = "플레이 중이지만 개발 패널이 아직 준비되지 않았다.";
                    return null;
                }

                where = "플레이 중인 판";
                return panel.NewNote("notes");
            }

            FeelNote note = TestSetupWindow.NoteForOpenSetup();
            where = note != null
                ? "테스트 세팅 창의 세팅"
                : "대상 없음 — 테스트 세팅 창(BlackHole > Test Setup)에서 세팅을 열거나, 플레이 중에 적는다.";
            return note;
        }

        // 대상과 목록을 다시 그린다(쓰던 칸은 그대로).
        private void Refresh()
        {
            if (_list == null)
                return;

            FeelNote target = Target(out string where);
            _shownKey = GroupOf(target);
            _target.text = target == null
                ? where
                : $"{where}: {Describe(target)}\n"
                  + $"시나리오 {(string.IsNullOrEmpty(target.SetupName) ? "(이름 없음)" : target.SetupName)} · "
                  + $"프로필 {(string.IsNullOrEmpty(target.Profile) ? FeelNotes.BaseProfileLabel : target.Profile)}\n"
                  + $"저장 이름 {PlaytestNotes.NextName(target.SetupName, target.Profile)}";

            _list.Clear();
            _notesStamp = PlaytestNotes.Stamp();

            if (target == null)
                return;

            IReadOnlyList<FeelNoteView> all = PlaytestNotes.ReadAll(out int skipped);
            var mine = new List<FeelNoteView>();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (FeelNotes.SameGroup(all[i], target.SetupName, target.Profile))
                    mine.Add(all[i]);
            }

            string header = $"{mine.Count}개 · {FeelNotes.NamePrefix(target.SetupName, target.Profile)}";
            if (skipped > 0)
                header += $" · 읽지 못한 줄 {skipped}개";
            _list.Add(Note(header));

            for (int i = 0; i < mine.Count && i < MaxNotesShown; i++)
                _list.Add(NoteRow(mine[i], target.Fingerprint));
        }

        // 목록을 묶는 열쇠(시나리오_프로필). 대상이 없으면 null.
        private static string GroupOf(FeelNote note) => note != null ? FeelNotes.NamePrefix(note.SetupName, note.Profile) : null;

        private static string Describe(FeelNote note)
        {
            var parts = new List<string>
            {
                $"성장 단계 {note.GrowthStage}",
                $"노드 {note.Nodes.Count}개",
            };

            if (note.Battle != null)
                parts.Add($"Lv {note.Battle.Level} · {note.Battle.Elapsed:0.#}초");
            return string.Join(" · ", parts);
        }

        #region 쓰기

        // 난이도·재미·태그(누를 때마다 다시 그림). 글 칸은 한 번만 만든다(쓰는 중 포커스를 잃지 않게).
        private void BuildChoices()
        {
            _choices.Clear();

            var difficulty = Row();
            difficulty.Add(RowLabel("난이도"));
            for (int value = FeelNote.DifficultyMin; value <= FeelNote.DifficultyMax; value++)
            {
                int chosen = value;
                difficulty.Add(Choice(FeelNotes.DifficultyLabel(value), _difficulty == value, () =>
                    _difficulty = _difficulty == chosen ? NoChoice : chosen));
            }
            _choices.Add(difficulty);

            var fun = Row();
            fun.Add(RowLabel("재미"));
            for (int value = FeelNote.FunMin; value <= FeelNote.FunMax; value++)
            {
                int chosen = value;
                fun.Add(Choice(value.ToString(CultureInfo.InvariantCulture), _fun == value, () =>
                    _fun = _fun == chosen ? NoChoice : chosen));
            }
            _choices.Add(fun);

            string group = null;
            VisualElement row = null;
            foreach ((string tagGroup, string id, string label) in FeelNotes.Tags)
            {
                if (tagGroup != group)
                {
                    group = tagGroup;
                    row = Row();
                    row.style.flexWrap = Wrap.Wrap;
                    row.Add(RowLabel(tagGroup));
                    _choices.Add(row);
                }

                string tagId = id;
                row.Add(Choice(label, _tags.Contains(id), () =>
                {
                    if (!_tags.Remove(tagId))
                        _tags.Add(tagId);
                }));
            }
        }

        private Button Choice(string text, bool chosen, Action toggle)
        {
            var button = new Button(() =>
            {
                toggle();
                BuildChoices();
            })
            {
                text = text,
            };

            if (chosen)
                button.style.backgroundColor = ChosenColor;

            return button;
        }

        private bool HasInput =>
            _difficulty != NoChoice || _fun != NoChoice || _tags.Count > 0
            || _text.Trim().Length > 0 || _fix.Trim().Length > 0;

        private void Save()
        {
            if (!HasInput)
            {
                _status.text = "난이도·재미·태그·느낌·고칠 방향 중 하나는 적어야 저장한다.";
                return;
            }

            FeelNote note = Target(out string where);

            if (note == null)
            {
                _status.text = where;
                return;
            }

            note.Difficulty = _difficulty != NoChoice ? _difficulty : (int?)null;
            note.Fun = _fun != NoChoice ? _fun : (int?)null;
            note.Tags.AddRange(_tags);
            note.Text = _text.Trim();
            note.Intent = _fix.Trim();

            if (!PlaytestNotes.TrySave(note, out string error))
            {
                _status.text = $"메모를 저장하지 못했다: {error}";
                return;
            }

            _difficulty = NoChoice;
            _fun = NoChoice;
            _tags.Clear();
            _text = string.Empty;
            _fix = string.Empty;
            _textField.SetValueWithoutNotify(string.Empty);
            _fixField.SetValueWithoutNotify(string.Empty);
            BuildChoices();

            _status.text = $"메모 {note.Name}를 저장했다({where}).";
            Refresh();
        }

        #endregion

        #region 목록

        private static VisualElement NoteRow(FeelNoteView note, string currentFingerprint)
        {
            var box = new VisualElement();
            box.style.marginBottom = 6;
            box.style.paddingLeft = 4;
            box.style.borderLeftWidth = 2;
            box.style.borderLeftColor = new Color(0.5f, 0.5f, 0.55f);

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(note.Name))
                parts.Add(note.Name);
            parts.Add(LocalTime(note.AtUtc));
            if (note.Difficulty.HasValue)
                parts.Add(FeelNotes.DifficultyLabel(note.Difficulty.Value));
            if (note.Fun.HasValue)
                parts.Add($"재미 {note.Fun.Value}");
            foreach (string tag in note.Tags)
                parts.Add("#" + FeelNotes.TagLabel(tag));
            if (note.HasBattle)
                parts.Add($"Lv {note.BattleLevel} · {note.BattleElapsed:0.#}초");
            if (!string.IsNullOrEmpty(note.Profile))
                parts.Add(note.Profile);

            bool old = !string.IsNullOrEmpty(currentFingerprint) && note.Fingerprint != currentFingerprint;
            if (old)
                parts.Add($"이전 수치({note.Fingerprint})");

            var head = new Label(string.Join(" · ", parts));
            head.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.style.whiteSpace = WhiteSpace.Normal;
            box.Add(head);

            if (note.Text.Length > 0)
                box.Add(Wrapped("느낌: " + note.Text));
            if (note.Intent.Length > 0)
                box.Add(Wrapped("고칠 방향: " + note.Intent));

            if (old)
                box.style.opacity = 0.5f;

            return box;
        }

        private static string LocalTime(string atUtc) =>
            DateTime.TryParse(atUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime time)
                ? time.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
                : atUtc;

        #endregion

        #region 모양

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

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Label RowLabel(string text)
        {
            var label = new Label(text);
            label.style.width = 52;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        #endregion
    }
}
