#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace BlackHole.Unity
{
    // 개발 패널(에디터·개발 빌드). F1 또는 세 손가락 터치로 열고 닫는다. GameBootstrap이 붙인다.
    // 탭: 시나리오(정해 둔 시점에서 시작) / 프로필(밸런스 값 고르기) / 판(시간·Level·배속·끝내기) / 적(소환·치우기·멈추기) / 메모(느낌 기록).
    // HUD는 패널과 따로 켜고 끈다(판이 있을 때만 보인다).
    // 판을 조작하면(배속 1이 아님 포함) 그 판의 전투 요약에 테스트 표시(+test)가 붙는다(ContentTag).
    // 패널이 열려 있는 동안 게임 UI 입력(EventSystem)을 막고, 고른 경우 판도 멈춘다.
    internal sealed class PlaytestPanel : MonoBehaviour
    {
        private enum Tab { Scenario, Profile, Battle, Enemies, Note }

        private static readonly string[] TabNames = { "시나리오", "프로필", "판", "적", "메모" };
        private static readonly float[] TimeScales = { 0.25f, 0.5f, 1f, 2f, 4f, 8f };
        private static readonly float[] MoveScales = { 0f, 0.5f, 1f, 2f };
        private static readonly int[] SpawnCounts = { 1, 5, 20 };

        // GUI 배율의 기준: 화면의 짧은 변이 이만큼이면 1배.
        private const float ReferenceShortSide = 540f;
        private const float PanelWidth = 400f;
        private const float HudWidth = 340f;
        private const float Margin = 8f;
        private const int ToggleFingers = 3;

        private PlaytestSession _session;
        private BattleSystem _battle;
        private ScreenFlow _screens;
        private GameContent _content;
        // 노드 정의(메모에 노드마다 효과를 적을 때 쓴다).
        private NodeTree _nodeTree;
        private BattleAnalytics _analytics;
        private readonly PlaytestHud _hud = new();
        private string[] _kindNames;

        private bool _open;
        private Tab _tab = Tab.Scenario;
        private Vector2 _scroll;
        private bool _pauseOnOpen = true;
        private bool _showHud = true;
        private int _hudCorner;
        private float _timeScale = 1f;
        private GameSession _pausedByPanel;
        private EventSystem _blockedEventSystem;
        private bool _touchHeld;
        private string _status = "";

        // 시나리오
        // 마지막으로 넣은 시나리오(파일이거나 테스트 세팅 창에서 넘긴 세팅). 같은 시드로 다시와 HUD가 쓴다.
        private PlaytestScenario _lastScenario;
        private PlaytestScenario _checkedScenario;
        // 테스트 세팅 창이 플레이를 시작하며 맡긴 세팅. 첫 Update에서 판을 시작한다(에디터만).
        private PlaytestScenario _pendingLaunch;
        private ProgressState _progress;

        // 수치 실시간 반영(M2, 에디터만). 수치 파일이 바뀌면 지금 세팅으로 장면을 다시 시작한다(끌 수 있다).
#if UNITY_EDITOR
        private const string LiveRestartKey = "playtest.liveRestart";
        private bool _restartOnDataChange = true;
        // 바뀐 수치를 아직 적용하지 않았다(다시 시작을 껐거나 기다리는 중).
        private bool _dataChanged;
#endif
        private bool _restartRequested;
        // HUD와 패널에 보이는 수치 상태 한 줄. 없으면 null(개발 빌드는 늘 null).
        private string _liveStatus = null;
        private readonly List<string> _scenarioErrors = new();
        private readonly List<string> _scenarioWarnings = new();

        // 프로필: 확인을 기다리는 프로필 이름(""는 원본). null이면 없다.
        private string _pendingProfile;

        // 적
        private int _kindIndex;
        private int _tier;
        private int _size = SizeRule.Base;
        private int _traitIndex; // 0이면 성질 없음, 1부터 이 판 구성의 성질.

        // 메모
        private int? _noteDifficulty;
        private int? _noteFun;
        private readonly List<string> _noteTags = new();
        private string _noteText = "";
        private string _noteIntent = "";
        private int _savedNotes;
        // 메모 탭 머리: 다음 저장 이름과 같은 시나리오·프로필로 쌓인 메모(최근 것부터). 0.5초마다 다시 읽는다.
        private const float NoteGroupRefreshSeconds = 0.5f;
        private const int MaxRecentNotes = 3;
        private string _noteGroupFor;
        private float _noteGroupAt;
        private string _nextNoteName;
        private int _noteGroupCount;
        private readonly List<string> _noteGroupRecent = new();

        // 지금(또는 마지막) 판이 시작한 세팅. 판이 바뀌는 순간 뜬다. 메모의 setup은 이것이다
        // (판이 끝나 결산이 진행 상태를 바꾼 뒤에 적어도 그 판의 세팅이 남는다).
        private GameSession _trackedSession;
        private BattleSetup _battleSetup;
        // 다음에 시작할 판을 연 시나리오·세팅. 그 판을 보면 지운다.
        private PlaytestScenario _scenarioForNextBattle;

        private sealed class BattleSetup
        {
            public string Name;
            public int GrowthStage;
            public int StartLevel;
            public int Seed;
            public long Gold;
            public readonly List<(string NodeId, int Rank)> Nodes = new();
            // 판이 시작할 때 읽은 계산된 수치(NoteStats). 판 밖에서 적는 메모가 쓴다.
            public JsonObject Stats;
        }

        private bool _stylesReady;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _error;
        private GUIStyle _hudStyle;
        private GUIContent _hudContent = new();
        private string _hudContentText;

        internal void Initialize(
            PlaytestSession session,
            BattleSystem battle,
            ScreenFlow screens,
            GameContent content,
            NodeTree nodeTree,
            ProgressState progress,
            BattleAnalytics analytics)
        {
            _session = session;
            _battle = battle;
            _screens = screens;
            _content = content;
            _nodeTree = nodeTree;
            _progress = progress;
            _analytics = analytics;
#if UNITY_EDITOR
            _pendingLaunch = TestSetupLaunch.Take();
            _restartOnDataChange = PlayerPrefs.GetInt(LiveRestartKey, 1) == 1;
            LiveDataSignal.Changed += OnLiveDataChanged;
#endif

            IReadOnlyList<EnemyDefinition> kinds = content.Enemies.Enemies;
            _kindNames = new string[kinds.Count];
            for (int i = 0; i < kinds.Count; i++)
                _kindNames[i] = PlaytestNames.Of(kinds[i].Type);

            if (session.ProfileProblem != null)
                _status = session.ProfileProblem;
        }

        private void Update()
        {
            if (_session == null)
                return;

            // 장면을 다시 부르는 일은 신호를 받은 자리(에디터 update)가 아니라 여기(게임 Update)에서 한다.
            if (_restartRequested)
            {
                _restartRequested = false;
                RestartForNewData();
                return;
            }

            if (_pendingLaunch != null)
            {
                PlaytestScenario launch = _pendingLaunch;
                _pendingLaunch = null;

                if (!RunScenario(launch, true, null))
                    Debug.LogError($"[테스트] 세팅 '{launch.name}'을 넣지 못했다.\n  {string.Join("\n  ", _scenarioErrors)}");
            }

            TrackBattleSetup();

            if (TogglePressed())
                SetOpen(!_open);

            // 배속은 판에만 건다. 판이 없으면(결산·업그레이드 화면) 원래 속도다.
            GameSession session = _battle.Session;
            float wanted = session != null ? _timeScale : 1f;

            if (Time.timeScale != wanted)
                Time.timeScale = wanted;

            if (session != null && _timeScale != 1f)
                _session.Tag.MarkTestBattle();
        }

        // 판이 이번 프레임의 Step을 마친 뒤에 잰다(GameHost는 Update에서 판을 진행한다).
        private void LateUpdate()
        {
            if (_session != null)
                _hud.Update(_battle.Session, _session.Tag, _lastScenario?.name, _session.Fingerprint, _liveStatus, Time.timeScale, Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            BlockGameInput(false);
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            LiveDataSignal.Changed -= OnLiveDataChanged;
#endif
        }

#if UNITY_EDITOR
        // 에디터의 수치 감시가 새 값을 검사한 뒤 부른다.
        private void OnLiveDataChanged(LiveDataChange change)
        {
            if (!change.ContentChanged || _session == null)
                return;

            if (!change.Valid)
            {
                _liveStatus = $"수치 오류 {change.Errors.Count}개 — 이전 값으로 계속 ({change.Files})";
                _status = _liveStatus;
                return;
            }

            if (change.Fingerprint == _session.Fingerprint)
            {
                // 저장만 다시 했거나 값이 그대로다.
                _liveStatus = _dataChanged ? _liveStatus : null;
                return;
            }

            _dataChanged = true;
            _liveStatus = $"수치 바뀜 {_session.Fingerprint} → {change.Fingerprint} ({change.Files})";

            if (_restartOnDataChange)
                _restartRequested = true;
            else
                _status = _liveStatus + " · 판 탭의 '새 수치로 다시 시작'으로 적용한다.";
        }
#endif

        // 새 수치로 장면을 다시 부른다. 마지막 세팅(같은 시드)이나 지금 진행으로 판을 바로 다시 시작한다.
        // 판도 세팅도 없으면(타이틀·업그레이드 화면의 보통 플레이) 장면만 다시 부른다(저장에서 다시 불러온다).
        private void RestartForNewData()
        {
            PlaytestScenario setup = SetupForRestart();
#if UNITY_EDITOR
            if (setup != null)
                TestSetupLaunch.Request(setup);
#endif
            Debug.Log($"[테스트] 새 수치로 다시 시작한다{(setup != null ? $"(세팅 '{setup.name}', 시드 {setup.seed})" : "")}.");
            ReloadScene();
        }

        private PlaytestScenario SetupForRestart()
        {
            if (_lastScenario != null)
            {
                PlaytestScenario copy = PlaytestScenario.Parse(JsonUtility.ToJson(_lastScenario), out _);

                if (copy != null && _battle.LastSeed != 0)
                    copy.seed = _battle.LastSeed;

                return copy;
            }

            GameSession session = _battle.Session;

            if (session == null || _progress == null)
                return null;

            var setup = new PlaytestScenario
            {
                name = "지금 진행",
                growthStage = _progress.GrowthStage,
                gold = _progress.Gold,
                startLevel = session.World.Hq.Level,
                seed = _battle.LastSeed,
            };

            foreach (string id in _progress.OwnedNodes)
                setup.nodes.Add(new PlaytestScenario.Node { nodeId = id, rank = _progress.RankOf(id) });

            return setup;
        }

        private bool TogglePressed()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                return true;

            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen == null)
                return false;

            int pressed = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                    pressed++;
            }

            // 세 손가락이 닿은 순간 한 번만.
            bool held = pressed >= ToggleFingers;
            bool toggled = held && !_touchHeld;
            _touchHeld = held;
            return toggled;
        }

        // 판을 바꾸는 동작(시나리오·판 시작·끝내기)으로 닫을 때는 resume을 false로 준다 — 지금 판은 곧 버리거나 끝내므로 다시 움직이지 않는다.
        private void SetOpen(bool open, bool resume = true)
        {
            if (_open == open)
                return;

            _open = open;
            BlockGameInput(open);

            if (open)
            {
                if (_pauseOnOpen && _battle.IsRunning)
                {
                    _battle.SetPaused(true);
                    _pausedByPanel = _battle.Session;
                }

                return;
            }

            // 패널이 멈춘 그 판이면 다시 움직인다(일시 정지 창이 멈춘 판은 그대로 둔다).
            if (resume && _pausedByPanel != null && _battle.Session == _pausedByPanel && _battle.IsPaused)
                _battle.SetPaused(false);

            _pausedByPanel = null;
        }

        // IMGUI는 게임 UI(uGUI) 클릭을 막지 못한다. 패널이 열려 있는 동안 EventSystem을 끈다.
        private void BlockGameInput(bool block)
        {
            if (block)
            {
                EventSystem current = EventSystem.current;

                if (current != null && current.enabled)
                {
                    current.enabled = false;
                    _blockedEventSystem = current;
                }

                return;
            }

            if (_blockedEventSystem != null)
                _blockedEventSystem.enabled = true;

            _blockedEventSystem = null;
        }

        private void MarkTest() => _session.Tag.MarkTestBattle();

        private void OnGUI()
        {
            if (_session == null)
                return;

            EnsureStyles();

            float scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / ReferenceShortSide);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Rect safe = SafeArea(scale);

            if (_showHud && _hud.Text.Length > 0)
                DrawHud(safe);

            if (_open)
                DrawPanel(safe);
        }

        // 노치·둥근 모서리를 피한 영역을 GUI 좌표(왼쪽 위 원점, 배율 적용)로.
        private static Rect SafeArea(float scale)
        {
            Rect safe = Screen.safeArea;
            return new Rect(safe.x / scale, (Screen.height - safe.yMax) / scale, safe.width / scale, safe.height / scale);
        }

        private void DrawHud(Rect safe)
        {
            if (!ReferenceEquals(_hudContentText, _hud.Text))
            {
                _hudContentText = _hud.Text;
                _hudContent = new GUIContent(_hudContentText);
            }

            float width = Mathf.Min(HudWidth, safe.width - Margin * 2);
            float height = _hudStyle.CalcHeight(_hudContent, width);
            bool right = _hudCorner == 1 || _hudCorner == 3;
            bool bottom = _hudCorner >= 2;
            float x = right ? safe.xMax - width - Margin : safe.x + Margin;
            float y = bottom ? safe.yMax - height - Margin : safe.y + Margin;

            GUI.Box(new Rect(x, y, width, height), _hudContent, _hudStyle);
        }

        private void DrawPanel(Rect safe)
        {
            float width = Mathf.Min(PanelWidth, safe.width - Margin * 2);
            var area = new Rect(safe.xMax - width - Margin, safe.y + Margin, width, safe.height - Margin * 2);

            // 터치는 스크롤 막대를 잡기 어렵다. 패널 안에서 끌면 내용을 굴린다.
            Event current = Event.current;
            if (Touchscreen.current != null && current.type == EventType.MouseDrag && area.Contains(current.mousePosition))
            {
                _scroll.y -= current.delta.y;
                current.Use();
            }

            // 기본 상자는 반투명이라 두 번 그려 진하게 한다.
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 6, area.y + 6, area.width - 12, area.height - 12));

            GUILayout.BeginHorizontal();
            string version = _session.Tag.ContentVersion;
            GUILayout.Label($"테스트 도구 · {(version.Length > 0 ? version : "원본")}", _title);
            if (GUILayout.Button("닫기 (F1)", GUILayout.Width(80)))
                SetOpen(false);
            GUILayout.EndHorizontal();

            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames);
            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case Tab.Scenario:
                    DrawScenarios();
                    break;
                case Tab.Profile:
                    DrawProfiles();
                    break;
                case Tab.Battle:
                    DrawBattle();
                    break;
                case Tab.Enemies:
                    DrawEnemies();
                    break;
                case Tab.Note:
                    DrawNote();
                    break;
            }

            GUILayout.EndScrollView();

            if (_status.Length > 0)
                GUILayout.Label(_status, _small);

            GUILayout.EndArea();
        }

        // ── 시나리오 ──────────────────────────────────────────────

        private void DrawScenarios()
        {
            GUILayout.Label("정해 둔 시점(성장도·Gold·노드·Level)에서 바로 시작한다. 시나리오를 쓰면 이번 실행은 저장하지 않고, 전투 요약에 +test가 붙는다.", _small);

            if (_session.Scenarios.Count == 0)
                GUILayout.Label($"시나리오가 없다. {PlaytestLibrary.ScenariosFolder}나 기기 폴더에 JSON을 둔다.", _small);

            foreach (ScenarioOption option in _session.Scenarios)
            {
                GUILayout.BeginVertical(GUI.skin.box);

                if (option.Scenario == null)
                {
                    GUILayout.Label($"{option.File}: 읽지 못했다. {option.Error}", _error);
                    GUILayout.EndVertical();
                    continue;
                }

                PlaytestScenario scenario = option.Scenario;
                GUILayout.Label($"[{scenario.category ?? "기본"}] {scenario.expected}", _small);
                GUILayout.Label($"{scenario.name}  ({option.File.Source})", _label);

                if (!string.IsNullOrEmpty(scenario.note))
                    GUILayout.Label(scenario.note, _small);

                GUILayout.Label(Describe(scenario), _small);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("판 시작"))
                    RunScenario(scenario, true, null);
                if (GUILayout.Button("업그레이드 화면으로"))
                    RunScenario(scenario, false, null);
                GUILayout.EndHorizontal();

                if (scenario == _checkedScenario)
                {
                    foreach (string error in _scenarioErrors)
                        GUILayout.Label("오류: " + error, _error);
                    foreach (string warning in _scenarioWarnings)
                        GUILayout.Label("맞춤: " + warning, _small);
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            if (GUILayout.Button("파일 다시 읽기"))
                Refresh();

            DrawFileProblems();
            GUILayout.Label($"기기 폴더: {PlaytestFiles.DeviceScenarios}", _small);
        }

        private static string Describe(PlaytestScenario scenario)
        {
            var parts = new List<string>
            {
                $"성장도 {scenario.growthStage}",
                $"Gold {scenario.gold:N0}",
                $"노드 {scenario.nodes.Count}개",
            };

            if (scenario.autoBuyBudget > 0)
                parts.Add($"자동 구매 {scenario.autoBuyBudget:N0}");
            if (scenario.startLevel > 0)
                parts.Add($"Lv {scenario.startLevel}에서");
            if (scenario.seed != 0)
                parts.Add($"시드 {scenario.seed}");
            if (scenario.freezeTime)
                parts.Add("시간 고정");

            return string.Join(" · ", parts);
        }

        // 테스트 세팅 창(에디터)이 부른다: 세팅을 검사하고, 맞으면 판을 시작한다(startBattle이 false면 업그레이드 화면).
        // 틀리면 지금 판·진행을 건드리지 않고 errors에 이유를 더한다.
        internal bool RunSetup(PlaytestScenario setup, bool startBattle, List<string> errors)
        {
            bool started = RunScenario(setup, startBattle, null);
            errors?.AddRange(_scenarioErrors);
            return started;
        }

        // 지금 진행 상태(성장도·Gold·산 노드)와 진행 중인 판의 Level. 테스트 세팅 창이 "지금 게임에서 가져오기"로 읽는다.
        internal ProgressState Progress => _progress;
        internal int BattleSeed => _battle.LastSeed;

        internal int? BattleLevel => _battle.Session?.World.Hq.Level;

        private bool RunScenario(PlaytestScenario scenario, bool startBattle, int? seed)
        {
            _checkedScenario = scenario;
            _scenarioErrors.Clear();
            _scenarioWarnings.Clear();

            // 빈 진행 상태에 먼저 넣어 본다. 틀리면 지금 판·진행을 건드리지 않는다.
            if (!_screens.CheckScenario(scenario, _scenarioErrors, _scenarioWarnings))
            {
                _status = $"시나리오 '{scenario.name}'에 오류가 있어 시작하지 않았다.";
                return false;
            }

            if (!string.IsNullOrEmpty(scenario.contentFingerprint) && scenario.contentFingerprint != _session.Fingerprint)
                _scenarioWarnings.Add("저장 당시와 콘텐츠 수치가 다릅니다.");
            _session.Tag.MarkTestSession();
            _lastScenario = scenario;
            _scenarioForNextBattle = startBattle ? scenario : null;

            int? useSeed = seed ?? (scenario.seed != 0 ? scenario.seed : (int?)null);
            Action<GameSession> afterStart = startBattle ? battle => AfterScenarioStart(battle, scenario) : null;
            _screens.ApplyScenario(scenario, useSeed, startBattle, afterStart);

            _status = $"시나리오 '{scenario.name}'" + (startBattle ? "로 판을 시작한다." : "의 진행 상태로 업그레이드 화면에 간다.");
            SetOpen(false, resume: false);
            return true;
        }

        private void AfterScenarioStart(GameSession battle, PlaytestScenario scenario)
        {
            try
            {
                if (scenario.startLevel > 0)
                    BattleCheats.ReachLevel(battle, scenario.startLevel);

                if (scenario.freezeTime)
                    BattleCheats.SetTimeFrozen(battle, true);
            }
            catch (ArgumentException error)
            {
                _status = $"시나리오 '{scenario.name}': {error.Message}";
                Debug.LogWarning($"[테스트] {_status}");
            }
        }

        // ── 프로필 ──────────────────────────────────────────────

        private void DrawProfiles()
        {
            GUILayout.Label("콘텐츠 값을 덮어쓴다(원본 에셋은 그대로). 고르면 장면을 다시 불러 적용한다. 진행 저장은 프로필마다 따로다.", _small);

            BalanceProfile applied = _session.AppliedProfile;
            GUILayout.Label($"지금: {(applied != null ? applied.name : "원본")}", _label);

            if (_session.ProfileProblem != null)
            {
                GUILayout.Label(_session.ProfileProblem, _error);
                foreach (string error in _session.ProfileErrors)
                    GUILayout.Label("· " + error, _error);
            }

            if (applied != null)
            {
                if (!string.IsNullOrEmpty(applied.note))
                    GUILayout.Label(applied.note, _small);

                foreach (BalanceProfile.Patch patch in applied.patches)
                    GUILayout.Label($"  {patch.path} = {patch.value}", _small);
            }

            GUILayout.Space(6);

            if (_pendingProfile != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                string name = _pendingProfile.Length > 0 ? _pendingProfile : "원본";
                GUILayout.Label($"'{name}'(으)로 다시 시작한다. 진행 중인 판은 버린다(결산·통계 없음). 판이나 세팅이 있으면 같은 세팅·같은 시드로 다시 시작한다.", _label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("다시 시작"))
                    RestartWithProfile(_pendingProfile);
                if (GUILayout.Button("취소"))
                    _pendingProfile = null;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            DrawProfileRow("", "원본", "에셋 값 그대로", applied == null);

            foreach (ProfileOption option in _session.Profiles)
            {
                if (option.Profile == null)
                {
                    GUILayout.Label($"{option.File}: 읽지 못했다. {option.Error}", _error);
                    continue;
                }

                BalanceProfile profile = option.Profile;
                string detail = $"값 {profile.patches.Count}개 · {option.File.Source}"
                                + (string.IsNullOrEmpty(profile.note) ? "" : $" · {profile.note}");
                DrawProfileRow(profile.name, profile.name, detail, applied != null && applied.name == profile.name);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("파일 다시 읽기"))
                Refresh();

            DrawFileProblems();
            GUILayout.Label($"기기 폴더: {PlaytestFiles.DeviceProfiles}", _small);
        }

        private void DrawProfileRow(string name, string label, string detail, bool current)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical();
            GUILayout.Label(current ? $"● {label}" : label, _label);
            GUILayout.Label(detail, _small);
            GUILayout.EndVertical();

            GUI.enabled = !current;
            if (GUILayout.Button("적용", GUILayout.Width(56)))
                _pendingProfile = name;
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        // 프로필을 바꿔 다시 시작한다. 판이나 세팅이 있으면 같은 세팅·같은 시드로 판을 바로 다시 시작한다(원본과 AI 초안 비교).
        // 테스트 세팅 창의 "초안 켜기·원본으로"도 플레이 중에는 이것을 부른다.
        internal void RestartWithProfile(string profile)
        {
            PlaytestSession.SelectProfile(profile ?? string.Empty);
            _pendingProfile = null;
            SetOpen(false, resume: false);
            RestartForNewData();
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // 빌드 설정에 없는 장면도 다시 불러온다.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        private void Refresh()
        {
            _session.Refresh();

            // 마지막 시나리오가 파일에 있으면 새로 읽은 것으로 바꾼다(테스트 세팅 창이 넘긴 세팅은 그대로).
            ScenarioOption fresh = _lastScenario == null
                ? null
                : _session.Scenarios.Find(option => option.Scenario != null && option.Scenario.name == _lastScenario.name);
            if (fresh != null)
                _lastScenario = fresh.Scenario;

            _checkedScenario = null;
            _status = $"프로필 {_session.Profiles.Count}개, 시나리오 {_session.Scenarios.Count}개를 읽었다.";
        }

        private void DrawFileProblems()
        {
            foreach (string problem in _session.FileProblems)
                GUILayout.Label(problem, _error);
        }

        // ── 판 ──────────────────────────────────────────────

        private void DrawBattle()
        {
            _pauseOnOpen = GUILayout.Toggle(_pauseOnOpen, " 패널을 열면 판을 멈춘다");
#if UNITY_EDITOR
            bool restart = GUILayout.Toggle(_restartOnDataChange, " 수치가 바뀌면 지금 세팅으로 다시 시작");
            if (restart != _restartOnDataChange)
            {
                _restartOnDataChange = restart;
                PlayerPrefs.SetInt(LiveRestartKey, restart ? 1 : 0);
            }

            if (_liveStatus != null)
                GUILayout.Label(_liveStatus, _dataChanged ? _small : _error);

            if (_dataChanged && GUILayout.Button("새 수치로 다시 시작"))
            {
                SetOpen(false, resume: false);
                RestartForNewData();
            }
#endif

            GUILayout.BeginHorizontal();
            _showHud = GUILayout.Toggle(_showHud, " HUD");
            if (GUILayout.Button("HUD 위치 바꾸기"))
                _hudCorner = (_hudCorner + 1) % 4;
            GUILayout.EndHorizontal();

            GUILayout.Label("배속(판에만 건다. 1이 아니면 +test)", _small);
            GUILayout.BeginHorizontal();
            foreach (float scale in TimeScales)
            {
                if (GUILayout.Toggle(_timeScale == scale, $"×{scale:0.##}", GUI.skin.button))
                    _timeScale = scale;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GameSession session = _battle.Session;

            if (session == null)
            {
                GUILayout.Label("진행 중인 판이 없다.", _label);

                if (GUILayout.Button("지금 진행 상태로 판 시작"))
                {
                    _screens.StartTestBattle(null, null);
                    SetOpen(false, resume: false);
                }

                DrawReplay();
                return;
            }

            Hq hq = session.World.Hq;
            GUILayout.Label($"시드 {_battle.LastSeed} · {(_battle.IsPaused ? "멈춤" : "진행")} · Lv {hq.Level} · 남은 {session.Remaining:0.0}s", _label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10초"))
                Cheat(() => BattleCheats.AddTime(session, 10f), "제한 시간 +10초");
            if (GUILayout.Button("+60초"))
                Cheat(() => BattleCheats.AddTime(session, 60f), "제한 시간 +60초");
            bool frozen = BattleCheats.IsTimeFrozen(session);
            if (GUILayout.Toggle(frozen, "시간 고정", GUI.skin.button) != frozen)
                Cheat(() => BattleCheats.SetTimeFrozen(session, !frozen), frozen ? "시간 고정 끔" : "시간 고정 켬");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Level +1"))
                Cheat(() =>
                {
                    if (!BattleCheats.RaiseLevel(session))
                        throw new InvalidOperationException("더 올릴 Level이 없다(목표 Level 또는 사다리 끝).");
                }, "다음 Step에 Level이 오른다");
            if (hq.GoalLevel != HqGrowthDefinition.NoGoal && hq.Level < hq.GoalLevel - 1
                && GUILayout.Button($"목표 직전(Lv {hq.GoalLevel - 1})"))
                Cheat(() => BattleCheats.ReachLevel(session, hq.GoalLevel - 1), $"Lv {hq.GoalLevel - 1}까지 EXP를 채웠다");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("판 끝내기(결산)"))
            {
                _screens.EndBattleNow();
                SetOpen(false, resume: false);
            }
            GUILayout.EndHorizontal();

            DrawReplay();
        }

        // 같은 시드로 다시: 마지막 시나리오가 있으면 그 시점부터, 없으면 지금 진행 상태로.
        private void DrawReplay()
        {
            if (_battle.LastSeed == 0 && _lastScenario == null)
                return;

            string from = _lastScenario != null ? $"시나리오 '{_lastScenario.name}'부터" : "지금 진행 상태로";

            if (!GUILayout.Button($"같은 시드({_battle.LastSeed})로 다시 · {from}"))
                return;

            int seed = _battle.LastSeed;

            if (_lastScenario != null)
            {
                RunScenario(_lastScenario, true, seed);
                return;
            }

            _screens.StartTestBattle(seed, null);
            SetOpen(false, resume: false);
        }

        // 판 조작 하나. 성공하면 테스트 표시를 붙인다. 규칙 밖의 값은 거부되고 이유를 보인다.
        private void Cheat(Action action, string done)
        {
            try
            {
                action();
                MarkTest();
                _status = done;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                _status = error.Message;
            }
        }

        // ── 적 ──────────────────────────────────────────────

        private void DrawEnemies()
        {
            GameSession session = _battle.Session;

            if (session == null)
            {
                GUILayout.Label("판이 있을 때 쓴다(판 탭에서 시작).", _label);
                return;
            }

            IReadOnlyList<EnemyDefinition> kinds = _content.Enemies.Enemies;

            if (kinds.Count == 0)
                return;

            _kindIndex = Mathf.Clamp(GUILayout.Toolbar(_kindIndex, _kindNames), 0, kinds.Count - 1);
            EnemyDefinition kind = kinds[_kindIndex];
            EnemyComposition composition = session.World.Stats.CompositionOf(kind);

            _tier = Stepper("색 등급", _tier, 0, kind.Tiers.Count - 1, 1);
            _size = Stepper("크기", _size, SizeRule.Base, composition.Size, 0);

            IReadOnlyList<EnemyTraitDefinition> traits = composition.Traits;
            var traitNames = new string[traits.Count + 1];
            traitNames[0] = "성질 없음";
            for (int i = 0; i < traits.Count; i++)
                traitNames[i + 1] = PlaytestNames.Of(traits[i].Type);

            _traitIndex = Mathf.Clamp(_traitIndex, 0, traits.Count);
            _traitIndex = GUILayout.Toolbar(_traitIndex, traitNames);
            EnemyTraitType? trait = _traitIndex == 0 ? (EnemyTraitType?)null : traits[_traitIndex - 1].Type;

            EnemyTier tier = kind.Tiers[_tier];
            GUILayout.Label($"이 등급: HP {tier.MaxHealth:N0} · Gold {tier.Gold:N0} · EXP {tier.Exp:N0} (크기·성질 배율 전)", _small);

            GUILayout.BeginHorizontal();
            foreach (int count in SpawnCounts)
            {
                if (GUILayout.Button($"{count}마리 소환"))
                    Spawn(session, kind, trait, count);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("모두 치우기"))
                Cheat(() => BattleCheats.ClearEnemies(session), "살아 있는 적을 치웠다(처치 아님)");
            GUILayout.EndHorizontal();

            float moveScale = BattleCheats.EnemyMoveScaleOf(session);
            GUILayout.Label("적 이동 배율", _small);
            GUILayout.BeginHorizontal();
            foreach (float scale in MoveScales)
            {
                bool on = Mathf.Approximately(moveScale, scale);
                if (GUILayout.Toggle(on, $"×{scale:0.#}", GUI.skin.button) && !on)
                    Cheat(() => BattleCheats.SetEnemyMoveScale(session, scale), $"적 이동 ×{scale:0.#}");
            }
            GUILayout.EndHorizontal();
        }

        private void Spawn(GameSession session, EnemyDefinition kind, EnemyTraitType? trait, int count)
        {
            int made = 0;
            Cheat(() => made = BattleCheats.Spawn(session, kind, _tier, trait, _size, count), "");

            if (_status.Length == 0)
                _status = made < count ? $"{made}마리 소환(동시 상한에 닿았다)" : $"{made}마리 소환";
        }

        // - 값 + 버튼. display는 화면에 보일 때 더하는 값(색 등급은 1부터 보인다).
        private int Stepper(string label, int value, int min, int max, int display)
        {
            value = Mathf.Clamp(value, min, Mathf.Max(min, max));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value + display} ({min + display}~{max + display})", _label, GUILayout.Width(170));
            if (GUILayout.Button("-") && value > min)
                value--;
            if (GUILayout.Button("+") && value < max)
                value++;
            GUILayout.EndHorizontal();

            return value;
        }

        // ── 메모 ──────────────────────────────────────────────

        private void DrawNote()
        {
            // 이 메모가 붙는 시나리오·프로필(지금 판 기준, 잠김). 바꾸려면 시나리오·프로필 탭에서 판을 다시 시작한다.
            string setup = NoteSetupName();
            string profile = _session.AppliedProfile != null ? _session.AppliedProfile.name : string.Empty;
            RefreshNoteGroup(setup, profile);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            LockedField("시나리오", setup);
            LockedField("프로필", profile.Length > 0 ? profile : FeelNotes.BaseProfileLabel);
            LockedField("저장 이름", _nextNoteName);
            GUI.enabled = wasEnabled;

            GUILayout.Label($"이 시나리오·프로필 메모 {_noteGroupCount}개", _small);
            foreach (string line in _noteGroupRecent)
                GUILayout.Label(line, _small);

            GUILayout.Label("느낌과 고칠 방향을 적는다. 저장하면 이 판의 세팅·수치 지문·판 상태와 함께 '저장 이름.json' 파일 하나로 쌓인다(에디터의 플레이 메모 창과 같은 폴더).", _small);

            GUILayout.Label("난이도", _small);
            GUILayout.BeginHorizontal();
            for (int value = FeelNote.DifficultyMin; value <= FeelNote.DifficultyMax; value++)
            {
                bool on = _noteDifficulty == value;
                if (GUILayout.Toggle(on, FeelNotes.DifficultyLabel(value), GUI.skin.button) != on)
                    _noteDifficulty = on ? (int?)null : value;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("재미", _small);
            GUILayout.BeginHorizontal();
            for (int value = FeelNote.FunMin; value <= FeelNote.FunMax; value++)
            {
                bool on = _noteFun == value;
                if (GUILayout.Toggle(on, value.ToString(), GUI.skin.button) != on)
                    _noteFun = on ? (int?)null : value;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("태그", _small);
            string group = null;
            int inRow = 0;
            foreach ((string tagGroup, string id, string label) in FeelNotes.Tags)
            {
                if (tagGroup != group || inRow == 3)
                {
                    if (group != null)
                        GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    group = tagGroup;
                    inRow = 0;
                }

                bool on = _noteTags.Contains(id);
                if (GUILayout.Toggle(on, label, GUI.skin.button) != on)
                {
                    if (on)
                        _noteTags.Remove(id);
                    else
                        _noteTags.Add(id);
                }

                inRow++;
            }
            if (group != null)
                GUILayout.EndHorizontal();

            GUILayout.Label("느낌", _small);
            _noteText = GUILayout.TextArea(_noteText, GUILayout.MinHeight(56));
            GUILayout.Label("고칠 방향 (어떻게 고치면 좋겠나)", _small);
            _noteIntent = GUILayout.TextArea(_noteIntent, GUILayout.MinHeight(36));

            GUI.enabled = HasNoteInput;
            if (GUILayout.Button("저장"))
                SaveNote();
            GUI.enabled = true;

            GUILayout.Label($"이번 실행에 {_savedNotes}개 저장 · {PlaytestNotes.NotesFolder}", _small);
#if UNITY_EDITOR
            if (GUILayout.Button("폴더 열기"))
                UnityEditor.EditorUtility.RevealInFinder(PlaytestNotes.NotesFolder);
#endif
        }

        private void LockedField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(64));
            GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        // 메모가 붙는 세팅 이름(NewNote와 같은 규칙): 지금 판이 시작한 세팅, 판이 없으면 마지막 시나리오(없으면 "지금 진행").
        private string NoteSetupName() =>
            _battleSetup != null ? _battleSetup.Name
            : _progress != null ? (_lastScenario != null ? _lastScenario.name : "지금 진행")
            : null;

        private void RefreshNoteGroup(string setup, string profile)
        {
            string key = setup + "\n" + profile;
            if (_nextNoteName != null && key == _noteGroupFor && Time.unscaledTime < _noteGroupAt)
                return;

            _noteGroupFor = key;
            _noteGroupAt = Time.unscaledTime + NoteGroupRefreshSeconds;
            _nextNoteName = PlaytestNotes.NextName(setup, profile);
            _noteGroupCount = 0;
            _noteGroupRecent.Clear();

            IReadOnlyList<FeelNoteView> all = PlaytestNotes.ReadAll(out _);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (!FeelNotes.SameGroup(all[i], setup, profile))
                    continue;

                _noteGroupCount++;
                if (_noteGroupRecent.Count < MaxRecentNotes)
                    _noteGroupRecent.Add(RecentLine(all[i]));
            }
        }

        private static string RecentLine(FeelNoteView note)
        {
            string text = note.Text.Length > 0 ? note.Text : note.Intent;
            if (text.Length > 40)
                text = text.Substring(0, 40) + "…";
            return $"{note.Name ?? "(이름 없음)"} · {text}";
        }

        private bool HasNoteInput =>
            _noteDifficulty.HasValue || _noteFun.HasValue || _noteTags.Count > 0
            || _noteText.Trim().Length > 0 || _noteIntent.Trim().Length > 0;

        private void SaveNote()
        {
            FeelNote note = NewNote("panel");
            note.Difficulty = _noteDifficulty;
            note.Fun = _noteFun;
            note.Tags.AddRange(_noteTags);
            note.Text = _noteText.Trim();
            note.Intent = _noteIntent.Trim();

            if (!PlaytestNotes.TrySave(note, out string error))
            {
                _status = $"메모를 저장하지 못했다: {error}";
                return;
            }

            _savedNotes++;
            _nextNoteName = null;
            _noteDifficulty = null;
            _noteFun = null;
            _noteTags.Clear();
            _noteText = "";
            _noteIntent = "";
            _status = $"메모 {note.Name}를 저장했다.";
        }

        // 판이 바뀌는 순간 그 판이 시작한 세팅을 떠 둔다.
        private void TrackBattleSetup()
        {
            GameSession session = _battle.Session;

            if (session == _trackedSession)
                return;

            _trackedSession = session;

            if (session == null)
                return;

            PlaytestScenario scenario = _scenarioForNextBattle;
            _scenarioForNextBattle = null;
            Hq hq = session.World.Hq;

            _battleSetup = new BattleSetup
            {
                Name = scenario != null ? scenario.name : "지금 진행",
                GrowthStage = hq.Stage,
                // 시나리오 판은 시작 Level까지 EXP를 채워 시작했다(단계 시작보다 낮으면 단계 시작).
                StartLevel = scenario != null ? Math.Max(scenario.startLevel, hq.StartLevel) : hq.StartLevel,
                Seed = _battle.LastSeed,
                Gold = _progress != null ? _progress.Gold : 0,
            };

            if (_progress != null)
            {
                foreach (string nodeId in _progress.OwnedNodes)
                    _battleSetup.Nodes.Add((nodeId, _progress.RankOf(nodeId)));
            }

            // 판이 끝나면 판 객체가 사라지므로, 판 밖(결산·업그레이드 화면)에서 적는 메모를 위해 시작할 때 읽어 둔다.
            _battleSetup.Stats = NoteStats.Capture(session, _content, _battle.Upgrades, NoteStats.AtBattleStart);
        }

        // 게임 시작(Initialize) 뒤인가. 플레이 메모 창이 플레이 직후 아직 준비 안 된 패널을 건드리지 않게 본다.
        internal bool Ready => _session != null;

        // 지금 판(없으면 지금 진행)의 세팅·수치·판 상태를 담은 메모. 느낌 칸은 부르는 쪽이 채운다.
        // 플레이 메모 창도 플레이 중에는 이것으로 메모를 만든다(실제로 플레이한 상태가 남는다).
        internal FeelNote NewNote(string source)
        {
            TrackBattleSetup();

            var note = new FeelNote
            {
                Source = source,
                Profile = _session.AppliedProfile != null ? _session.AppliedProfile.name : string.Empty,
                Fingerprint = _session.Fingerprint,
                BaseFingerprint = _session.BaseFingerprint,
                Cheated = _session.Tag.IsTest,
            };

            if (_battleSetup != null)
            {
                note.SetupName = _battleSetup.Name;
                note.GrowthStage = _battleSetup.GrowthStage;
                note.StartLevel = _battleSetup.StartLevel;
                note.Seed = _battleSetup.Seed;
                note.Gold = _battleSetup.Gold;
                note.Nodes.AddRange(_battleSetup.Nodes);
            }
            else if (_progress != null)
            {
                note.SetupName = _lastScenario != null ? _lastScenario.name : "지금 진행";
                note.GrowthStage = _progress.GrowthStage;
                note.StartLevel = _content.Growth.StartLevelAt(Math.Min(_progress.GrowthStage, _content.Growth.MaxStage));
                note.Gold = _progress.Gold;

                foreach (string nodeId in _progress.OwnedNodes)
                    note.Nodes.Add((nodeId, _progress.RankOf(nodeId)));
            }

            GameSession session = _battle.Session;

            if (session != null)
            {
                note.Battle = _hud.Capture(session);
                note.BattleId = _analytics.CurrentBattleId;
                note.Stats = NoteStats.Capture(session, _content, _battle.Upgrades, NoteStats.AtNow);
            }
            else if (_battleSetup != null)
            {
                note.Stats = _battleSetup.Stats;
            }

            note.DescribeNodes(_nodeTree);
            return note;
        }

        // ── 모양 ──────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_stylesReady)
                return;

            Font font = _session.Font;
            _label = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
            _small = new GUIStyle(_label) { fontSize = 11 };
            _title = new GUIStyle(_label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _error = new GUIStyle(_small);
            _error.normal.textColor = new Color(1f, 0.6f, 0.55f);
            _hudStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                wordWrap = true,
                padding = new RectOffset(6, 6, 4, 4),
            };
            _hudStyle.normal.textColor = Color.white;

            if (font != null)
            {
                _label.font = font;
                _small.font = font;
                _title.font = font;
                _error.font = font;
                _hudStyle.font = font;
                GUI.skin.font = font;
            }

            _stylesReady = true;
        }
    }
}
#endif
