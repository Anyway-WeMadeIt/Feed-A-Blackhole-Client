#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 상시 성능 HUD와 측정 기록(에디터·개발 빌드 전용). 장면을 불러오면 스스로 생긴다.
    // - 화면: 왼쪽 아래(안전 영역 안). 0.5초마다 갱신. 예산(런북 0단계)을 넘은 값은 빨간색.
    //   1줄 빌드 정보(커밋·브랜치·기기) / 2줄 FPS·프레임·메인·GPU·GC / 3~4줄(자세히) 렌더 스레드·드로우콜·적 수·메모리·목표 FPS.
    // - 조작: F3 또는 네 손가락 탭 = 간단 → 자세히 → 숨김. F4 또는 네 손가락 1.5초 누르기 = 손으로 기록 켜기·끄기(판 밖 측정, S1·S4용).
    // - 기록: 판이 시작되면 자동으로 CSV를 열고 판이 끝나면 닫는다(PerfLog). 숨겨도 기록은 계속된다.
    // - 값의 출처: 프레임 간격 = Time.unscaledDeltaTime, 메인·렌더·GPU = FrameTimingManager(개발 빌드에서 늘 켜짐, 4프레임 늦음),
    //   GC·드로우콜·메모리 = ProfilerRecorder. 읽지 못하는 값은 '-'(기기에 따라 GPU 시간이 없을 수 있다).
    // HUD 자체도 캔버스 하나(드로우콜 1~2개)를 쓰고, 글은 0.5초마다 할당 없이 다시 만든다.
    internal sealed class PerfHud : MonoBehaviour
    {
        private enum Mode { Compact, Full, Hidden }

        private const float RefreshSeconds = 0.5f;
        private const int ToggleFingers = 4;
        private const float HoldSeconds = 1.5f;
        private const int SortingOrder = 32000;
        private const float FontSize = 13f;

        // 런북 0단계 예산(제안값). 넘으면 빨간색.
        private const float MainBudgetMs = 11f;
        private const float SlowFrameMs = 20f;

        private const string Red = "<color=#FF6B6B>";
        private const string EndColor = "</color>";

        public static PerfHud Instance { get; private set; }

        // 이 프로젝트는 플레이에 들어갈 때 도메인을 다시 불러오지 않아 static이 남는다. 플레이마다 되돌린다.
        private static Mode _mode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            _mode = Mode.Compact;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (Instance != null)
                return;

            var host = new GameObject("Perf HUD");
            DontDestroyOnLoad(host);
            host.AddComponent<PerfHud>();
        }

        // 게임 코드가 PerfProbe를 거쳐 넣는다.
        public int AliveEnemies { get; set; }
        private int _levelUpsThisFrame;

        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private ProfilerRecorder _gcAlloc;
        private ProfilerRecorder _gcAllocCount;
        private ProfilerRecorder _gcUsed;
        private ProfilerRecorder _gcReserved;
        private ProfilerRecorder _totalUsed;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPass;

        private readonly PerfWindow _window = new();
        private readonly PerfLog _log = new();
        private readonly StringBuilder _sb = new(1024);
        private string _buildLine;
        private float _sinceRefresh;

        private RectTransform _safeArea;
        private Rect _appliedSafeArea;
        private TextMeshProUGUI _text;

        private bool _touchHeld;
        private bool _holdFired;
        private float _touchHeldFor;

        public void BeginBattle(int seed)
        {
            AliveEnemies = 0;
            _log.Begin("battle", seed);
        }

        public void EndBattle()
        {
            if (_log.IsRecording && _log.Label == "battle")
                _log.End(Read(_gcUsed), Read(_totalUsed));

            AliveEnemies = 0;
        }

        public void AddLevelUps(int levels) => _levelUpsThisFrame += levels;

        public void SetContext(string scenario, string profile, string aim)
        {
            _log.Scenario = scenario;
            _log.Profile = profile;
            _log.Aim = aim;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _buildLine = "FAB " + Application.version + " " + BuildStamp.Commit + " " + BuildStamp.Branch + " | " + SystemInfo.deviceModel;
            CreateView();
        }

        private void OnEnable()
        {
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcAllocCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            _totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        private void OnDisable()
        {
            _gcAlloc.Dispose();
            _gcAllocCount.Dispose();
            _gcUsed.Dispose();
            _gcReserved.Dispose();
            _totalUsed.Dispose();
            _drawCalls.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
        }

        private void OnDestroy()
        {
            _log.End();

            if (Instance == this)
                Instance = null;
        }

        // 백그라운드로 가면 써 둔 줄을 디스크에 밀어 넣는다(그대로 앱이 꺼져도 남게).
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                _log.Flush();
        }

        private void OnApplicationQuit() => _log.End();

        private void LateUpdate()
        {
            HandleInput();

            PerfSample sample = Capture();
            _window.Add(sample);
            _log.Add(sample, Read(_gcUsed), Read(_totalUsed));

            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= RefreshSeconds)
            {
                _sinceRefresh = 0;
                Refresh();
                _window.Reset();
            }
        }

        private PerfSample Capture()
        {
            var sample = new PerfSample
            {
                DeltaMs = Time.unscaledDeltaTime * 1000f,
                MainMs = float.NaN,
                RenderMs = float.NaN,
                GpuMs = float.NaN,
                GcBytes = Read(_gcAlloc),
                GcCount = Read(_gcAllocCount),
                DrawCalls = Read(_drawCalls),
                Batches = Read(_batches),
                SetPass = Read(_setPass),
                Alive = AliveEnemies,
                LevelUps = _levelUpsThisFrame,
            };

            _levelUpsThisFrame = 0;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                FrameTiming timing = _timings[0];
                float main = (float)(timing.cpuMainThreadFrameTime - timing.cpuMainThreadPresentWaitTime);
                float render = (float)timing.cpuRenderThreadFrameTime;
                float gpu = (float)timing.gpuFrameTime;

                if (main > 0)
                    sample.MainMs = main;
                if (render > 0)
                    sample.RenderMs = render;
                if (gpu > 0)
                    sample.GpuMs = gpu;
            }

            return sample;
        }

        private static long Read(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f3Key.wasPressedThisFrame)
                    CycleMode();
                if (keyboard.f4Key.wasPressedThisFrame)
                    ToggleManualRecording();
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return;

            int pressed = 0;
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed)
                    pressed++;
            }

            if (pressed >= ToggleFingers)
            {
                if (!_touchHeld)
                {
                    _touchHeld = true;
                    _holdFired = false;
                    _touchHeldFor = 0;
                }
                else
                {
                    _touchHeldFor += Time.unscaledDeltaTime;
                    if (!_holdFired && _touchHeldFor >= HoldSeconds)
                    {
                        _holdFired = true;
                        ToggleManualRecording();
                    }
                }
            }
            else if (_touchHeld)
            {
                _touchHeld = false;
                if (!_holdFired)
                    CycleMode();
            }
        }

        private void CycleMode()
        {
            _mode = _mode == Mode.Compact ? Mode.Full : _mode == Mode.Full ? Mode.Hidden : Mode.Compact;
            Refresh();
        }

        // 판 밖(대기 화면, 결산→트리 전환)을 재려고 손으로 켜고 끈다. 판이 시작되면 판 기록으로 넘어간다.
        private void ToggleManualRecording()
        {
            if (_log.IsRecording)
                _log.End(Read(_gcUsed), Read(_totalUsed));
            else
                _log.Begin("manual", -1);

            Refresh();
        }

        private void Refresh()
        {
            if (_text == null)
                return;

            bool visible = _mode != Mode.Hidden;
            if (_text.enabled != visible)
                _text.enabled = visible;

            if (!visible)
                return;

            ApplySafeArea();

            StringBuilder sb = _sb.Clear();
            PerfWindow w = _window;

            sb.Append("<mark=#000000B4>");
            sb.Append(_buildLine);
            if (_log.IsRecording)
            {
                sb.Append("  ").Append(Red).Append("REC ").Append(_log.Label);
                if (_log.Seed >= 0)
                    PerfText.Int(sb.Append(" s"), _log.Seed);
                sb.Append(' ');
                int seconds = (int)_log.ElapsedSeconds;
                PerfText.Int(sb, seconds / 60).Append(':');
                PerfText.Two(sb, seconds % 60).Append(EndColor);
            }

            sb.Append('\n');
            PerfText.Fixed(sb, w.Fps, 1).Append(" fps  frame ");
            PerfText.Fixed(sb, w.AvgDelta, 1).Append(" (max ");
            AppendOver(sb, w.MaxDelta, SlowFrameMs, 1).Append(")  main ");
            AppendOver(sb, w.AvgMain, MainBudgetMs, 1).Append("  gpu ");
            PerfText.Fixed(sb, w.AvgGpu, 1).Append("  gc ");
            AppendOver(sb, w.GcKb, 0, 1).Append("KB");

            if (_mode == Mode.Full)
            {
                sb.Append("\nrender ");
                PerfText.Fixed(sb, w.AvgRender, 1).Append("  draw ");
                PerfText.Fixed(sb, w.AvgDraw, 0).Append("  batch ");
                PerfText.Fixed(sb, w.AvgBatches, 0).Append("  setpass ");
                PerfText.Fixed(sb, w.AvgSetPass, 0).Append("  enemies ");
                PerfText.Int(sb, w.LastAlive).Append("  lvup ");
                PerfText.Int(sb, w.LevelUps);

                sb.Append("\nheap ");
                AppendMb(sb, Read(_gcUsed)).Append('/');
                AppendMb(sb, Read(_gcReserved)).Append("MB  total ");
                AppendMb(sb, Read(_totalUsed)).Append("MB  target ");
                PerfText.Int(sb, Application.targetFrameRate).Append(" @");
                PerfText.Fixed(sb, Screen.currentResolution.refreshRateRatio.value, 0).Append("Hz");
            }

            sb.Append("</mark>");
            _text.SetText(sb);
        }

        private static StringBuilder AppendOver(StringBuilder sb, double value, float limit, int decimals)
        {
            bool over = !double.IsNaN(value) && value > limit;
            if (over)
                sb.Append(Red);

            PerfText.Fixed(sb, value, decimals);

            if (over)
                sb.Append(EndColor);

            return sb;
        }

        private static StringBuilder AppendMb(StringBuilder sb, long bytes) =>
            bytes >= 0 ? PerfText.Fixed(sb, bytes / (1024.0 * 1024.0), 0) : sb.Append('-');

        private void CreateView()
        {
            var canvasObject = new GameObject("Perf HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var safeObject = new GameObject("Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(canvasObject.transform, false);
            _safeArea = (RectTransform)safeObject.transform;
            ApplySafeArea();

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(_safeArea, false);

            _text = textObject.AddComponent<TextMeshProUGUI>();
            if (_text.font == null)
                _text.font = TMP_Settings.defaultFontAsset;

            _text.raycastTarget = false;
            _text.richText = true;
            _text.fontSize = FontSize;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.BottomLeft;

            RectTransform rect = _text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(6f, 4f);
            rect.sizeDelta = new Vector2(1200f, 120f);

            if (_text.font == null)
                Debug.LogWarning("[Perf] TMP 기본 폰트가 없어 성능 HUD 글자가 보이지 않습니다(TMP Settings의 Default Font Asset).");
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width <= 0 || Screen.height <= 0)
                return;

            _appliedSafeArea = safe;
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }
    }
}
#endif
