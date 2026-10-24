using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed class GameHost : IDisposable
    {
        private readonly UIManager _ui;
        private readonly BattleSystem _battle;
        private readonly AimInput _aim;
        private readonly ScreenFlow _screens;
        private readonly EnemyLooks _enemyLooks;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        private readonly CameraShake _cameraShake;
        private readonly GameSettings _settings;
        // 이정표에 닿아 판이 끝났고, 전투 화면의 달성 연출이 끝나기를 기다리는 중. 끝나면 한 번만 결산 전환을 요청한다.
        private bool _endAwaitsMilestoneEffect;
        // 이 판의 은하(체력바가 따라가는 적). 죽은 뒤에도 남겨 0이 된 막대를 보인다. 판이 바뀌면 다시 찾는다.
        private World _galaxyWorld;
        private Enemy _galaxy;

        public GameHost(
            UIManager ui,
            BattleSystem battle,
            AimInput aim,
            ScreenFlow screens,
            EnemyLooks enemyLooks,
            EnemyView enemyView,
            BreakerView breakerView,
            DeathEffectView deathEffectView,
            HqView hqView,
            CameraShake cameraShake,
            GameSettings settings)
        {
            _ui = ui;
            _battle = battle;
            _aim = aim;
            _screens = screens;
            _enemyLooks = enemyLooks;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _cameraShake = cameraShake;
            _settings = settings;
        }

        public void Start() => _screens.GoToTitle();

        public void Tick(float deltaTime)
        {
            _aim.Tick();

            // 적 공전 설정은 판 중에도 바꿀 수 있다. 판이 있으면 매 프레임 설정 값을 넣는다(bool 대입 하나).
            _battle.Session?.SetOrbitEnabled(_settings.IsOn(GameSettings.EnemyOrbit));

            AdvanceResult result = _battle.Tick(deltaTime);

            if (result.RaisedLevels > 0)
            {
                if (_settings.IsOn(GameSettings.ScreenShake))
                    _cameraShake.Play();

                SoundManager.Instance.PlayLevelUp();
            }

            // 화면을 먼저 맞춘다 — 판이 끝난 프레임이면 끝난 상태(10칸)를 보인 뒤에 연출·전환을 결정한다.
            RefreshBattleHud();

            if (result.Ended)
                HandleBattleEnded();
            else if (_endAwaitsMilestoneEffect)
                RequestEndAfterMilestoneEffect();

            _screens.TickEnding();

            RefreshCursor();
        }

        // 판이 이번 프레임에 끝났다. 전투 규칙상 종료는 이미 적용됐다(판이 더 흐르지 않는다).
        // 이정표 도달이면 전투 화면의 달성 연출을 먼저 보여 주고, 연출이 끝난 뒤 결산 전환을 요청한다. 시간 만료는 곧바로 요청한다.
        // 마지막 이정표(보라 은하를 부순 판)면 결산 화면 대신 엔딩으로 간다(ScreenFlow.Ending). 달성 연출은 붕괴와 함께 보인다.
        private void HandleBattleEnded()
        {
            GameSession session = _battle.Session;
            bool milestone = session != null && session.World.Hq.ReachedMilestone;

            if (milestone && !_battle.IsTutorial && IsFinalMilestone(session.World.Hq) && _screens.TryBeginEnding())
            {
                if (_ui.CurrentRoot is BattleScreen ending)
                    ending.TryPlayMilestoneReached();

                return;
            }

            if (milestone && _ui.CurrentRoot is BattleScreen screen && screen.TryPlayMilestoneReached())
            {
                _endAwaitsMilestoneEffect = true;
                return;
            }

            _screens.RequestEnd();
        }

        // 이 이정표를 넘으면 더 이상 이정표가 없다(엔딩). 튜토리얼 판은 자기 성장 표를 쓰므로 부르는 쪽이 뺀다.
        private static bool IsFinalMilestone(Hq hq) => hq.NextStage >= hq.Growth.MaxStage;

        // 달성 연출이 끝났거나 전투 화면이 다른 이유로 닫혔으면 결산 전환을 한 번 요청한다. 판이 이미 없으면(다른 경로로 끝남) 요청하지 않는다.
        private void RequestEndAfterMilestoneEffect()
        {
            if (_ui.CurrentRoot is BattleScreen screen && screen.IsCelebratingMilestone)
                return;

            _endAwaitsMilestoneEffect = false;

            if (_battle.Session != null)
                _screens.RequestEnd();
        }

        private void RefreshCursor()
        {
            // 판이 진행 중일 때만 숨긴다(조준점의 Breaker 링이 커서 역할을 한다).
            bool visible = !_battle.IsRunning;

            if (Cursor.visible != visible)
                Cursor.visible = visible;
        }

        private void RefreshBattleHud()
        {
            if (!(_ui.CurrentRoot is BattleScreen screen))
                return;

            GameSession session = _battle.Session;
            if (session == null)
            {
                screen.ShowIdle();
                return;
            }

            // Level은 규칙(Hq)에서 먼저 오르고 화면은 성장 공급의 바퀴(GrowthPacer)를 따라 한 바퀴씩 뒤따른다:
            // 보여줄 Level = Hq.Level − 아직 안 돈 바퀴 수, 바퀴가 도는 중이면 그 바퀴의 진행도, 아니면 EXP 진행도.
            Hq hq = session.World.Hq;
            GrowthPacer pacer = session.World.GrowthPacer;
            bool wheeling = pacer.IsAnimating;

            screen.Show(
                session.Remaining,
                session.AddedSeconds,
                session.World.EarnedGold,
                _battle.IsPaused,
                hq.Level - pacer.PendingWheels,
                wheeling ? pacer.WheelProgress : hq.Progress,
                wheeling,
                hq.StartLevel,
                hq.GoalLevel);

            screen.ShowGalaxy(GalaxyStateOf(session.World));
        }

        // 이 판의 은하 상태. 은하가 없는 판이면 null. 판에 은하는 하나뿐이다(HqGrowthDefinition.GalaxyTierAt).
        // 살아 있는 동안 한 번 찾아 두고, 죽으면(목록에서 빠져도) 그 적의 마지막 상태(체력 0)를 보인다.
        private GalaxyHealthState? GalaxyStateOf(World world)
        {
            if (world != _galaxyWorld)
            {
                _galaxyWorld = world;
                _galaxy = null;
            }

            if (world.Hq.GalaxyTier == HqGrowthDefinition.NoGalaxy)
                return null;

            _galaxy ??= FindGalaxy(world);

            if (_galaxy == null)
                return null;

            return new GalaxyHealthState(
                _galaxy.Id.Value,
                _galaxy.Tier,
                _galaxy.Size,
                _galaxy.Health,
                _galaxy.Stats.MaxHealth,
                _enemyLooks.ColorOf(EnemyType.Galaxy, _galaxy.Tier));
        }

        private static Enemy FindGalaxy(World world)
        {
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Definition.Type == EnemyType.Galaxy)
                    return enemies[i];
            }

            return null;
        }

        public void Dispose()
        {
            _screens.Dispose();
            _deathEffectView.Dispose();
            _hqView.Dispose();
            _breakerView.Dispose();
            _enemyView.Dispose();
            _enemyLooks.Dispose();
            Cursor.visible = true;
        }
    }
}
