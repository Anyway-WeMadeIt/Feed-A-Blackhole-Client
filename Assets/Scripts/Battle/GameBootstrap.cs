using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private GameContentSetup _content;

        [Header("Looks")]
        [SerializeField] private BreakerLook _breakerLook;
        [SerializeField] private ExplosionLook _explosionLook;
        [SerializeField] private LightningLook _lightningLook;
        [SerializeField] private CometLook _cometLook;
        [SerializeField] private GameObject _blackHole;

        [Header("UI Layers")]
        [SerializeField] private RectTransform _rootLayer;
        [SerializeField] private RectTransform _panelLayer;

        [Header("Screen Transition")]
        [SerializeField] private ScreenTransitionLook _screenTransitionLook;

        [Header("Registered Views")]
        [SerializeField] private UIBase[] _views;

        [Header("Presentations")]
        [SerializeField] private ScreenPresentations _presentations;

        [Header("Analytics")]
        [SerializeField] private AnalyticsSettings _analyticsSettings;

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

        private const UITheme Theme = UITheme.Light;
        private const UILocale Locale = UILocale.Korean;

        private ProgressState _progress;
        private ProgressStore _progressStore;
        private GameSettings _settings;
        private BattleAnalytics _analytics;
        private GameHost _host;

        private void Awake()
        {
            // 판의 콘텐츠 표시(밸런스 프로필 이름, 테스트 표시). 릴리스는 늘 비어 있다.
            ContentTag contentTag = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlaytestSession playtest = PlaytestSession.Create(_playtest, contentTag);
            LoadedContent loaded = LoadContent(playtest);
            // 프로필을 적용했으면 그 프로필의 저장을 따로 쓴다. 테스트 값으로 번 진행이 실제 저장을 덮지 않는다.
            string saveDirectory = playtest.SaveDirectory(Application.persistentDataPath);
#else
            LoadedContent loaded = GameContentLoader.Load(_content);
            string saveDirectory = Application.persistentDataPath;
#endif

            if (loaded == null || !HasConfiguredLooks() || !HasConfiguredUI())
            {
                enabled = false;
                return;
            }

            Camera battleCamera = Camera.main;
            CameraShake cameraShake = battleCamera.GetComponent<CameraShake>();
            BattleCameraFit cameraFit = GetOrAdd<BattleCameraFit>(battleCamera.gameObject);

            EnemyLooks enemyLooks = new(_content.Enemies.Kinds());
            EnemyView enemyView = new(transform, enemyLooks, _breakerLook, _cometLook);
            BreakerView breakerView = new(transform, _breakerLook);
            DeathEffectView deathEffectView = new(transform, _lightningLook, _explosionLook);
            HqView hqView = new(transform, _blackHole, battleCamera);

            _progress = new ProgressState();
            _progressStore = ProgressStore.Load(
                saveDirectory,
                loaded.NodeTree.Content,
                loaded.Content.Growth);
            _settings = GameSettings.Load();
            _analytics = BattleAnalytics.Create(
                _analyticsSettings,
                Application.persistentDataPath,
                Application.version,
                Application.platform.ToString(),
                contentTag);

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
                loaded.Content.Growth,
                _settings,
                transition,
                keyInput,
                _analytics);

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
                cameraShake);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlaytestPanel panel = GetOrAdd<PlaytestPanel>(gameObject);
            panel.Initialize(playtest, battle, screens, loaded.Content, loaded.NodeTree, _progress, _analytics);
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

        private void Start()
        {
            _host.Start();
            SoundManager.Instance.Bind(_settings);
            // 지난 실행에서 보내지 못한 통계를 보낸다.
            _analytics.Flush();
        }

        private void Update() => _host.Tick(Time.deltaTime);

        private void OnApplicationPause(bool paused)
        {
            if (!enabled)
                return;

            if (paused)
            {
                _progressStore.Save(_progress);
                return;
            }

            // 앱으로 돌아왔다. 내려가 있는 동안 보내지 못한 통계를 보낸다.
            _analytics.Flush();
        }

        private void OnDestroy() => _host?.Dispose();

        private UIManager CreateUI()
        {
            UIContext uiContext = new(Theme.ToId(), Locale.ToId());
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

            return configured;
        }
    }
}
