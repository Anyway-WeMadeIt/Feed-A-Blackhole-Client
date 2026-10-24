#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 테스트 도구(개발 패널)가 쓰는 화면 흐름. 에디터와 개발 빌드에만 있다.
    // 판 시작은 원래 시작과 같은 길(TryStart → 통계 시작 → 전투 화면)이고, 화면 전환으로 다 덮은 뒤에 한다.
    // 어느 화면에서든 부를 수 있다: 열린 창을 모두 닫고, 진행 중인 판은 포기한다(결산·통계 없음).
    internal sealed partial class ScreenFlow
    {
        // 시나리오를 빈 진행 상태에 넣어 검사만 한다. 지금 진행 상태는 바꾸지 않는다.
        internal bool CheckScenario(PlaytestScenario scenario, List<string> errors, List<string> warnings) =>
            ScenarioRunner.TryApply(scenario, new ProgressState(), _tree, _growth, errors, warnings);

        // 진행 상태를 시나리오의 시점으로 바꾸고, 판을 시작하거나(startBattle) 업그레이드 화면으로 간다.
        // 이후 이번 실행은 저장하지 않는다(테스트 세션). seed가 있으면 다음 판을 그 시드로 시작한다.
        internal void ApplyScenario(PlaytestScenario scenario, int? seed, bool startBattle, Action<GameSession> afterStart)
        {
            RunTest(() =>
            {
                var errors = new List<string>();
                var warnings = new List<string>();

                if (!ScenarioRunner.TryApply(scenario, _progress, _tree, _growth, errors, warnings))
                {
                    Debug.LogError($"[테스트] 시나리오 '{scenario.name}'을 넣지 못했다.\n  {string.Join("\n  ", errors)}");
                    return false;
                }

                if (warnings.Count > 0)
                    Debug.LogWarning($"[테스트] 시나리오 '{scenario.name}'을 맞춰 넣었다.\n  {string.Join("\n  ", warnings)}");

                _progressStore.EnterTestSession();

                if (seed.HasValue)
                    _battle.UseSeedForNextBattle(seed.Value);

                return startBattle;
            }, afterStart);
        }

        // 지금 진행 상태로 판을 바로 시작한다. seed가 있으면 그 시드로 시작한다.
        internal void StartTestBattle(int? seed, Action<GameSession> afterStart)
        {
            RunTest(() =>
            {
                if (seed.HasValue)
                    _battle.UseSeedForNextBattle(seed.Value);

                return true;
            }, afterStart);
        }

        // 이번 실행은 더 저장하지 않는다. 테스트 도구가 저장 파일을 지운 뒤 장면을 다시 부르기 전에 부른다.
        internal void StopSaving() => _progressStore.EnterTestSession();

        // 진행 중인 판을 지금 끝내고 결산한다(시간 종료와 같은 길).
        internal void EndBattleNow()
        {
            if (_battle.Session != null)
                RequestEnd();
        }

        // 덮인 뒤: 창 닫기 → 판 포기 → prepare(false면 업그레이드 화면) → 판 시작 → 통계 시작 → afterStart → 전투 화면.
        // afterStart는 통계 시작 뒤에 부른다 — 통계 시작이 판의 조작 표시를 지우므로 그 뒤의 조작이 표시에 남는다.
        private void RunTest(Func<bool> prepare, Action<GameSession> afterStart)
        {
            // 덮이는 동안 지금 판이 흐르지 않게 멈춘다. 흐르다 시간이 끝나면 끝내기 전환(RequestEnd)이 이 전환을 덮어쓴다.
            _battle.SetPaused(true);
            SoundManager.Instance?.PlaySwitchingScreens();

            _transition.Play(async () =>
            {
                // 엔딩 중에 새 테스트를 시작하면 엔딩 화면을 닫는다(엔딩의 결산은 하지 않는다 — 판은 아래에서 포기한다).
                CloseEnding();
                _ui.PopAllPanels(Unbind);

                if (await _battle.TryAbandonAsync())
                    _runId = null;

                if (!prepare())
                {
                    ShowUpgrade();
                    return;
                }

                if (!_battle.TryStart(NodePurchase.StatsFor(_progress, _tree)))
                    return;

                afterStart?.Invoke(_battle.Session);
                ShowBattle();
            });
        }
    }
}
#endif
