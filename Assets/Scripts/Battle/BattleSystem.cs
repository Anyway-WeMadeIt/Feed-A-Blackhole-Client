using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 한 판의 수명과 전투 화면을 묶는다.
    // - 시작: 판을 조립하고(GameSessionFactory) 카메라를 이 판의 전장 배율에 맞춘 뒤 판을 시작.
    // - 시작 연출: 카메라가 블랙홀 가까이에서 머물다가 전장 전체로 천천히 넓어지고, 적은 블랙홀에 가까운 것부터 바깥쪽으로 차례로 나타난다.
    //   연출 동안 판은 진행하지 않는다(시간·조준·피해 없음, IsRunning이 false). 정지하면 연출도 멈춘다. 화면에서만 하는 일이다.
    // - 튜토리얼 판: 정해 둔 적만 두고 시간 제한 없이 시작한다(TutorialStage). 시작 연출 없이 좁은 카메라에서 머문다.
    // - 진행: 판을 한 Step 진행하고, 적·Breaker·사망 효과·블랙홀 화면을 판의 지금 상태에 맞춤.
    // - 정지: 판의 일이 아니라 여기의 상태다. 정지 중에는 판을 진행하지 않고 화면만 멈춘 채 맞춤.
    // - 종료: 남은 적을 치우고(처치 아님) 결산한 뒤 판을 버림. 포기는 결산하지 않음.
    // - 붕괴(엔딩): 마지막 이정표로 끝난 판에서만. 판은 이미 끝나 규칙은 멈췄고, 화면에서 남은 적이 소용돌이치며 블랙홀로 빨려 들어가고
    //   블랙홀이 커지며 카메라가 다가간다(TryBeginCollapse → HasCollapsed). 결산은 그 뒤 엔딩 흐름이 한다(ScreenFlow.Ending).
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

        // 붕괴 연출(엔딩) [임시].
        // 기다림: 마지막 은하가 나선으로 말려 사라지는 동안(EnemyVanishes 0.7초) 나머지는 멈춰 있다.
        private const float CollapseDelaySeconds = 0.9f;
        // 빨려 들어감: 가까운 적부터 먼 적까지 이 시간 안에 모두 블랙홀에 닿는다(EnemyView.Collapse).
        private const float CollapseSeconds = 3.4f;
        // 다 빨아들인 뒤 블랙홀이 번쩍이고 엔딩 화면이 덮기 시작할 때까지.
        private const float CollapseHoldSeconds = 1.0f;
        // 끝날 때의 카메라 배율(전장 전체 = 1). 블랙홀 쪽으로 다가간다.
        private const float CollapseZoom = 0.55f;

        private readonly GameContent _content;
        private readonly ProgressState _progress;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;

        // 마우스·펜이 가리키는 자리(AimInput이 매 프레임 넣는다). 화면(BreakerView 대기 링)만 읽고 판·Core에는 들어가지 않는다.
        public Point2? HoverPoint { get; set; }
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        private readonly TutorialHintView _tutorialHint;
        private readonly BattleCameraFit _cameraFit;
        private readonly BattleSounds _sounds;
        private State _state = State.Idle;
        // 다음 판에 쓸 시드. 테스트 도구만 정한다(UseSeedForNextBattle). 없으면 시각으로 정한다.
        private int? _nextSeed;
        // 시작 연출 중인가와 그 연출이 흐른 시간(초, 정지 중에는 흐르지 않는다).
        private bool _intro;
        private float _introElapsed;
        // 붕괴 연출 중인가와 그 연출이 흐른 시간(초).
        private bool _collapse;
        private float _collapseElapsed;
        private bool _collapseBurst;

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

        // 지금 판이 튜토리얼 판인가. 판이 없으면 false.
        public bool IsTutorial { get; private set; }

        // 튜토리얼 판에서 마지막으로 무리를 놓은 Level(TutorialStage.WaveAt).
        private int _tutorialWaveLevel;

        // 붕괴 연출(엔딩)을 다 보였는가. 엔딩 흐름이 이때 엔딩 화면을 연다.
        public bool HasCollapsed => _collapse && _collapseElapsed >= CollapseDelaySeconds + CollapseSeconds + CollapseHoldSeconds;

        // 판이 있는가(진행 또는 정지). 종료·포기는 이때만 한다.
        private bool HasBattle => _state == State.Running || _state == State.Paused;

        public BattleSystem(
            GameContent content,
            ProgressState progress,
            EnemyView enemyView,
            BreakerView breakerView,
            DeathEffectView deathEffectView,
            HqView hqView,
            TutorialHintView tutorialHint,
            BattleCameraFit cameraFit,
            BattleSounds sounds)
        {
            _content = content;
            _progress = progress;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _tutorialHint = tutorialHint;
            _cameraFit = cameraFit;
            _sounds = sounds;
        }

        public bool TryStart(UpgradeStatValues upgrades) => TryStart(upgrades, tutorial: false);

        // 튜토리얼 판을 시작한다(TutorialStage). 판 구성·Breaker는 일반 판과 같은 길로 만든다.
        public bool TryStartTutorial(UpgradeStatValues upgrades) => TryStart(upgrades, tutorial: true);

        private bool TryStart(UpgradeStatValues upgrades, bool tutorial)
        {
            if (_state != State.Idle)
                return false;

            LastSeed = _nextSeed ?? Environment.TickCount;
            _nextSeed = null;
            Upgrades = upgrades;
            IsTutorial = tutorial;
            Session = tutorial
                ? GameSessionFactory.CreateWithGrowth(_content, _progress, LastSeed, upgrades, TutorialStage.GrowthFrom(_content.Enemies))
                : GameSessionFactory.Create(_content, _progress, LastSeed, upgrades);

            _cameraFit.SetFieldScale(Session.World.Hq.FieldScale);

            ResetViews();

            if (tutorial)
            {
                // 시작 연출 없이 블랙홀 가까이에 머문다. 적은 처음부터 모두 보인다(출현 팝 연출은 그대로).
                // 블랙홀은 튜토리얼 크기 표를 따른다(처음엔 거의 보이지 않음).
                _tutorialWaveLevel = HqGrowthDefinition.StartLevel;
                Session.BeginStaged(TutorialStage.WaveAt(_content.Enemies, _tutorialWaveLevel));
                _intro = false;
                _cameraFit.SetZoom(TutorialStage.CameraZoom);
                _enemyView.RevealRadius = float.PositiveInfinity;
                _hqView.Radii = TutorialStage.BlackHoleRadii;
            }
            else
            {
                Session.Begin();
                _intro = true;
                _introElapsed = 0;
                ApplyIntro();
            }

            _enemyView.Synchronize(Session.World, false, 0f);

            _state = State.Running;
            return true;
        }

        // 이미 끝난 판(마지막 이정표)에서 붕괴 연출을 시작한다. 판이 없거나 끝나지 않았거나 이미 시작했으면 false.
        // 정리·결산은 하지 않는다 — 연출이 끝난 뒤 TryEndAsync로 한다.
        public bool TryBeginCollapse()
        {
            if (!HasBattle || !Session.IsEnded || _collapse)
                return false;

            _collapse = true;
            _collapseElapsed = 0;
            _collapseBurst = false;
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

            bool paused = _state == State.Paused;
            AdvanceResult result = default;

            if (!paused && _intro)
                AdvanceIntro(delta);
            else if (!paused)
                result = Session.Advance(delta);

            if (!paused && _collapse)
                AdvanceCollapse(delta);

            if (!paused && IsTutorial)
                StageNextTutorialWave();

            _enemyView.Synchronize(Session.World, paused, delta);
            _breakerView.Synchronize(Session.World, HoverPoint, paused, delta);
            _deathEffectView.Synchronize(Session.World, paused, delta);
            _hqView.Synchronize(Session.World, delta);
            _tutorialHint.Synchronize(Session.World, IsTutorial && !Session.IsEnded, paused, delta);

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

        // 튜토리얼 판을 끝낸다. 결과(원자료)는 만들지만 진행 상태에 결산하지 않는다 — 튜토리얼의 이정표는 콘텐츠의 것이 아니다.
        public async Task<BattleRawData> TryEndTutorialAsync()
        {
            if (!HasBattle || !IsTutorial)
                return null;

            return await ShutDownAsync(settle: true, applyToProgress: false);
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
        // applyToProgress가 false면 원자료만 만들고 진행 상태(Gold·성장도)는 바꾸지 않는다(튜토리얼).
        private async Task<BattleRawData> ShutDownAsync(bool settle, bool applyToProgress = true)
        {
            _state = State.ShuttingDown;
            EndIntro();
            _collapse = false;

            Session.End();
            Session.ClearRemainingEnemies();

            BattleRawData raw = null;

            if (settle)
            {
                raw = Session.CreateRawData();

                if (applyToProgress)
                    Session.Settle();
            }

            // 지운 객체는 프레임 끝에 사라지므로 한 프레임 기다린 뒤 판을 버린다.
            ResetViews();
            await Awaitable.NextFrameAsync();

            Session = null;
            IsTutorial = false;
            _state = State.Idle;
            return raw;
        }

        // 튜토리얼: Level이 올랐으면 그 Level의 무리를 놓는다. 게이지 바퀴가 다 돈 뒤(일반 판의 성장 공급과 같은 시각)에 놓는다.
        private void StageNextTutorialWave()
        {
            World world = Session.World;
            int level = world.Hq.Level;

            if (Session.IsEnded || level <= _tutorialWaveLevel || world.GrowthPacer.IsAnimating)
                return;

            _tutorialWaveLevel = level;
            Session.Stage(TutorialStage.WaveAt(_content.Enemies, level));
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

        // 붕괴: 기다린 뒤 적을 빨아들이고(0 → 1) 블랙홀을 키우며 카메라를 다가가게 한다. 다 빨아들이는 순간 한 번 터지는 소리.
        private void AdvanceCollapse(float delta)
        {
            _collapseElapsed += delta;
            float t = Mathf.Clamp01((_collapseElapsed - CollapseDelaySeconds) / CollapseSeconds);
            float eased = t * t * (3 - 2 * t);

            _enemyView.Collapse = t;
            _hqView.Collapse = t;
            _cameraFit.SetZoom(Mathf.Lerp(1, CollapseZoom, eased));

            if (t >= 1 && !_collapseBurst)
            {
                _collapseBurst = true;
                _sounds.PlaySuperNova();
            }
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
            _tutorialHint.Reset();
        }
    }
}
