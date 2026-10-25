using System.Threading.Tasks;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 판이 끝났을 때 어디로 가는지는 여기서 정한다. GameHost는 끝난 이유를 넘기고(HandleBattleEnded) 매 프레임 한 번 부른다(TickBattle).
    // - 시간 만료·종료 버튼·테스트 도구: 곧바로 결산 전환(RequestEnd). 튜토리얼 판은 결산하지 않고 첫 판으로(EndTutorialAsync).
    // - 이정표 도달: 전투 화면의 달성 연출을 먼저 보이고, 연출이 끝난 뒤 결산 전환.
    //   마지막 이정표(보라 은하를 부순 판)면 결산 대신 엔딩(ScreenFlow.Ending). 달성 연출은 붕괴와 함께 보인다.
    //   튜토리얼 판은 자기 성장 표를 쓰므로 엔딩이 아니다.
    // - 포기(일시 정지의 메인 메뉴, 테스트 도구): 결산 없이 타이틀 또는 새 판(ScreenFlow.Pause·Playtest).
    internal sealed partial class ScreenFlow
    {
        // 이정표에 닿아 판이 끝났고, 달성 연출이 끝나기를 기다리는 중. 끝나면 결산 전환을 한 번만 요청한다.
        private bool _endAwaitsMilestoneEffect;

        private void ShowBattle()
        {
            _ui.SwitchRoot<BattleScreen>(
                _presentations.Battle,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    root.ShowIdle();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(BattleScreen root)
        {
            AddBinding(root,
                r => r.PauseClicked += OpenPause,
                r => r.PauseClicked -= OpenPause);

            AddBinding(root,
                r => r.EndClicked += RequestEnd,
                r => r.EndClicked -= RequestEnd);
        }

        // 화면 버튼과 시간 종료가 같은 전환 경로를 사용한다. 판은 화면이 다 덮인 뒤에 바꾼다.
        // 덮인 뒤의 일에서 난 예외는 ScreenTransition이 로그로 남기고 전환을 이어 간다.
        // 시작: 덮인 뒤 판을 조립·시작하고 전투 화면으로 바꾼다. 덮이는 동안 판이 흐르지 않는다.
        private void RequestStart()
        {
            _sounds.PlaySwitchingScreens();
            _transition.Play(StartBattle);
        }

        // 판이 이번 프레임에 규칙 안에서 끝났다. 종료는 이미 적용됐다(판이 더 흐르지 않는다).
        internal void HandleBattleEnded(BattleEndReason reason)
        {
            if (reason != BattleEndReason.MilestoneReached)
            {
                RequestEnd();
                return;
            }

            if (!_battle.IsTutorial && IsFinalMilestone && TryBeginEnding())
            {
                PlayMilestoneReached();
                return;
            }

            if (PlayMilestoneReached())
                _endAwaitsMilestoneEffect = true;
            else
                RequestEnd();
        }

        // 매 프레임: 달성 연출이 끝났거나 전투 화면이 다른 이유로 닫혔으면 결산 전환을 한 번 요청한다.
        internal void TickBattle()
        {
            if (!_endAwaitsMilestoneEffect || IsCelebratingMilestone)
                return;

            _endAwaitsMilestoneEffect = false;

            // 판이 이미 없으면(다른 경로로 끝남) 요청하지 않는다.
            if (_battle.Session != null)
                RequestEnd();
        }

        // 끝: 덮인 뒤 판을 정리·결산하고 결산 화면으로 바꾼다. 적이 치워지는 모습이 보이지 않는다.
        // 튜토리얼 판은 결산하지 않고 첫 판으로 넘어간다(EndTutorialAsync).
        // 엔딩 중(마지막 이정표 뒤 붕괴·엔딩 화면)에는 받지 않는다 — 엔딩이 판을 결산하고 타이틀로 간다(ScreenFlow.Ending).
        // 종료 버튼, 판 끝 분기(시간 만료·이정표 연출 뒤), 테스트 도구가 이 길로 온다.
        private void RequestEnd()
        {
            if (IsEndingPlaying)
                return;

            _transition.Play(_battle.IsTutorial ? EndTutorialAsync : EndBattleAsync);
        }

        // 이 이정표를 넘으면 더 이상 이정표가 없다(엔딩).
        private bool IsFinalMilestone
        {
            get
            {
                Hq hq = _battle.Session.World.Hq;
                return hq.NextStage >= hq.Growth.MaxStage;
            }
        }

        private bool PlayMilestoneReached() =>
            _ui.CurrentRoot is BattleScreen screen && screen.TryPlayMilestoneReached();

        private bool IsCelebratingMilestone =>
            _ui.CurrentRoot is BattleScreen screen && screen.IsCelebratingMilestone;

        private void StartBattle()
        {
            if (_battle.TryStart(NodePurchase.StatsFor(_progress, _tree)))
            {
                // 판 시작 로그. 시작 Gold는 이 판을 시작할 때의 진행도 값이다.
                _recorder.StartRun(_battle.Session.StartInfo, _progress.Gold);
                ShowBattle();
            }
        }

        // 튜토리얼 판: 새 게임의 첫 화면(TutorialStage). 판 시작 로그를 남기지 않는다 — 서버에 보내는 판이 아니고,
        // 진행 상태에 결산하지도 않아(EndTutorialAsync) 진행도(Gold·성장도)가 바뀌지 않는다.
        // 열린 판은 없다(새 게임 직후이거나, 테스트 도구가 이전 판을 포기한 뒤다).
        private void StartTutorial()
        {
            if (_battle.TryStartTutorial(NodePurchase.StatsFor(_progress, _tree)))
                ShowBattle();
        }

        // 튜토리얼을 마친다.
        // - 이정표(소행성 셋을 모두 부숨)에 닿았으면 결산 화면을 보여 준다. 진행 상태는 바뀌지 않고 결산 로그도 없다.
        //   결산 화면의 업그레이드·계속부터는 일반 흐름이다.
        // - 이정표 전에 끝냈으면(종료 버튼 = 건너뛰기) 곧바로 첫 판을 시작한다.
        private async Task EndTutorialAsync()
        {
            BattleRawData raw = await _battle.TryEndTutorialAsync();

            if (raw == null)
                return;

            if (raw.ReachedMilestone)
                ShowSettlement(raw, tutorial: true);
            else
                StartBattle();
        }

        private async Task EndBattleAsync()
        {
            BattleRawData raw = await _battle.TryEndAsync();

            if (raw != null)
            {
                // 결산이 진행 상태(Gold·성장도)를 바꿨다. 결산 로그와 함께 저장한다.
                _recorder.Settle(raw);
                ShowSettlement(raw);
            }
        }
    }
}
