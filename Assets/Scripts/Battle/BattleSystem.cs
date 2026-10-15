using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using Unity.Profiling;
using UnityEngine;

namespace BlackHole.Unity
{
    // 한 판의 수명과 전투 화면을 묶는다.
    // - 시작: 판을 조립하고(GameSessionFactory) 카메라를 이 판의 전장 배율에 맞춘 뒤 판을 시작.
    // - 시작 연출: 카메라가 블랙홀 가까이에서 머물다가 전장 전체로 천천히 넓어지고, 적은 블랙홀에 가까운 것부터 바깥쪽으로 차례로 나타난다.
    //   연출 동안 판은 진행하지 않는다(시간·조준·피해 없음, IsRunning이 false). 정지하면 연출도 멈춘다. 화면에서만 하는 일이다.
    // - 진행: 판을 한 Step 진행하고, 적·Breaker·사망 효과·블랙홀 화면을 판의 지금 상태에 맞춤.
    // - 정지: 판의 일이 아니라 여기의 상태다. 정지 중에는 판을 진행하지 않고 화면만 멈춘 채 맞춤.
    // - 종료: 남은 적을 치우고(처치 아님) 결산한 뒤 판을 버림. 포기는 결산하지 않음.
    internal sealed class BattleSystem
    {
        private enum State { Idle, Running, Paused, ShuttingDown }

        // 시작 연출 [임시].
        // 머묾: 화면 전환(블랙홀 아이리스)이 열리는 동안(약 0.5초) 블랙홀 가까이에서 머문다. 이때는 적이 보이지 않는다.
        private const float IntroHoldSeconds = 0.5f;
        // 넓힘: 이 시간 동안 카메라를 IntroStartZoom에서 1(전장 전체)로 넓힌다. 처음과 끝이 느린 곡선이다.
        private const float IntroWidenSeconds = 2f;
        // 시작 카메라 배율(전장 전체 = 1). 작을수록 블랙홀에 더 다가간 채 시작한다.
        private const float IntroStartZoom = 0.3f;

        // 성능 측정용 구간 이름표(Profiler의 Hierarchy·Timeline에 이 이름으로 보인다). 릴리스 빌드에서는 비용이 거의 없다.
        private static readonly ProfilerMarker TickMarker = new("Battle.Tick");
        private static readonly ProfilerMarker AdvanceMarker = new("Battle.Advance");
        private static readonly ProfilerMarker IntroMarker = new("Battle.Intro");
        private static readonly ProfilerMarker EnemyViewMarker = new("Battle.EnemyView");
        private static readonly ProfilerMarker BreakerViewMarker = new("Battle.BreakerView");
        private static readonly ProfilerMarker DeathEffectViewMarker = new("Battle.DeathEffectView");
        private static readonly ProfilerMarker HqViewMarker = new("Battle.HqView");

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
        // 시작 연출 중인가와 그 연출이 흐른 시간(초, 정지 중에는 흐르지 않는다).
        private bool _intro;
        private float _introElapsed;

        // 진행 중인 판. 판이 없으면 null.
        public GameSession Session { get; private set; }

        // 판이 진행 중인가(시작 연출·정지·종료·정리 중이 아님). 조준 입력과 커서가 본다.
        public bool IsRunning => CanPause && !_intro;

        // 정지할 수 있는가(시작 연출 중이거나 진행 중). 일시 정지 창과 테스트 도구가 본다. 연출 중에 정지하면 연출도 멈춘다.
        public bool CanPause => _state == State.Running && !Session.IsEnded;

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

            _intro = true;
            _introElapsed = 0;
            ApplyIntro();
            _enemyView.Synchronize(Session.World, false, 0f);

            _state = State.Running;
            PerfProbe.BattleStarted(LastSeed);
            return true;
        }

        // 다음 판 하나만 이 시드로 시작한다(테스트 도구: 시나리오의 시드, 같은 시드로 다시).
        public void UseSeedForNextBattle(int seed) => _nextSeed = seed;

        // 판을 한 Step 진행하고 전투 화면을 맞춘다. 진행 중인 판이 없으면 아무것도 하지 않는다.
        // 정지 중에는 판을 진행하지 않는다(화면은 멈춘 채 맞춘다). 시작 연출 중에는 판 대신 연출을 진행한다.
        // Ended는 이번 Step에 판이 끝났을 때만 true다.
        public AdvanceResult Tick(float delta)
        {
            if (!HasBattle)
                return default;

            using (TickMarker.Auto())
            {
                bool paused = _state == State.Paused;
                AdvanceResult result = default;

                if (!paused && _intro)
                {
                    using (IntroMarker.Auto())
                        AdvanceIntro(delta);
                }
                else if (!paused)
                {
                    using (AdvanceMarker.Auto())
                        result = Session.Advance(delta);
                }

                using (EnemyViewMarker.Auto())
                    _enemyView.Synchronize(Session.World, paused, delta);

                using (BreakerViewMarker.Auto())
                    _breakerView.Synchronize(Session.World, paused, delta);

                using (DeathEffectViewMarker.Auto())
                    _deathEffectView.Synchronize(Session.World, paused, delta);

                using (HqViewMarker.Auto())
                    _hqView.Synchronize(Session.World, delta);

                PerfProbe.SetAliveEnemies(Session.World.Enemies.Count);
                return result;
            }
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
            PerfProbe.BattleEnded();
            _state = State.ShuttingDown;
            EndIntro();

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

        private void AdvanceIntro(float delta)
        {
            _introElapsed += delta;

            if (_introElapsed >= IntroHoldSeconds + IntroWidenSeconds)
                EndIntro();
            else
                ApplyIntro();
        }

        // 연출이 흐른 시간에 맞춰 카메라 배율과 적이 보이는 반경을 정한다.
        // 배율은 곱으로 넓힌다(같은 시간에 같은 비율로 넓어져 고르게 보인다).
        // 보이는 반경은 넓히기 시작할 때 가장 가까운 적에서 출발해, 배율이 시작에서 1까지 간 비율만큼 가장 먼 적까지 넓힌다.
        // 머무는 동안은 0이라 아무 적도 보이지 않는다. 반경은 늘 카메라가 보여 주는 범위 안쪽이라 적이 화면 안에서 나타난다.
        private void ApplyIntro()
        {
            float t = Mathf.Clamp01((_introElapsed - IntroHoldSeconds) / IntroWidenSeconds);
            float eased = t * t * (3 - 2 * t);
            float zoom = Mathf.Pow(IntroStartZoom, 1 - eased);
            float reveal = (zoom - IntroStartZoom) / (1 - IntroStartZoom);

            _cameraFit.SetZoom(zoom);

            if (reveal <= 0)
            {
                _enemyView.RevealRadius = 0;
                return;
            }

            EnemyDistances(Session.World, out float nearest, out float farthest);
            _enemyView.RevealRadius = Mathf.Lerp(nearest, farthest, reveal);
        }

        // 연출을 끝낸다: 카메라는 전장 전체, 적은 모두 보인다. 연출 중이 아니어도 불러도 된다.
        private void EndIntro()
        {
            _intro = false;
            _cameraFit.SetZoom(1);
            _enemyView.RevealRadius = float.PositiveInfinity;
        }

        // 원점(HQ)에서 가장 가까운·먼 적 중심까지의 거리. 적이 없으면 둘 다 0.
        private static void EnemyDistances(World world, out float nearest, out float farthest)
        {
            IReadOnlyList<Enemy> enemies = world.Enemies;
            float near2 = float.PositiveInfinity;
            float far2 = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                Point2 position = enemies[i].Position;
                float distance2 = position.X * position.X + position.Y * position.Y;
                near2 = Mathf.Min(near2, distance2);
                far2 = Mathf.Max(far2, distance2);
            }

            nearest = enemies.Count == 0 ? 0 : Mathf.Sqrt(near2);
            farthest = Mathf.Sqrt(far2);
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
