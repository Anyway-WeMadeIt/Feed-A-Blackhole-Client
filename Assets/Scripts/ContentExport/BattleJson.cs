using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.ContentExport
{
    // battle/battle.json(계약 1.4): 서버 결산 검증에 필요한 전투 콘텐츠만 쓴다.
    // HP, 이동 속도, 반지름, 배치, 블랙홀 공격 수치, 황금 외 사망 효과는 쓰지 않는다.
    internal static class BattleJson
    {
        public static string Write(ContentData content, NodeContent nodes)
        {
            string StatId(UpgradeStat? stat) => stat.HasValue ? nodes.StatOf(stat.Value).StatId : null;

            var enemies = new List<object>();

            foreach (EnemyData enemy in content.Enemies.Enemies)
                enemies.Add(Enemy(enemy, StatId));

            var levelExp = new List<object>();

            foreach (long exp in content.Growth.LevelExp)
                levelExp.Add(exp);

            var milestones = new List<object>();

            foreach (HqMilestoneData milestone in content.Growth.Milestones)
                milestones.Add(new JsonObject { { "level", milestone.Level }, { "targetGold", milestone.TargetGold } });

            var startSupply = new List<object>();

            foreach (SupplyData supply in content.Enemies.StartSupply)
            {
                if (!supply.Enemy.HasValue)
                    throw new InvalidOperationException("전투 시작 공급에 종류가 비어 있는 칸이 있다.");

                startSupply.Add(new JsonObject { { "enemyId", IdOf(supply.Enemy.Value) }, { "count", supply.Count } });
            }

            return PyJson.Write(new JsonObject
            {
                { "rules", new JsonObject
                    {
                        { "timeLimitSeconds", content.BattleRules.TimeLimit },
                        { "killTimeBonusSeconds", content.BattleRules.KillTimeBonus },
                    }
                },
                { "growth", new JsonObject { { "levelExp", levelExp }, { "milestones", milestones }, { "galaxyStage", content.Growth.GalaxyStage } } },
                { "stats", new JsonObject
                    {
                        { "timer", StatId(UpgradeStat.Timer) },
                        { "growthTime", StatId(UpgradeStat.GrowthTime) },
                    }
                },
                { "enemies", enemies },
                { "startSupply", startSupply },
            }) + "\n";
        }

        // 소문자 종류·성질 id(계약 1.4).
        public static string IdOf(EnemyType type) => type.ToString().ToLowerInvariant();

        public static string IdOf(EnemyTraitType type) => type.ToString().ToLowerInvariant();

        private static JsonObject Enemy(EnemyData enemy, Func<UpgradeStat?, string> statId)
        {
            EnemyType type = enemy.Type;

            var tiers = new List<object>();

            foreach (EnemyTierData tier in enemy.Tiers)
                tiers.Add(new JsonObject { { "gold", tier.Gold }, { "exp", tier.Exp } });

            var traits = new List<object>();
            var traitChance = new JsonObject();
            JsonObject golden = null;

            foreach (EnemyTraitData trait in enemy.Traits)
            {
                string traitId = IdOf(trait.Type);
                traits.Add(traitId);

                string chance = statId(EnemyUpgradeStats.TraitChance(type, trait.Type));

                if (chance != null)
                    traitChance.Add(traitId, chance);

                if (trait.Type == EnemyTraitType.Golden)
                {
                    golden = new JsonObject
                    {
                        { "multiplier", trait.Effect.Multiplier },
                        { "critChance", trait.Effect.CritChance },
                        { "critRewardScale", trait.Effect.CritRewardScale },
                    };
                }
            }

            bool hasGolden = golden != null;

            return new JsonObject
            {
                { "enemyId", IdOf(type) },
                { "pickup", enemy.SpawnPeriod > 0 },
                { "upgradesTo", enemy.UpgradesTo.HasValue ? IdOf(enemy.UpgradesTo.Value) : null },
                { "tiers", tiers },
                { "traits", traits },
                { "golden", golden },
                { "stats", new JsonObject
                    {
                        { "mass", statId(EnemyUpgradeStats.Mass(type)) },
                        { "size", statId(EnemyUpgradeStats.Size(type)) },
                        { "startSupply", statId(EnemyUpgradeStats.StartSupply(type)) },
                        { "growthSupply", statId(EnemyUpgradeStats.GrowthSupply(type)) },
                        { "upgrade", statId(EnemyUpgradeStats.Upgrade(type)) },
                        { "timeChance", statId(EnemyUpgradeStats.TimeChance(type)) },
                        { "traitChance", traitChance },
                        { "goldenMultiplier", hasGolden ? statId(EnemyUpgradeStats.TraitMultiplier(type, EnemyTraitType.Golden)) : null },
                        { "goldenCritChance", hasGolden ? statId(EnemyUpgradeStats.TraitCritChance(type, EnemyTraitType.Golden)) : null },
                        { "goldenCritRewardScale", hasGolden ? statId(EnemyUpgradeStats.TraitCritRewardScale(type, EnemyTraitType.Golden)) : null },
                    }
                },
            };
        }
    }
}
