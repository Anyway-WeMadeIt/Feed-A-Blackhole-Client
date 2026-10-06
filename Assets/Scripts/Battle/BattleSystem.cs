using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적·전투 시스템: 한 판(GameSession)과 그 표현(적 화면 EnemyView, Breaker 화면 BreakerView, 사망 효과 화면 DeathEffectView, 블랙홀 화면 HqView)의 수명을 가진다.
    // 스킬은 판의 일부다 — 판 조립 때 참가자마다 생기고 판과 함께 버려진다. 그래서 스킬 화면(BreakerView)도 적 화면과 같이 정리한다.
    //
    // 스스로 시작하거나 끝내지 않는다. 화면 흐름(ScreenFlow)이 버튼·시간 만료에서 부를 때만 시작(TryStart)하고 정리(TryEndAsync)한다.
    // 할 수 없는 때(진행 중인 판이 있을 때의 시작, 판이 없거나 정리 중일 때의 종료)의 요청은 무시한다.
    // Tick은 판을 진행하고 종료에 도달한 순간을 돌려준다.
    //
    // 진행 상태(PlayerState: Gold, 성장도)는 방장의 것 하나다. 판을 조립할 때 읽고, 결산 때 바뀐다. 전투 사이에 이어진다(저장은 없다).
    //
    // 시작 단계:
    //   1. 업그레이드에서 바뀐 수치 받기 — 판을 조립한다: 업그레이드 표로
    //      이 판의 판 구성(해금·질량 단계·황금 비율·황금 배율·더할 공급 수·성장 공급 수), 적 수치·색 비율, 블랙홀의 Level업마다 늘어나는 시간을 확정한다.
    //   2. 카메라를 이 판의 전장 배율(이정표마다 넓어진다)에 맞춘다.
    //   3. 적 소환 단계 진입 — 전투 시작 공급을 내보내고 판을 진행 단계로 넣는다.
    // 종료 단계:
    //   1. 종료 요청                   2. 화면에서 관리하던 적의 수가 0(남은 적·요청 정리 — 처치 아님)
    //   3. 죽은 적의 처리 완료          4. 처치 집계와 번 Gold를 계산해 보관(원자료)
    //   5. 결산(번 Gold를 진행 상태에)   6. 화면의 연출 정리
    //   7. 모두 끝났으면 완전 초기화
    // 종료 뒤에 남는 것은 UI와 원자료(LastRawData)뿐이다. 판을 시작했던 다른 흔적은 없다.
    internal sealed class BattleSystem
    {
        private enum State { Idle, Starting, Running, ShuttingDown, Faulted }

        private readonly GameContent _content;
        private readonly PlayerState _progress;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        // 전투 카메라의 크기 맞춤. 없으면(씬 구성 누락) 카메라가 전장 배율을 따르지 않는다.
        private readonly BattleCameraFit _cameraFit;
        private State _state = State.Idle;

        // 진행 중인(또는 정리 중인) 판. 시작 전과 완전 초기화 뒤에는 null이다.
        public GameSession Session { get; private set; }
        // 마지막으로 정리한 판의 원자료. 정리가 끝난 뒤에도 남는다.
        public BattleRawData LastRawData { get; private set; }
        public bool IsRunning => _state == State.Running;
        // 정리를 요청할 수 있는가: 진행 중이거나, 앞선 정리가 실패해 멈춘 상태.
        private bool CanShutdown => _state == State.Running || _state == State.Faulted;

        public BattleSystem(GameContent content, PlayerState progress, EnemyView enemyView, BreakerView breakerView,
            DeathEffectView deathEffectView, HqView hqView, BattleCameraFit cameraFit)
        {
            _content = content;
            _progress = progress;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _cameraFit = cameraFit;
        }

        // 전투 진입을 위한 초기화. 준비된 상태가 아니면 무시하고 false를 돌려준다.
        // upgrades: 방장의 산 노드로 만든 업그레이드 표(NodePurchase.UpgradesFor). 전투 시스템은 노드 트리를 모른다.
        public bool TryStart(UpgradeTable upgrades)
        {
            if (_state != State.Idle)
                return false;

            _state = State.Starting;
            // 전투마다 seed를 새로 정한다. 쓴 seed는 판과 원자료에 남는다.
            int seed = Environment.TickCount;

            // 1. 업그레이드에서 바뀐 수치 받기: 업그레이드 표로
            //    이 판의 Breaker 수치, 판 구성과 적 수치 표를 확정한다. 판이 끝날 때까지 바뀌지 않는다.
            //    조립이 실패하면(산 노드 조합이 한계 밖 등) 판이 없으므로 준비된 상태로 돌아간다.
            try
            {
                Session = SessionAssembler.CreateBattle(_content, _progress, seed, upgrades);
            }
            catch
            {
                _state = State.Idle;
                throw;
            }

            // 2. 카메라를 이 판의 전장 배율에 맞춘다. 적·Breaker는 월드 크기라 그만큼 작아 보인다.
            if (_cameraFit != null)
                _cameraFit.SetFieldScale(Session.World.Hq.FieldScale);

            // 3. 적 소환 단계 진입.
            Session.Begin();
            _enemyView.Reset();
            // 시작 직후 스냅: 아직 지난 시간이 없으니 흔들림 연출 없이 위치만 맞춘다.
            _enemyView.Synchronize(Session.World, false, 0f);
            _breakerView.Reset();
            _deathEffectView.Reset();
            _hqView.Reset();

            _state = State.Running;
            return true;
        }

        // 전투 Step과 적·스킬·사망 효과·블랙홀 표현을 진행한다. 이번 Step에서 판이 끝났을 때만 true를 반환한다.
        public BattleStepResult Tick(float delta)
        {
            // if (_state != State.Running)
            //     return false;

            if (_state != State.Running)
                return new BattleStepResult(false, 0);

            bool wasRunning = Session.Phase == SessionPhase.Running;

            int raised = Session.Advance(delta);

            //Session.Advance(delta);
            bool paused = Session.Phase == SessionPhase.Paused;
            _enemyView.Synchronize(Session.World, paused, delta);
            _breakerView.Synchronize(Session.World, paused, delta);
            _deathEffectView.Synchronize(Session.World, Session.Phase == SessionPhase.Paused, delta);
            _hqView.Synchronize(Session.World, delta);

            bool battleEnded = wasRunning && Session.Phase == SessionPhase.Ended;

            //return wasRunning && Session.Phase == SessionPhase.Ended;
            return new BattleStepResult(battleEnded, raised);
        }

        public void TogglePause()
        {
            if (_state == State.Running)
                Session.TogglePause();
        }

        // 전투 종료 뒤 자신의 모든 것을 정리하고 원자료를 돌려준다. 정리할 판이 없거나 정리 중이면 무시하고 null을 돌려준다.
        // 단계 하나라도 확인에 실패하면 멈추고(Faulted) 완전 초기화하지 않는다. 다시 부르면 처음부터 확인한다.
        public async Task<BattleRawData> TryEndAsync()
        {
            if (!CanShutdown)
                return null;

            _state = State.ShuttingDown;

            try
            {
                // 1. 종료 요청. 시간이 끝나 이미 끝난 판이면 기존 결과를 유지한다.
                Session.RequestEnd();

                // 2. 화면에서 관리하던 적의 수가 0. 남은 적은 처치가 아니라 정리다.
                //    처리되지 않은 생성·파괴 요청도 함께 버린다 — 끝난 판은 새 적도, 새 사망도 만들지 않는다.
                Session.ClearRemainingEnemies();
                World world = Session.World;
                Verify(world.Enemies.Count == 0 && world.PendingSpawns.Count == 0 && world.PendingDestroys.Count == 0,
                    "Enemies on screen: 0");

                // 3. 죽은 적의 처리 완료(사망 효과·보상 처리가 붙으면 그것이 끝났는지까지).
                Verify(!Session.World.HasPendingDeathProcessing, "Dead enemies processed");

                // 4. 처치 집계와 번 Gold를 계산해 보관.
                LastRawData = Session.CreateRawData();
                Verify(LastRawData != null, "Kill tally stored");

                // 5. 결산: 판이 번 Gold를 진행 상태에 한 번 더한다. 앞선 정리가 이 뒤에서 실패했다가 다시 와도 두 번 더하지 않는다.
                Session.Settle();
                Verify(Session.IsSettled, "Gold settled");

                // 6. 화면의 연출 정리. 지운 객체는 프레임 끝에 사라지므로 한 프레임 기다린 뒤 확인한다.
                _enemyView.Reset();
                _breakerView.Reset();
                _deathEffectView.Reset();
                _hqView.Reset();
                await Awaitable.NextFrameAsync();
                Verify(_enemyView.IsClear && _breakerView.IsClear && _deathEffectView.IsClear && _hqView.IsClear, "Presentation cleared");

                // 7. 완전 초기화: 판을 버린다.
                Session = null;
                _state = State.Idle;
                return LastRawData;
            }
            catch
            {
                _state = State.Faulted;
                throw;
            }
        }

        private static void Verify(bool passed, string stepName)
        {
            if (!passed)
                throw new InvalidOperationException($"전투 정리 단계 실패: {stepName}.");
        }

        /// <summary>
        /// 전투 중도 포기
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TryAbandonAsync()
        {
            if (_state != State.Running) return false;

            _state = State.ShuttingDown;

            try
            {
                // 이번 판의 결과를 PlayerState에 반영하지 않는다.
                // Session.Settle()을 호출하지 않는다.

                Session.RequestEnd();
                Session.ClearRemainingEnemies();

                World world = Session.World;

                Verify(
                    world.Enemies.Count == 0 &&
                    world.PendingSpawns.Count == 0 &&
                    world.PendingDestroys.Count == 0,
                    "Enemies on screen: 0"
                );

                Verify(!Session.World.HasPendingDeathProcessing,
                    "Dead enemies processed");

                // 이번 판의 RawData도 필요 없다면 생성하지 않아도 된다.
                // LastRawData도 유지하지 않는다.

                _enemyView.Reset();
                _breakerView.Reset();
                _deathEffectView.Reset();
                _hqView.Reset();

                await Awaitable.NextFrameAsync();

                Verify(
                    _enemyView.IsClear &&
                    _breakerView.IsClear &&
                    _deathEffectView.IsClear &&
                    _hqView.IsClear,
                    "Presentation cleared"
                );

                // 전투 자체를 폐기
                Session = null;
                _state = State.Idle;

                return true;
            }
            catch
            {
                _state = State.Faulted;
                throw;
            }
        }
    }
}
