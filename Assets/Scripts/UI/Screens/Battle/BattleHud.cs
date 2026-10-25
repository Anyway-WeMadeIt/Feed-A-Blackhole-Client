using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 판의 상태를 전투 화면(BattleScreen)의 값으로 바꿔 넣는다. 매 프레임 GameHost가 부른다.
    // 판이 없으면 대기 상태를 보이고, 체력바를 보이는 종류(EnemyLook.healthBar, 은하)가 있는 판이면 그 체력바의 상태도 넘긴다.
    internal sealed class BattleHud
    {
        private readonly UIManager _ui;
        private readonly BattleSystem _battle;
        private readonly EnemyLooks _enemyLooks;
        // 이 판의 은하(체력바가 따라가는 적). 죽은 뒤에도 남겨 0이 된 막대를 보인다. 판이 바뀌면 다시 찾는다.
        private World _galaxyWorld;
        private Enemy _galaxy;

        public BattleHud(UIManager ui, BattleSystem battle, EnemyLooks enemyLooks)
        {
            _ui = ui;
            _battle = battle;
            _enemyLooks = enemyLooks;
        }

        public void Refresh()
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
                _enemyLooks.ColorOf(_galaxy.Definition.Type, _galaxy.Tier));
        }

        // 체력바를 보이는 종류(외형 칸)의 첫 마리. 판에 은하는 하나뿐이다.
        private Enemy FindGalaxy(World world)
        {
            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (_enemyLooks.LookOf(enemies[i].Definition.Type).healthBar)
                    return enemies[i];
            }

            return null;
        }
    }
}
