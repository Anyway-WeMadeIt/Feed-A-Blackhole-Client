using System;
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

            RefreshCursor();
        }

        // 판이 이번 프레임에 끝났다. 전투 규칙상 종료는 이미 적용됐다(판이 더 흐르지 않는다).
        // 이정표 도달이면 전투 화면의 달성 연출을 먼저 보여 주고, 연출이 끝난 뒤 결산 전환을 요청한다. 시간 만료는 곧바로 요청한다.
        private void HandleBattleEnded()
        {
            GameSession session = _battle.Session;
            bool milestone = session != null && session.World.Hq.ReachedMilestone;

            if (milestone && _ui.CurrentRoot is BattleScreen screen && screen.TryPlayMilestoneReached())
            {
                _endAwaitsMilestoneEffect = true;
                return;
            }

            _screens.HandleBattleTimeExpired();
        }

        // 달성 연출이 끝났거나 전투 화면이 다른 이유로 닫혔으면 결산 전환을 한 번 요청한다. 판이 이미 없으면(다른 경로로 끝남) 요청하지 않는다.
        private void RequestEndAfterMilestoneEffect()
        {
            if (_ui.CurrentRoot is BattleScreen screen && screen.IsCelebratingMilestone)
                return;

            _endAwaitsMilestoneEffect = false;

            if (_battle.Session != null)
                _screens.HandleBattleTimeExpired();
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
