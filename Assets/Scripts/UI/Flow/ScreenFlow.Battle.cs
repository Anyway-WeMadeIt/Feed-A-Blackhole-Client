using System.Threading.Tasks;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
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
            SoundManager.Instance.PlaySwitchingScreens();
            _transition.Play(StartBattle);
        }

        // 끝: 덮인 뒤 판을 정리·결산하고 결산 화면으로 바꾼다. 적이 치워지는 모습이 보이지 않는다.
        // 튜토리얼 판은 결산하지 않고 첫 판으로 넘어간다(EndTutorialAsync).
        // 엔딩 중(마지막 이정표 뒤 붕괴·엔딩 화면)에는 받지 않는다 — 엔딩이 판을 결산하고 타이틀로 간다(ScreenFlow.Ending).
        // GameHost(시간 만료, 이정표 연출 뒤)와 테스트 도구도 이 길로 온다.
        internal void RequestEnd()
        {
            if (IsEndingPlaying)
                return;

            _transition.Play(_battle.IsTutorial ? EndTutorialAsync : EndBattleAsync);
        }

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
