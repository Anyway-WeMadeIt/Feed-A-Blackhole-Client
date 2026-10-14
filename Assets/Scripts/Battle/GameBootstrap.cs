using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using BlackHole.Net;
using BlackHole.Net.Unity;
using BlackHole.Sync;
using UnityEngine;

namespace BlackHole.Unity
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private GameContentSetup _content;

        // 서버 설정(Assets/Data/ServerSettings.asset). 비우거나 꺼 두면 로그인하지 않고 오프라인으로 실행한다.
        [Header("Server")]
        [SerializeField] private ServerSettings _server;

        [Header("Looks")]
        [SerializeField] private BreakerLook _breakerLook;
        [SerializeField] private ExplosionLook _explosionLook;
        [SerializeField] private LightningLook _lightningLook;
        [SerializeField] private CometLook _cometLook;
        [SerializeField] private GameObject _blackHole;

        // 전투 배경의 밝기 테마(설정 Dark Mode). 메뉴 Tools/Black Hole/Battle Background/Install in Open Scene이 연결한다. 비우면 씬에 놓인 그대로.
        [Header("Battle Background")]
        [SerializeField] private BattleBackgroundTheme _battleBackground;

        [Header("UI Layers")]
        [SerializeField] private RectTransform _rootLayer;
        [SerializeField] private RectTransform _panelLayer;

        [Header("Screen Transition")]
        [SerializeField] private ScreenTransitionLook _screenTransitionLook;

        [Header("Registered Views")]
        [SerializeField] private UIBase[] _views;

        [Header("Presentations")]
        [SerializeField] private ScreenPresentations _presentations;

        // 언어·역할별 글꼴(Assets/UI/Fonts/UIFontSet.asset). 화면 Theme들이 가리키는 것과 같은 에셋을 연결한다.
        // Theme는 TextBindings 글자에, 이 칸은 TextBindings가 닿지 않는 글자(복제·코드 글자)에 같은 글꼴을 넣는다.
        [Header("Fonts")]
        [SerializeField] private UIFontSet _fonts;

        // 테마별 색(Assets/UI/Palette/UIPalette.asset)과 밝은·어두운 그림 짝(UISpriteSet). 화면 Theme들이 가리키는 팔레트와 같은 에셋을 연결한다.
        // Theme는 TextBindings·ImageBindings에, 이 칸은 그 밖의 글자·그림(설정 행, 카드, 노드 트리 등)에 같은 테마를 넣는다.
        [Header("Theme")]
        [SerializeField] private UIPalette _palette;
        [SerializeField] private UISpriteSet _sprites;

        // UI 글자 표(Assets/Data/Localization/UIText.csv). 형식과 키 규칙은 Localizer에 적었다.
        [Header("Localization")]
        [SerializeField] private TextAsset _texts;

        // 테스트 도구(개발 패널)의 밸런스 프로필·시나리오 목록. 에디터와 개발 빌드만 쓴다. 비워 두면 기기 폴더의 파일만 읽는다.
        // 장면 직렬화가 빌드마다 같도록 #if로 감싸지 않는다. 릴리스 빌드는 읽지 않는다(CS0169).
        [Header("Playtest")]
#pragma warning disable CS0169
        [SerializeField] private PlaytestLibrary _playtest;
#pragma warning restore CS0169

        private static readonly Type[] RequiredViews =
        {
            typeof(TitleScreen),
            typeof(UpgradeScreen),
            typeof(BattleScreen),
            typeof(SettlementScreen),
            typeof(NodeTreeView),
            typeof(ModeSelectPanel),
            typeof(SettingsPanel),
            typeof(PausePanel),
            typeof(ConfirmPanel),
        };

        private ProgressState _progress;
        private ProgressStore _progressStore;
        private GameSettings _settings;
        private ViewTexts _viewTexts;
        private UISettingsSync _uiSettings;
        private FrameRateLimiter _frameRate;
        private GameHost _host;
        private IAuthSession _auth;
        // 로그인이 끝나기 전에는 화면이 없으므로 호스트를 돌리지 않는다.
        private bool _loggingIn;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 판의 콘텐츠 표시(밸런스 프로필 이름, 테스트 표시)는 개발 HUD·메모가 보여 준다.
            PlaytestSession playtest = PlaytestSession.Create(_playtest, new ContentTag());
            LoadedContent loaded = LoadContent(playtest);
            // 프로필을 적용했으면 그 프로필의 저장을 따로 쓴다. 테스트 값으로 번 진행이 실제 저장을 덮지 않는다.
            string saveDirectory = playtest.SaveDirectory(SaveLocation.Root(Application.persistentDataPath));
            // 밸런스 프로필을 적용한 실행은 콘텐츠가 서버와 달라 로그인하지 않는다(계약 C10).
            bool profileApplied = playtest.AppliedProfile != null;
#else
            LoadedContent loaded = GameContentLoader.Load(_content);
            // 웹은 빌드를 올려도 바뀌지 않는 고정 폴더를 쓴다(SaveLocation).
            string saveDirectory = SaveLocation.Root(Application.persistentDataPath);
            const bool profileApplied = false;
#endif

            if (loaded != null)
                Debug.Log($"[콘텐츠] 서버 콘텐츠 버전: {loaded.ServerContentVersion}");

            if (loaded == null || !HasConfiguredLooks() || !HasConfiguredUI())
            {
                enabled = false;
                return;
            }

            // 설정은 화면(적 연출·화면 흔들림)이 읽으므로 화면보다 먼저 읽는다.
            _settings = GameSettings.Load();

            // 전투 배경은 설정의 Dark Mode를 따른다. 설정을 읽은 직후에 묶어 첫 프레임부터 맞는 배경이 보인다.
            if (_battleBackground != null)
                _battleBackground.Bind(_settings);
            _frameRate = new FrameRateLimiter(_settings);

            Camera battleCamera = Camera.main;
            CameraShake cameraShake = battleCamera.GetComponent<CameraShake>();
            BattleCameraFit cameraFit = GetOrAdd<BattleCameraFit>(battleCamera.gameObject);

            EnemyLooks enemyLooks = new(_content.Enemies.Kinds());
            EnemyView enemyView = new(transform, enemyLooks, _breakerLook, _cometLook, _settings);
            BreakerView breakerView = new(transform, _breakerLook);
            DeathEffectView deathEffectView = new(transform, _lightningLook, _explosionLook);
            HqView hqView = new(transform, _blackHole, battleCamera, _settings);

            _progress = new ProgressState();
            _progressStore = ProgressStore.Load(
                saveDirectory,
                loaded.NodeTree.Content,
                loaded.Content.Growth,
                _progress);
            _auth = CreateAuthSession(saveDirectory, profileApplied);
            // 서버 동기화 로그를 만든다. 로그는 빌드 버전과 이 콘텐츠의 서버 콘텐츠 버전을 싣는다.
            SyncLogFactory syncLogs = new(new UnityJsonSerializer(), Application.version, loaded.ServerContentVersion, () => DateTime.UtcNow);
            Localizer localizer = Localizer.Create(_texts, _settings);

            BattleSystem battle = new(
                loaded.Content,
                _progress,
                enemyView,
                breakerView,
                deathEffectView,
                hqView,
                cameraFit);
            AimInput aim = new(battle);

            UIManager ui = CreateUI();
            // 화면을 등록한 뒤에 프리팹 글자를 지금 언어로 바꾼다. 언어가 바뀌면 다시 바꾼다.
            _viewTexts = new ViewTexts(_views, localizer);
            // 언어·글자 크기 설정을 UIContext와 화면 글자(크기·글꼴)에 넣는다. 설정이 바뀌면 다시 넣는다.
            _uiSettings = new UISettingsSync(ui, _views, _settings, _fonts, _palette, _sprites);
            ScreenTransition transition = CreateScreenTransition();
            KeyInput keyInput = GetOrAdd<KeyInput>(gameObject);

            ScreenFlow screens = new(
                ui,
                _presentations,
                battle,
                _progress,
                _progressStore,
                loaded.NodeTree,
                loaded.NodeItems,
                loaded.NodeRegions,
                loaded.Content.Growth,
                _settings,
                localizer,
                transition,
                keyInput,
                syncLogs);

            _host = new GameHost(
                ui,
                battle,
                aim,
                screens,
                enemyLooks,
                enemyView,
                breakerView,
                deathEffectView,
                hqView,
                cameraShake,
                _settings);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlaytestPanel panel = GetOrAdd<PlaytestPanel>(gameObject);
            panel.Initialize(playtest, battle, screens, loaded.Content, loaded.NodeTree, _progress);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 고른 밸런스 프로필을 적용해 불러온다. 패치한 값이 콘텐츠 검사를 통과하지 못하면 원본 값으로 다시 불러온다.
        private LoadedContent LoadContent(PlaytestSession playtest)
        {
            LoadedContent loaded = GameContentLoader.Load(_content, playtest.Patch);

            if (loaded != null || playtest.AppliedProfile == null)
                return loaded;

            playtest.RejectProfile();
            Debug.LogWarning("[테스트] 밸런스 프로필을 적용한 콘텐츠가 검사를 통과하지 못해 원본 값으로 다시 불러온다. 위의 [콘텐츠] 오류를 본다.", this);
            return GameContentLoader.Load(_content);
        }
#endif

        // 콘텐츠를 읽은 뒤 타이틀로 가기 전에 로그인을 마친다(W2-3). 대기 중에는 아무것도 띄우지 않는다.
        // StartAsync는 예외를 던지지 않고 loginBudgetSeconds 안에 끝난다.
        private IEnumerator Start()
        {
            _loggingIn = true;
            Task login = _auth.StartAsync();

            while (!login.IsCompleted)
                yield return null;

            _loggingIn = false;
            Debug.Log(_auth.State == ConnectionState.Online
                ? "[인증] 온라인"
                : $"[인증] 오프라인: {_auth.OfflineReason}");

            _host.Start();
            SoundManager.Instance.Bind(_settings);
        }

        private void Update()
        {
            if (!_loggingIn)
                _host.Tick(Time.deltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!enabled)
                return;

            if (paused)
                _progressStore.Save(_progress);
        }

        private void OnDestroy()
        {
            _frameRate?.Dispose();
            _viewTexts?.Dispose();
            _uiSettings?.Dispose();
            _host?.Dispose();
        }

        // 설정이 없거나 꺼져 있거나 프로필을 적용했으면 요청 없이 오프라인(Disabled)이다. 계정은 진행 저장과 같은 폴더에 둔다(계약 2.5).
        // 웹 빌드도 오프라인이다: itch.io 페이지(HTTPS)에서 http 서버 요청은 브라우저가 막고, 다른 도메인이라 서버의 CORS 허용도 필요하다.
        // 붙으려 하면 실패할 로그인을 기다리느라 첫 화면이 늦게 뜬다.
        private IAuthSession CreateAuthSession(string saveDirectory, bool profileApplied)
        {
            if (_server == null || !_server.Enabled || profileApplied || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return new OfflineAuthSession(OfflineReason.Disabled);
            }

            return new AuthSession(
                new UnityHttpTransport(_server.BaseUrl),
                new UnityJsonSerializer(),
                new FileCredentialStore(saveDirectory),
                _server.RequestTimeoutSeconds,
                _server.LoginBudgetSeconds,
                Debug.Log,
                Debug.LogError);
        }

        private UIManager CreateUI()
        {
            // 시작할 때의 테마·언어·글자 배율. 실행 중에 바뀌면 UISettingsSync가 새 UIContext로 바꿔 끼운다.
            UIContext uiContext = UISettingsSync.ContextFor(_settings);
            UIResolver uiResolver = new(uiContext);
            UIPresentationApplier presentationApplier = new();

            UIManager ui = new(
                _rootLayer,
                _panelLayer,
                uiResolver,
                presentationApplier);

            foreach (UIBase view in _views)
            {
                if (view == null)
                    continue;

                view.gameObject.SetActive(false);
                ui.Register(view);
            }

            UIDisplayRefreshDriver displayRefresh = GetOrAdd<UIDisplayRefreshDriver>(gameObject);
            displayRefresh.Initialize(ui);

            return ui;
        }

        private ScreenTransition CreateScreenTransition()
        {
            Canvas canvas = _rootLayer.GetComponentInParent<Canvas>();
            Transform transitionParent = canvas != null
                ? canvas.rootCanvas.transform
                : _rootLayer.parent;

            return ScreenTransition.Create(transitionParent, _screenTransitionLook);
        }

        // 에디터의 GetComponent는 없을 때 가짜 null을 돌려줘 ??로는 거를 수 없다. TryGetComponent로 본다.
        private static T GetOrAdd<T>(GameObject owner) where T : Component =>
            owner.TryGetComponent(out T component) ? component : owner.AddComponent<T>();

        private bool HasConfiguredLooks()
        {
            bool configured = true;

            if (_breakerLook == null
                || _breakerLook.Material == null
                || _breakerLook.OrbMaterial == null
                || _breakerLook.CometAuraMaterial == null)
            {
                Debug.LogError(
                    "[외형] GameBootstrap에 Breaker 외형(BreakerLook)을," +
                    " Breaker 외형에 링·버프 구체·혜성 배경 원 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_explosionLook == null
                || _explosionLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 폭발 외형(ExplosionLook)을," +
                               " 폭발 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_cometLook == null || _cometLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 혜성 외형(CometLook)을, " +
                               "혜성 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_lightningLook == null || _lightningLook.Material == null)
            {
                Debug.LogError("[외형] GameBootstrap에 번개 외형(LightningLook)을," +
                               " 번개 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            return configured;
        }

        // Theme가 다른 글꼴 묶음을 가리키면 같은 화면 안에서 TextBindings 글자와 나머지 글자의 글꼴이 갈린다.
        private void WarnOtherFontSets()
        {
            var others = new List<string>();

            foreach (ThemeSpec theme in _presentations.Themes())
            {
                if (theme != null && (theme.fonts != _fonts || theme.palette != _palette) && !others.Contains(theme.name))
                    others.Add(theme.name);
            }

            if (others.Count > 0)
            {
                Debug.LogWarning(
                    $"[UI] 이 Theme들은 GameBootstrap과 다른 글꼴 묶음 또는 팔레트를 가리킨다: {string.Join(", ", others)}." +
                    " 같은 UIFontSet·UIPalette를 연결한다.", this);
            }
        }

        private bool HasConfiguredUI()
        {
            bool configured = true;

            if (_screenTransitionLook == null
                || _screenTransitionLook.Material == null)
            {
                Debug.LogError("[UI] GameBootstrap에 화면 전환 외형(ScreenTransitionLook)을," +
                               " 화면 전환 외형에 머티리얼을 연결해야 한다.", this);
                configured = false;
            }

            if (_rootLayer == null
                || _panelLayer == null)
            {
                Debug.LogError("[UI] GameBootstrap에 Root Layer와 Panel Layer를 연결해야 한다.", this);
                configured = false;
            }

            var missingViews = new List<string>();

            foreach (Type type in RequiredViews)
            {
                if (_views == null
                    || !Array.Exists(_views, view => view != null && view.GetType() == type))
                    missingViews.Add(type.Name);
            }

            if (missingViews.Count > 0)
            {
                Debug.LogError(
                    $"[UI] GameBootstrap의 Registered Views에 빠진 화면이 있다: {string.Join(", ", missingViews)}.", this);
                configured = false;
            }

            if (_presentations == null)
            {
                Debug.LogError(
                    "[UI] GameBootstrap에 화면 Presentation 묶음(ScreenPresentations)을 연결해야 한다.", this);
                configured = false;
            }
            else
            {
                IReadOnlyList<string> missing = _presentations.MissingReferences();

                if (missing.Count > 0)
                {
                    Debug.LogError(
                        $"[UI] 화면 Presentation 묶음에 연결하지 않은 칸이 있다: {string.Join(", ", missing)}.", _presentations);
                    configured = false;
                }
            }

            if (_fonts == null)
            {
                Debug.LogError("[UI] GameBootstrap에 글꼴 묶음(UIFontSet)을 연결해야 한다.", this);
                configured = false;
            }

            if (_palette == null || _sprites == null)
            {
                Debug.LogError("[UI] GameBootstrap에 팔레트(UIPalette)와 그림 짝(UISpriteSet)을 연결해야 한다.", this);
                configured = false;
            }
            else if (_presentations != null)
            {
                WarnOtherFontSets();
            }

            return configured;
        }
    }
}
