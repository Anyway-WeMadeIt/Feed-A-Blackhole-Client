using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 프레임 루프. 매 프레임 조준 → 판 진행 → 전투 화면 → 판 끝 분기 → 엔딩 진행 → 소리 → 커서 순서로 돈다.
    // 판이 끝났을 때 어디로 갈지(결산·엔딩)는 여기서 정하고, 실제 전환은 ScreenFlow가 한다.
    internal sealed class GameHost : IDisposable
    {
        private readonly UIManager _ui;
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
            GameSettings settings,
            BattleSounds sounds)
        {
            _ui = ui;
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
                HandleBattleEnded(reason);
            else if (_endAwaitsMilestoneEffect)
                RequestEndAfterMilestoneEffect();

            _screens.TickEnding();

            // 이 프레임의 타격·파괴 요청을 소리로 낸다(같은 프레임이면 파괴음만).
            _sounds.FlushFrame();
            RefreshCursor();
        }

        // 판이 이번 프레임에 규칙 안에서 끝났다. 전투 규칙상 종료는 이미 적용됐다(판이 더 흐르지 않는다).
        // - 시간 만료: 곧바로 결산 전환을 요청한다.
        // - 이정표 도달: 전투 화면의 달성 연출을 먼저 보여 주고, 연출이 끝난 뒤 결산 전환을 요청한다.
        //   마지막 이정표(보라 은하를 부순 판)면 결산 화면 대신 엔딩으로 간다(ScreenFlow.Ending). 달성 연출은 붕괴와 함께 보인다.
        //   튜토리얼 판은 자기 성장 표를 쓰므로 엔딩이 아니다.
        private void HandleBattleEnded(BattleEndReason reason)
        {
            switch (reason)
            {
                case BattleEndReason.TimeExpired:
                    _screens.RequestEnd();
                    return;

                case BattleEndReason.MilestoneReached:
                    if (!_battle.IsTutorial && IsFinalMilestone(_battle.Session.World.Hq) && _screens.TryBeginEnding())
                    {
                        if (_ui.CurrentRoot is BattleScreen ending)
                            ending.TryPlayMilestoneReached();

                        return;
                    }

                    if (_ui.CurrentRoot is BattleScreen screen && screen.TryPlayMilestoneReached())
                    {
                        _endAwaitsMilestoneEffect = true;
                        return;
                    }

                    _screens.RequestEnd();
                    return;
            }
        }

        // 이 이정표를 넘으면 더 이상 이정표가 없다(엔딩).
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
