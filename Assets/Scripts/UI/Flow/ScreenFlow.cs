using System;
using System.Collections.Generic;
using BlackHole.Core;
using BlackHole.Sync;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow : IDisposable
    {
        private readonly UIManager _ui;
        private readonly ScreenPresentations _presentations;
        private readonly BattleSystem _battle;
        private readonly ProgressState _progress;
        private readonly ProgressStore _progressStore;
        private readonly NodeTree _tree;
        private readonly IReadOnlyList<NodeItem> _nodes;// 업그레이드 화면에 그릴 노드(칸, 가격).
        private readonly IReadOnlyList<NodeRegionItem> _regions;// 업그레이드 화면에 그릴 구역(이름·테두리).
        private readonly HqGrowthDefinition _growth;// 블랙홀 성장(성장도별 Level 표, 이정표).
        private readonly GameSettings _settings;// 플레이어 설정.
        private readonly Localizer _localizer;// 코드가 쓰는 UI 글자의 번역.
        private readonly ScreenTransition _transition;
        private readonly KeyInput _keyInput;
        private readonly SyncLogFactory _syncLogs;// 판 시작·결산, 노드 구매, 새 게임을 서버 동기화 로그로 만든다.
        private readonly Dictionary<UIBase, List<Action>> _cleanupByScreen = new();
        // 지금 진행 중인 판의 runId(소문자 UUID). 판 시작 로그를 남긴 판만 가진다. 결산 로그가 같은 값을 싣는다.
        private string _runId;

        public ScreenFlow(
            UIManager ui,
            ScreenPresentations presentations,
            BattleSystem battle,
            ProgressState progress,
            ProgressStore progressStore,
            NodeTree tree,
            IReadOnlyList<NodeItem> nodes,
            IReadOnlyList<NodeRegionItem> regions,
            HqGrowthDefinition growth,
            GameSettings settings,
            Localizer localizer,
            ScreenTransition transition,
            KeyInput keyInput,
            SyncLogFactory syncLogs)
        {
            _ui = ui;
            _presentations = presentations;
            _battle = battle;
            _progress = progress;
            _progressStore = progressStore;
            _tree = tree;
            _nodes = nodes;
            _regions = regions;
            _growth = growth;
            _settings = settings;
            _localizer = localizer;
            _transition = transition;
            _keyInput = keyInput;
            _syncLogs = syncLogs;

            _keyInput.ContinuePressed += HandleKeyActionSpace;
            _keyInput.UpgradePressed += HandleKeyActionShift;
            _keyInput.PausePressed += HandleKeyActionEsc;
        }

        private void BindView<T>(T screen, Action<T> apply) where T : UIBase
        {
            Unbind(screen);
            apply(screen);
        }

        private void AddBinding<T>(T screen, Action<T> attach, Action<T> detach) where T : UIBase
        {
            attach(screen);
            AddCleanup(screen, () => detach(screen));
        }

        private void AddCleanup(UIBase screen, Action cleanup)
        {
            if (!_cleanupByScreen.TryGetValue(screen, out List<Action> cleanups))
            {
                cleanups = new List<Action>();
                _cleanupByScreen[screen] = cleanups;
            }

            cleanups.Add(cleanup);
        }

        private void Unbind(UIBase screen)
        {
            if (!_cleanupByScreen.TryGetValue(screen, out List<Action> cleanups))
                return;

            _cleanupByScreen.Remove(screen);
            RunCleanups(cleanups);
        }

        private static void RunCleanups(List<Action> cleanups)
        {
            for (int i = cleanups.Count - 1; i >= 0; i--)
                cleanups[i]?.Invoke();
        }

        public void Dispose()
        {
            _keyInput.ContinuePressed -= HandleKeyActionSpace;
            _keyInput.UpgradePressed -= HandleKeyActionShift;
            _keyInput.PausePressed -= HandleKeyActionEsc;

            foreach (List<Action> cleanups in _cleanupByScreen.Values)
                RunCleanups(cleanups);

            _cleanupByScreen.Clear();
        }
    }
}
