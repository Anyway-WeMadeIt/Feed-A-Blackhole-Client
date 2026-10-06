using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 조립된 게임의 실행 수명. GameBootstrap이 Awake에서 만들고 Start/Update/OnDestroy에서 호출한다.
    // 매 프레임: 조준 입력 → 전투 진행 → 판이 끝난 프레임에 화면 흐름에 알림 → 전투 HUD 갱신 → 마우스 커서.
    // 마우스 커서: 판이 진행 중일 때만 숨긴다(조준점의 Breaker 링이 커서 역할을 한다). 일시정지·결산·타이틀에서는 보인다.
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

        public GameHost(UIManager ui, BattleSystem battle, AimInput aim, ScreenFlow screens,
            EnemyLooks enemyLooks, EnemyView enemyView, BreakerView breakerView,
            DeathEffectView deathEffectView, HqView hqView, CameraShake cameraShake)
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
        }

        public void Start() => _screens.GoToTitle();

        // public void Tick(float deltaTime)
        // {
        //     _aim.Tick();
        //     if (_battle.Tick(deltaTime))
        //         _screens.HandleBattleTimeExpired();
        //     RefreshBattleHud();
        // }

        public void Tick(float deltaTime)
        {
            _aim.Tick();

            BattleStepResult result = _battle.Tick(deltaTime);

            if (result.BattleEnded)
                _screens.HandleBattleTimeExpired();

            if (result.Raised > 0)
            {
                _cameraShake.Play();
                SoundManager.Instance.PlayLevelUp();
            }

            RefreshBattleHud();
            RefreshCursor();
        }

        private void RefreshCursor()
        {
            GameSession session = _battle.Session;
            bool visible = session == null || session.Phase != SessionPhase.Running;

            if (Cursor.visible != visible)
                Cursor.visible = visible;
        }

        private void RefreshBattleHud()
        {
            if (!(_ui.CurrentRoot is BattleScreen screen))
                return;

            GameSession session = _battle.Session;
            if (session == null)
                screen.ShowIdle();
            else
                screen.Show(session.Remaining, session.World.EarnedGold, session.Phase == SessionPhase.Paused,
                    session.World.Hq.Level, session.World.Hq.Progress, session.World.Hq.GoalLevel);
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
