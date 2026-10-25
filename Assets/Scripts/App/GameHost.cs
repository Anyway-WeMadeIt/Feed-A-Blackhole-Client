using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 프레임 루프. 매 프레임 조준 → 판 진행 → 전투 화면 → 판 끝 → 소리 → 커서 순서로 돈다.
    // 판이 끝났을 때 어디로 갈지(결산·엔딩)는 ScreenFlow가 정한다(ScreenFlow.Battle).
    internal sealed class GameHost : IDisposable
    {
        private readonly BattleSystem _battle;
        private readonly AimInput _aim;
        private readonly ScreenFlow _screens;
        private readonly BattleHud _hud;
        private readonly EnemyLooks _enemyLooks;
        private readonly EnemyView _enemyView;
        private readonly BreakerView _breakerView;
        private readonly DeathEffectView _deathEffectView;
        private readonly HqView _hqView;
        private readonly CameraShake _cameraShake;
        private readonly GameSettings _settings;
        private readonly BattleSounds _sounds;

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
            GameSettings settings,
            BattleSounds sounds)
        {
            _battle = battle;
            _aim = aim;
            _screens = screens;
            _hud = new BattleHud(ui, battle, enemyLooks);
            _enemyLooks = enemyLooks;
            _enemyView = enemyView;
            _breakerView = breakerView;
            _deathEffectView = deathEffectView;
            _hqView = hqView;
            _cameraShake = cameraShake;
            _settings = settings;
            _sounds = sounds;
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

                _sounds.PlayLevelUp();
            }

            // 화면을 먼저 맞춘다 — 판이 끝난 프레임이면 끝난 상태(10칸)를 보인 뒤에 연출·전환을 결정한다.
            _hud.Refresh();

            if (result.EndReason is BattleEndReason reason)
                _screens.HandleBattleEnded(reason);

            _screens.TickBattle();
            _screens.TickEnding();

            // 이 프레임의 타격·파괴 요청을 소리로 낸다(같은 프레임이면 파괴음만).
            _sounds.FlushFrame();
            RefreshCursor();
        }

        private void RefreshCursor()
        {
            // 판이 진행 중일 때만 숨긴다(조준점의 Breaker 링이 커서 역할을 한다).
            bool visible = !_battle.IsRunning;

            if (Cursor.visible != visible)
                Cursor.visible = visible;
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
