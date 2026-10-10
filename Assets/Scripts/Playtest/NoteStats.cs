#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 메모에 함께 남기는 계산된 수치: 게임이 그 판에 실제로 쓰는 값을 판 객체(GameSession·EnemyStatTable)에서 읽는다.
    // 노드 ID를 노드·업그레이드 시트와 맞춰 보지 않아도 그 판이 어땠는지 읽히게 하려는 것(시트가 나중에 바뀌어도 남는다).
    // - player: 블랙홀(이정표 단계·Level·전장 배율·제한 시간)과 Breaker(피해·공격 주기·반경·치명 등).
    // - enemies: 종류(계약 ID)마다 살아 있는 수, 색 등급별 HP·Gold·EXP·속도, 크기·질량·공급, 성질 확률·효과 수치.
    // - upgrades: 기본값과 달라진 업그레이드 수치(StatId: 기본 → 지금). 노드를 해석한 결과다.
    // 테스트 세팅 창의 확정 정보(TestSetupPreview)와 같은 객체를 읽는다.
    // at: 언제 읽었나 — "now"(메모를 적는 순간의 판) | "battleStart"(판이 끝난 뒤라 그 판이 시작할 때 읽어 둔 값) | "preview"(창의 세팅으로 조립한 판).
    internal static class NoteStats
    {
        public const string AtNow = "now";
        public const string AtBattleStart = "battleStart";
        public const string AtPreview = "preview";

        public static JsonObject Capture(GameSession session, GameContent content, UpgradeStatValues upgrades, string at)
        {
            if (session == null || content == null)
                return null;

            return new JsonObject
            {
                { "at", at },
                { "player", Player(session, content, upgrades) },
                { "enemies", Enemies(session.World, content) },
                { "upgrades", upgrades != null ? Upgrades(upgrades) : null },
            };
        }

        private static JsonObject Player(GameSession session, GameContent content, UpgradeStatValues upgrades)
        {
            Hq hq = session.World.Hq;
            HqMilestone next = hq.Growth.NextMilestoneAt(hq.Stage);

            var player = new JsonObject
            {
                { "stage", hq.Stage },
                { "level", hq.Level },
                { "stageStartLevel", hq.StartLevel },
                { "goalLevel", hq.GoalLevel != HqGrowthDefinition.NoGoal ? hq.GoalLevel : (object)null },
                { "milestoneGold", next != null ? next.TargetGold : (object)null },
                { "fieldScale", R(hq.FieldScale) },
                { "growthTime", R(hq.GrowthTime) },
                { "elapsed", R(session.Elapsed) },
                { "remaining", R(session.Remaining) },
            };

            if (upgrades != null)
            {
                TimeLimitDefinition limit = content.TimeLimit.Upgraded(upgrades);
                player.Add("timeLimit", R(limit.Duration));
                player.Add("killTimeBonus", R(limit.KillTimeBonus));
            }

            BreakerDefinition d = session.World.Breaker.Definition;
            player.Add("breaker", new JsonObject
            {
                { "damage", R(d.Damage) },
                { "interval", R(d.Interval) },
                { "radius", R(d.Radius) },
                { "critChance", R(d.CritChance) },
                { "critDamage", R(d.CritDamage) },
                { "moonDuration", R(d.MoonDuration) },
                { "moonSpeedBonus", R(d.MoonSpeedBonus) },
                { "moonRadiusBonus", R(d.MoonRadiusBonus) },
                { "cometDuration", R(d.CometDuration) },
                { "cometCritDamageBonus", R(d.CometCritDamageBonus) },
                { "planetBonusDamage", R(d.PlanetBonusDamage) },
                { "starBonusDamage", R(d.StarBonusDamage) },
            });

            return player;
        }

        private static JsonObject Enemies(World world, GameContent content)
        {
            var alive = new Dictionary<EnemyType, int>();
            var aliveTraits = new Dictionary<(EnemyType, EnemyTraitType), int>();

            foreach (Enemy enemy in world.Enemies)
            {
                EnemyType type = enemy.Definition.Type;
                alive[type] = Count(alive, type) + 1;

                if (enemy.Trait != null)
                    aliveTraits[(type, enemy.Trait.Type)] = Count(aliveTraits, (type, enemy.Trait.Type)) + 1;
            }

            var enemies = new JsonObject();

            foreach (EnemyDefinition kind in content.Enemies.Enemies)
            {
                EnemyComposition composition = world.Stats.CompositionOf(kind);
                var entry = new JsonObject { { "alive", Count(alive, kind.Type) } };

                if (kind.IsPickup)
                {
                    entry.Add("spawnPeriod", R(kind.SpawnPeriod));
                    entry.Add("spawnChance", R(composition.SpawnChance));
                    entry.Add("rainChance", R(composition.RainChance));
                }
                else
                {
                    entry.Add("maxSize", composition.Size);
                    entry.Add("mass", R(composition.Mass));
                    entry.Add("startSupplyBonus", composition.StartSupplyBonus);
                    entry.Add("growthPercent", R(composition.GrowthPercent));
                    entry.Add("upgradeCount", composition.UpgradeCount);
                    entry.Add("respawnChance", R(composition.RespawnChance));
                    entry.Add("timeChance", R(composition.TimeChance));
                    entry.Add("tiers", Tiers(world.Stats, kind, composition));
                }

                var traits = new List<object>();
                for (int i = 0; i < composition.Traits.Count; i++)
                {
                    EnemyTraitDefinition trait = composition.Traits[i];
                    var item = new JsonObject
                    {
                        { "trait", ContractIds.Of(trait.Type.ToString()) },
                        { "chance", kind.IsPickup ? 1d : R(composition.TraitChances[i]) },
                        { "alive", Count(aliveTraits, (kind.Type, trait.Type)) },
                        { "effect", EffectOf(trait) },
                    };

                    if (trait.MaxActive > 0)
                        item.Add("maxActive", trait.MaxActive);

                    // 성질이 붙은 1등급·크기 1의 수치(황금처럼 보상이 달라지는 성질).
                    if (!kind.IsPickup)
                        item.Add("tier1", StatsOf(world.Stats.Of(kind, 0, trait)));

                    traits.Add(item);
                }

                entry.Add("traits", traits);
                enemies.Add(ContractIds.Of(kind.Type.ToString()), entry);
            }

            return enemies;
        }

        // 색 등급(1부터)마다 이 판의 비율과 크기 1의 HP·Gold·EXP·속도. 크기가 1보다 크게 나오면 가장 큰 크기의 값도 붙인다.
        private static List<object> Tiers(EnemyStatTable stats, EnemyDefinition kind, EnemyComposition composition)
        {
            var tiers = new List<object>();

            for (int tier = 0; tier < composition.TierRatios.Count; tier++)
            {
                if (composition.TierRatios[tier] <= 0)
                    continue;

                JsonObject item = StatsOf(stats.Of(kind, tier));
                item.Add("tier", tier + 1);
                item.Add("ratio", R(composition.TierRatios[tier]));

                if (composition.Size > SizeRule.Base)
                    item.Add("atMaxSize", StatsOf(stats.Of(kind, tier, null, composition.Size)));

                tiers.Add(item);
            }

            return tiers;
        }

        private static JsonObject StatsOf(EnemyStats stats) => new JsonObject
        {
            { "hp", R(stats.MaxHealth) },
            { "gold", stats.Gold },
            { "exp", stats.Exp },
            { "speed", R(stats.MoveSpeed) },
        };

        private static JsonObject Upgrades(UpgradeStatValues upgrades)
        {
            var changed = new JsonObject();

            foreach (UpgradeStatDefinition stat in upgrades.Stats)
            {
                if (upgrades.SumOf(stat.Stat) == 0)
                    continue;

                changed.Add(stat.StatId, new JsonObject
                {
                    { "default", R(stat.DefaultValue) },
                    { "value", R(upgrades.ValueOf(stat.Stat)) },
                    { "unit", stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty },
                });
            }

            return changed;
        }

        // 성질 효과의 수치(노드 반영). 효과마다 이름이 달라 공개 수치 속성을 그대로 읽는다(TestSetupPreview와 같다).
        private static JsonObject EffectOf(EnemyTraitDefinition trait)
        {
            var effect = new JsonObject();

            if (trait.Effect == null)
                return effect;

            foreach (PropertyInfo property in trait.Effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(trait.Effect);

                if (value is float f)
                    effect.Add(property.Name, R(f));
                else if (value is int n)
                    effect.Add(property.Name, n);
            }

            return effect;
        }

        private static int Count<TKey>(Dictionary<TKey, int> map, TKey key) => map.TryGetValue(key, out int count) ? count : 0;

        private static double R(float value) => Math.Round((double)value, 4);
    }
}
#endif
