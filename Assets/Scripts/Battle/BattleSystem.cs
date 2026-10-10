using System;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 한 판의 수명과 전투 화면을 묶는다.
    // - 시작: 판을 조립하고(GameSessionFactory) 카메라를 이 판의 전장 배율에 맞춘 뒤 판을 시작.
    // - 진행: 판을 한 Step 진행하고, 적·Breaker·사망 효과·블랙홀 화면을 판의 지금 상태에 맞춤.
    // - 정지: 판의 일이 아니라 여기의 상태다. 정지 중에는 판을 진행하지 않고 화면만 멈춘 채 맞춤.
    // - 종료: 남은 적을 치우고(처치 아님) 결산한 뒤 판을 버림. 포기는 결산하지 않음.
    internal sealed class BattleSystem
    {
        private enum State { Idle, Running, Paused, ShuttingDown }

        private readonly GameContent _content;
        private readonly ProgressState _progress;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        private readonly BattleCameraFit _cameraFit;
        private State _state = State.Idle;
        // 다음 판에 쓸 시드. 테스트 도구만 정한다(UseSeedForNextBattle). 없으면 시각으로 정한다.
        private int? _nextSeed;

        // 진행 중인 판. 판이 없으면 null.
        public GameSession Session { get; private set; }

        // 판이 진행 중인가(정지·종료·정리 중이 아님). 조준 입력과 일시 정지 창, 커서가 본다.
        public bool IsRunning => _state == State.Running && !Session.IsEnded;

        public bool IsPaused => _state == State.Paused;

        // 마지막으로 시작한 판의 시드. 테스트 도구가 같은 판을 다시 할 때 쓴다.
        public int LastSeed { get; private set; }

        // 마지막으로 시작한 판의 업그레이드 수치(노드로 정해진 값). 테스트 도구가 메모에 계산된 수치를 남길 때 읽는다.
        public UpgradeStatValues Upgrades { get; private set; }

        // 판이 있는가(진행 또는 정지). 종료·포기는 이때만 한다.
        private bool HasBattle => _state == State.Running || _state == State.Paused;

        public BattleSystem(
            GameContent content,
            ProgressState progress,
            EnemyView enemyView,
            BreakerView breakerView,
            DeathEffectView deathEffectView,
            HqView hqView,
            BattleCameraFit cameraFit)
        {
            _content = content;
            _progress = progress;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _cameraFit = cameraFit;
        }

        public bool TryStart(UpgradeStatValues upgrades)
        {
            if (_state != State.Idle)
                return false;

            LastSeed = _nextSeed ?? Environment.TickCount;
            _nextSeed = null;
            Upgrades = upgrades;
            Session = GameSessionFactory.Create(_content, _progress, LastSeed, upgrades);

            _cameraFit.SetFieldScale(Session.World.Hq.FieldScale);

            ResetViews();
            Session.Begin();

            _enemyView.Synchronize(Session.World, false, 0f);

            _state = State.Running;
            return true;
        }

        // 다음 판 하나만 이 시드로 시작한다(테스트 도구: 시나리오의 시드, 같은 시드로 다시).
        public void UseSeedForNextBattle(int seed) => _nextSeed = seed;

        // 판을 한 Step 진행하고 전투 화면을 맞춘다. 진행 중인 판이 없으면 아무것도 하지 않는다.
        // 정지 중에는 판을 진행하지 않는다(화면은 멈춘 채 맞춘다). Ended는 이번 Step에 판이 끝났을 때만 true다.
        public AdvanceResult Tick(float delta)
        {
            if (!HasBattle)
                return default;

            bool paused = _state == State.Paused;
            AdvanceResult result = paused ? default : Session.Advance(delta);

            _enemyView.Synchronize(Session.World, paused, delta);
            _breakerView.Synchronize(Session.World, paused, delta);
            _deathEffectView.Synchronize(Session.World, paused, delta);
            _hqView.Synchronize(Session.World, delta);

            return result;
        }

        // 판을 멈추거나 다시 움직인다. 판이 없거나 이미 그 상태면 그대로 둔다.
        public void SetPaused(bool paused)
        {
            if (paused && _state == State.Running)
                _state = State.Paused;
            else if (!paused && _state == State.Paused)
                _state = State.Running;
        }

        // 판을 끝내고 결산.
        public async Task<BattleRawData> TryEndAsync()
        {
            if (!HasBattle)
                return null;

            return await ShutDownAsync(settle: true);
        }

        // 판을 포기. 결산하지 않음.
        public async Task<bool> TryAbandonAsync()
        {
            if (!HasBattle)
                return false;

            await ShutDownAsync(settle: false);
            return true;
        }

        // 판 종료(이미 끝났으면 결과 유지) -> 남은 적 정리 -> (결산이면) 집계·결산 -> 화면 정리 -> 판을 버림.
        private async Task<BattleRawData> ShutDownAsync(bool settle)
        {
            _state = State.ShuttingDown;

            Session.End();
            Session.ClearRemainingEnemies();

            BattleRawData raw = null;

            if (settle)
            {
                raw = Session.CreateRawData();
                Session.Settle();
            }

            // 지운 객체는 프레임 끝에 사라지므로 한 프레임 기다린 뒤 판을 버린다.
            ResetViews();
            await Awaitable.NextFrameAsync();

            Session = null;
            _state = State.Idle;
            return raw;
        }

        private void ResetViews()
        {
            _enemyView.Reset();
            _breakerView.Reset();
            _deathEffectView.Reset();
            _hqView.Reset();
        }
    }
}
