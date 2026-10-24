using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using BlackHole.Net;
using BlackHole.Net.Unity;
using BlackHole.Sync;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BlackHole.Unity
{
    public sealed partial class GameBootstrap : MonoBehaviour
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
        [SerializeField] private StarLightningLook _starLightningLook;
        [SerializeField] private LaserLook _laserLook;
        [SerializeField] private CometLook _cometLook;
        [SerializeField] private GameObject _blackHole;

        // 소리를 내는 씬 객체(SoundPlayer 프리팹). 비우면 소리 없이 실행한다.
        [Header("Sound")]
        [SerializeField] private SoundPlayer _sound;

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

        //파동
        [Header("BlackHole")]
        [SerializeField] private ScriptableRendererData _rendererData;

        // 테스트 도구(개발 패널)의 밸런스 프로필·시나리오 목록. 에디터와 개발 빌드만 쓴다. 비워 두면 기기 폴더의 파일만 읽는다.
        // 장면 직렬화가 빌드마다 같도록 #if로 감싸지 않는다. 릴리스 빌드는 읽지 않는다(CS0169).
        [Header("Playtest")]
#pragma warning disable CS0169
        [SerializeField] private PlaytestLibrary _playtest;
#pragma warning restore CS0169

        private ProgressState _progress;
        private ProgressStore _progressStore;
        private GameSettings _settings;
        private ViewTexts _viewTexts;
        private UISettingsSync _uiSettings;
        private FrameRateLimiter _frameRate;
        private GameHost _host;
        private IAuthSession _auth;
        // 서버를 쓰지 않는 실행(Offline(Disabled))이면 null이다.
        private SyncSender _syncSender;
        private TutorialHintView _tutorialHint;
        // 로그인·시작 동기화가 끝나기 전에는 화면이 없으므로 호스트를 돌리지 않는다.
        private bool _loggingIn;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 판의 콘텐츠 표시(밸런스 프로필 이름, 테스트 표시)는 개발 HUD·메모가 보여 준다.
            PlaytestSession playtest = PlaytestSession.Create(_playtest, new ContentTag());
            LoadedContent loaded = LoadContent(playtest);
            // 프로필을 적용했으면 그 프로필의 저장을 따로 쓴다. 테스트 값으로 번 진행이 실제 저장을 덮지 않는다.
            string saveDirectory = playtest.SaveDirectory(Application.persistentDataPath);
            // 밸런스 프로필을 적용한 실행은 콘텐츠가 서버와 달라 로그인하지 않는다(계약 C10).
            bool profileApplied = playtest.AppliedProfile != null;
#else
            LoadedContent loaded = GameContentLoader.Load(_content);
            string saveDirectory = Application.persistentDataPath;
            const bool profileApplied = false;
#endif

            if (loaded != null)
                Debug.Log($"[콘텐츠] 서버 콘텐츠 버전: {loaded.ServerContentVersion}");

            // 연결이 빠졌으면 돌리지 않는다. 빠진 칸은 에디터에서 고칠 때 바로 보인다(OnValidate, GameBootstrap.Validation).
            List<string> missing = MissingLinks();

            if (loaded == null || missing.Count > 0)
            {
                if (missing.Count > 0)
                    Debug.LogError("[조립] GameBootstrap에 연결하지 않은 것이 있다:\n- " + string.Join("\n- ", missing), this);

                enabled = false;
                return;
            }

            // 설정은 화면(적 연출·화면 흔들림)이 읽으므로 화면보다 먼저 읽는다.
            _settings = GameSettings.Load();

            // 전투 배경은 설정의 Dark Mode를 따른다. 설정을 읽은 직후에 묶어 첫 프레임부터 맞는 배경이 보인다.
            if (_battleBackground != null)
                _battleBackground.Bind(_settings);
            _frameRate = new FrameRateLimiter(_settings);

            if (_sound == null)
                Debug.LogWarning("[소리] GameBootstrap에 SoundPlayer를 연결하지 않아 소리 없이 실행한다.", this);

            BattleSounds battleSounds = new(_sound);
            UISounds uiSounds = new(_sound);

            Camera battleCamera = Camera.main;
            CameraShake cameraShake = battleCamera.GetComponent<CameraShake>();
            BattleCameraFit cameraFit = GetOrAdd<BattleCameraFit>(battleCamera.gameObject);

            EnemyLooks enemyLooks = new(_content.Enemies.Kinds());
            EnemyView enemyView = new(transform, enemyLooks, _breakerLook, _cometLook, _settings, battleSounds);
            BreakerView breakerView = new(transform, _breakerLook, battleSounds);
            DeathEffectView deathEffectView = new(transform, _lightningLook, _starLightningLook, _explosionLook, _laserLook, battleSounds);
            HqView hqView = new(transform, _blackHole, battleCamera, _settings, FindShockwaveFeature());

            _progress = new ProgressState();
            _progressStore = ProgressStore.Load(
                saveDirectory,
                loaded.NodeTree.Content,
                loaded.Content.Growth,
                _progress);
            CreateServerSession(saveDirectory, profileApplied);
            // 서버 동기화 로그를 만든다. 로그는 빌드 버전과 이 콘텐츠의 서버 콘텐츠 버전을 싣는다.
            SyncLogFactory syncLogs = new(new UnityJsonSerializer(), Application.version, loaded.ServerContentVersion, () => DateTime.UtcNow);
            ProgressRecorder recorder = new(_progressStore, syncLogs);
            Localizer localizer = Localizer.Create(_texts, _settings);
            _tutorialHint = new TutorialHintView(transform, localizer);

            BattleSystem battle = new(
                loaded.Content,
                _progress,
                enemyView,
                breakerView,
                deathEffectView,
                hqView,
                _tutorialHint,
                cameraFit,
                battleSounds);
            AimInput aim = new(battle);

            UIManager ui = CreateUI();
            // UI 소리의 출처를 캔버스 루트에 둔다. 그 아래의 버튼 배선(ButtonFeedback)과 화면이 찾아 쓴다.
            GetOrAdd<UISoundSource>(CanvasRoot().gameObject).Sounds = uiSounds;
            // 화면을 등록한 뒤에 프리팹 글자를 지금 언어로 바꾼다. 언어가 바뀌면 다시 바꾼다.
            _viewTexts = new ViewTexts(_views, localizer);
            // 다크 모드·언어·글자 크기 설정을 UIContext에 넣고, Gold 표기 설정을 숫자 화면에 넣는다. 설정이 바뀌면 다시 넣는다.
            _uiSettings = new UISettingsSync(ui, _views, _settings);
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
                recorder,
                uiSounds,
                _auth);

            // 거부를 받으면 알림을 띄운다(W3-5). 화면이 아직 없는 시작 동기화 중이면 타이틀에 들어갈 때 보인다. 전송기는 Dispose에서 구독을 푼다.
            if (_syncSender != null)
                _syncSender.RejectionReceived += _ => screens.HandleSyncRejection();

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
                _settings,
                battleSounds);

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

        // 콘텐츠를 읽은 뒤 타이틀로 가기 전에 로그인과 시작 동기화를 마친다(W2-3, W3-4, 계약 2.8). 대기 중에는 아무것도 띄우지 않는다.
        // StartAsync는 예외를 던지지 않고 loginBudgetSeconds 안에, 시작 동기화는 syncTimeoutSeconds 안에 끝난다.
        private IEnumerator Start()
        {
            _loggingIn = true;
            Task login = _auth.StartAsync();

            while (!login.IsCompleted)
                yield return null;

            if (login.IsFaulted)
                Debug.LogError($"[인증] 로그인 처리 중 예외: {login.Exception}");

            Debug.Log(_auth.State == ConnectionState.Online
                ? "[인증] 온라인"
                : $"[인증] 오프라인: {_auth.OfflineReason}");

            // 오프라인이면 보내지 않고 바로 끝난다. 응답의 진행도가 반영된 뒤에 타이틀로 간다.
            if (_syncSender != null)
            {
                Task sync = _syncSender.SyncOnStartupAsync();

                while (!sync.IsCompleted)
                    yield return null;

                if (sync.IsFaulted)
                    Debug.LogError($"[동기화] 시작 동기화 중 예외: {sync.Exception}");
            }

            _loggingIn = false;

            _host.Start();

            if (_sound != null)
                _sound.Bind(_settings);
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
            _syncSender?.Dispose();
            _tutorialHint?.Dispose();
        }

        // 설정이 없거나 꺼져 있거나 프로필을 적용했으면 요청 없이 오프라인(Disabled)이고 전송기를 만들지 않는다.
        // 계정은 진행 저장과 같은 폴더에 둔다(계약 2.5). 전송기는 진행 저장소의 LogAppended를 보고 보낸다(계약 5.8).
        private void CreateServerSession(string saveDirectory, bool profileApplied)
        {
            if (_server == null || !_server.Enabled || profileApplied)
            {
                _auth = new OfflineAuthSession(OfflineReason.Disabled);
                return;
            }

            UnityHttpTransport transport = new(_server.BaseUrl);
            UnityJsonSerializer json = new();

            _auth = new AuthSession(
                transport,
                json,
                new FileCredentialStore(saveDirectory),
                _server.RequestTimeoutSeconds,
                _server.LoginBudgetSeconds,
                Debug.Log,
                Debug.LogError);

            SyncApi api = new(transport, _auth, json, _server.SyncTimeoutSeconds, Debug.Log, Debug.LogError);
            _syncSender = new SyncSender(_auth, api, _progressStore, _server.SyncTimeoutSeconds, Debug.Log, Debug.LogError);
        }

        private UIManager CreateUI()
        {
            // 시작할 때의 테마·언어·글자 배율. 실행 중에 바뀌면 UISettingsSync가 새 UIContext로 바꿔 끼운다.
            UIContext uiContext = UISettingsSync.ContextFor(_settings);
            UIResolver uiResolver = new(uiContext, new UIThemeAssets(_fonts, _palette, _sprites));
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

        private ScreenTransition CreateScreenTransition() =>
            ScreenTransition.Create(CanvasRoot(), _screenTransitionLook);

        // UI 캔버스의 루트. 캔버스를 못 찾으면 화면 층의 부모.
        private Transform CanvasRoot()
        {
            Canvas canvas = _rootLayer.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.transform : _rootLayer.parent;
        }

        // 에디터의 GetComponent는 없을 때 가짜 null을 돌려줘 ??로는 거를 수 없다. TryGetComponent로 본다.
        private static T GetOrAdd<T>(GameObject owner) where T : Component =>
            owner.TryGetComponent(out T component) ? component : owner.AddComponent<T>();

        private ScriptableRendererFeature FindShockwaveFeature()
        {
            if (_rendererData == null)
                return null;
            
            foreach (ScriptableRendererFeature feature in _rendererData.rendererFeatures)
            {
                if (feature is FullScreenPassRendererFeature)
                    return feature;
            }

            return null;
        }
    }
}
