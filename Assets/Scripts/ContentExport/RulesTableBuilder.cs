using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.ContentExport
{
    // 산 노드 상태(loadout) 하나: 이름과 노드별 Rank. 순서는 상관없다(표에는 노드 배치 순서로 쓴다).
    public sealed class RulesTableLoadout
    {
        public string Name { get; }
        public IReadOnlyList<(string NodeId, int Rank)> Ranks { get; }

        public RulesTableLoadout(string name, IReadOnlyList<(string NodeId, int Rank)> ranks)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Ranks = ranks ?? throw new ArgumentNullException(nameof(ranks));
        }
    }

    // 전투 규칙 기대값 표(계약 1.7a, W4-1a): 서버(Java)가 같은 입력에서 같은 값을 내는지 확인하는 기준이다.
    // 값은 게임이 쓰는 실제 코드로 계산한다 — 진행 상태(ProgressSave) → 수치 값(NodePurchase.StatsFor) → 판 조립(GameSessionFactory.Create)
    // → 판 구성·적 수치(EnemyStatTable), 레벨 계산(Hq), 마일스톤 보상(HqMilestone.RewardFor). 따로 옮겨 쓴 식은 없다.
    //
    // 성장도(stage)와 산 노드(loadout)는 서로 독립이라 따로 담는다(C34). 같은 입력이면 언제나 같은 글자다.
    public static class RulesTableBuilder
    {
        public const int FormatVersion = 1;

        // 같은 입력에서 나온 표 글자. loadout 이름은 겹치면 안 된다.
        public static string Build(GameContent content, NodeTree tree, string contentVersion, IReadOnlyList<RulesTableLoadout> loadouts)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (string.IsNullOrEmpty(contentVersion)) throw new ArgumentException("콘텐츠 버전이 필요하다.", nameof(contentVersion));
            if (loadouts == null) throw new ArgumentNullException(nameof(loadouts));

            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (RulesTableLoadout loadout in loadouts)
            {
                if (!names.Add(loadout.Name))
                    throw new ArgumentException($"loadout 이름이 겹친다: '{loadout.Name}'.", nameof(loadouts));
            }

            HqGrowthDefinition growth = content.Growth;
            var stages = new List<object>();

            for (int stage = HqGrowthDefinition.StartStage; stage <= growth.MaxStage; stage++)
                stages.Add(StageJson(growth, stage));

            var loadoutJson = new List<object>();

            foreach (RulesTableLoadout loadout in loadouts)
                loadoutJson.Add(LoadoutJson(content, tree, loadout));

            return PyJson.Write(new JsonObject
            {
                { "formatVersion", FormatVersion },
                { "contentVersion", contentVersion },
                { "stages", stages },
                { "loadouts", loadoutJson },
                { "arithmetic", ArithmeticJson() },
            }) + "\n";
        }

        // 산술 확인 지점의 입력(계약 1.7a의 arithmetic). 콘텐츠가 아직 쓰지 않는 경우를 덮는다:
        // 정수 .5가 되는 곱(홀수 × 0.5), float 정밀도(2^24)를 넘는 값, 0.1처럼 float로 정확히 나타낼 수 없는 배율.
        // 곱 결과가 long을 넘지 않게 값과 배율의 범위를 잡았다.
        internal static readonly long[] ArithmeticValues =
        {
            0, 1, 2, 3, 5, 7, 99, 100, 12345, 16777215, 16777216, 16777217,
            123456789012, 50000000000, 999999999999999, 9007199254740993,
        };

        internal static readonly float[] ArithmeticFactors =
        {
            0.5f, 0.25f, 0.1f, 0.3f, 0.35f, 1f, 1.5f, 1.7f, 2.5f, 3f, 9.99f, 13f, 32f, 100.5f,
        };

        // [값, 배율, 결과] 쌍. 결과는 클라이언트의 실제 식이 낸다.
        // - multiply: EnemyDefinition.Multiply(value, factor). 크기 배율·황금 배율을 곱하는 식(long × double, .5는 0에서 먼 쪽).
        // - critBonus: DeathRewards.CritBonusOf(gold, scale). 황금 치명 보너스(long × float이라 float 곱셈, C28).
        internal static JsonObject ArithmeticJson()
        {
            var multiply = new List<object>();
            var critBonus = new List<object>();

            foreach (long value in ArithmeticValues)
            {
                foreach (float factor in ArithmeticFactors)
                {
                    multiply.Add(new JsonInline(new List<object> { value, factor, EnemyDefinition.Multiply(value, factor) }));
                    critBonus.Add(new JsonInline(new List<object> { value, factor, DeathRewards.CritBonusOf(value, factor) }));
                }
            }

            return new JsonObject { { "multiply", multiply }, { "critBonus", critBonus } };
        }

        // 진행 상태를 저장 검사(ProgressSave.Load)를 거쳐 만든다. 게임이 저장을 읽는 길과 같다.
        public static ProgressState NewProgress(NodeTree tree, HqGrowthDefinition growth, int growthStage, long gold,
            IReadOnlyList<(string NodeId, int Rank)> ranks)
        {
            var data = new ProgressSaveData
            {
                FormatVersion = ProgressSaveData.CurrentFormatVersion,
                Gold = gold,
                GrowthStage = growthStage,
            };

            foreach ((string nodeId, int rank) in ranks)
                data.Nodes.Add(new NodeRankSaveData { NodeId = nodeId, Rank = rank });

            ProgressLoadResult loaded = ProgressSave.Load(data, tree.Content, growth);

            if (!loaded.Succeeded)
                throw new InvalidOperationException("진행 상태를 만들지 못했다: " + string.Join(" / ", Messages(loaded.Diagnostics)));

            if (loaded.Adjustments.Count > 0)
                throw new InvalidOperationException("콘텐츠와 맞지 않는 노드·Rank가 있다: " + string.Join(" / ", Messages(loaded.Adjustments)));

            var progress = new ProgressState();
            ProgressSave.Restore(progress, loaded.Progress);
            return progress;
        }

        // 구매 순서에서 총 구매 횟수 total의 percent%(내림)만큼.
        public static int PartialCount(int total, int percent)
        {
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));

            return (int)((long)total * percent / 100);
        }

        // 성장도 stage의 levelForExp 확인 지점(누적 EXP, 오름차순, 중복 없음, 0 이상):
        // 시작 EXP − 1, 시작 EXP, 그리고 시작 레벨 위의 모든 레벨의 누적 EXP t에 대해 t − 1, t, t + 1.
        public static IReadOnlyList<long> ProbeExps(HqGrowthDefinition growth, int stage)
        {
            int startLevel = growth.StartLevelAt(stage);
            long startExp = growth.ExpToReach(startLevel) ?? 0;
            var exps = new SortedSet<long> { startExp };

            if (startExp > 0)
                exps.Add(startExp - 1);

            for (int level = startLevel + 1; level <= growth.MaxLevel; level++)
            {
                long threshold = growth.ExpToReach(level).Value;

                for (long exp = threshold - 1; exp <= threshold + 1; exp++)
                {
                    if (exp >= 0)
                        exps.Add(exp);
                }
            }

            return new List<long>(exps);
        }

        // 마일스톤 보상 확인 지점(현재 Gold, 오름차순, 중복 없음, 0 이상): 0, 1, target − 1, target, target + 1, 2 × target.
        public static IReadOnlyList<long> ProbeGolds(long targetGold)
        {
            var golds = new SortedSet<long> { 0, 1, targetGold - 1, targetGold, targetGold + 1 };

            if (targetGold <= long.MaxValue / 2)
                golds.Add(targetGold * 2);

            golds.RemoveWhere(gold => gold < 0);
            return new List<long>(golds);
        }

        private static JsonObject StageJson(HqGrowthDefinition growth, int stage)
        {
            int startLevel = growth.StartLevelAt(stage);
            HqMilestone next = growth.NextMilestoneAt(stage);

            var levelForExp = new List<object>();
            long startExp = growth.ExpToReach(startLevel) ?? 0;

            foreach (long exp in ProbeExps(growth, stage))
            {
                // 판이 시작할 때의 Hq에 누적 EXP가 exp가 되도록 더하고 Level을 올린다(Hq.RaiseLevels).
                var hq = new Hq(growth, 0f, stage);
                hq.AddExp(exp - hq.Exp);
                hq.RaiseLevels();
                levelForExp.Add(new JsonInline(new List<object> { exp, hq.Level }));
            }

            var rewards = new List<object>();

            if (next != null)
            {
                foreach (long gold in ProbeGolds(next.TargetGold))
                    rewards.Add(new JsonInline(new List<object> { gold, next.RewardFor(gold) }));
            }

            return new JsonObject
            {
                { "growthStage", stage },
                { "startLevel", startLevel },
                { "goalLevel", growth.GoalLevelAt(stage) },
                { "galaxyTier", growth.GalaxyTierAt(stage) },
                { "startExp", startExp },
                { "nextMilestone", next == null ? null : new JsonInline(new JsonObject { { "level", next.Level }, { "targetGold", next.TargetGold } }) },
                { "levelForExp", levelForExp },
                { "milestoneRewards", rewards },
            };
        }

        private static JsonObject LoadoutJson(GameContent content, NodeTree tree, RulesTableLoadout loadout)
        {
            ProgressState progress = NewProgress(tree, content.Growth, HqGrowthDefinition.StartStage, 0, loadout.Ranks);
            UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, tree);

            // 성장도는 판 시작 값(시작 레벨 등)에만 들어간다. loadout 값은 성장도 0의 판에서 읽고, 성장도 값은 stages가 맡는다.
            GameSession session = GameSessionFactory.Create(content, progress, 0, upgrades);
            EnemyStatTable stats = session.World.Stats;

            var nodes = new JsonObject();

            foreach (NodeDefinition node in tree.Nodes)
            {
                int rank = progress.RankOf(node.Id);

                if (rank > 0)
                    nodes.Add(node.Id, rank);
            }

            var startSupply = new List<object>();

            foreach (SupplyRequest supply in session.StartInfo.StartSupply)
                startSupply.Add(new JsonObject { { "enemyId", BattleJson.IdOf(supply.Enemy.Type) }, { "count", supply.Count } });

            var kinds = new List<object>();

            foreach (EnemyDefinition kind in stats.Kinds)
                kinds.Add(KindJson(stats, kind));

            return new JsonObject
            {
                { "name", loadout.Name },
                { "nodes", nodes },
                { "stats", StatsJson(content, tree, upgrades) },
                { "timeLimitSeconds", session.StartInfo.TimeLimitSeconds },
                { "growthTimeSeconds", session.World.Hq.GrowthTime },
                { "killTimeBonusSeconds", content.TimeLimit.KillTimeBonus },
                { "startSupply", startSupply },
                { "kinds", kinds },
            };
        }

        // battle.json이 참조하는 모든 statId의 값과 늘어난 양(진단용). 키 순서는 battle.json에서 처음 참조되는 순서다(계약 1.7a).
        private static JsonObject StatsJson(GameContent content, NodeTree tree, UpgradeStatValues upgrades)
        {
            var stats = new JsonObject();
            var seen = new HashSet<UpgradeStat>();

            void Add(UpgradeStat? stat)
            {
                if (!stat.HasValue || !seen.Add(stat.Value))
                    return;

                stats.Add(tree.Content.StatOf(stat.Value).StatId, new JsonInline(new JsonObject
                {
                    { "value", upgrades.ValueOf(stat.Value) },
                    { "gain", upgrades.GainOf(stat.Value) },
                }));
            }

            Add(UpgradeStat.Timer);
            Add(UpgradeStat.GrowthTime);

            foreach (EnemyDefinition kind in content.Enemies.Enemies)
            {
                EnemyType type = kind.Type;
                Add(EnemyUpgradeStats.Mass(type));
                Add(EnemyUpgradeStats.Size(type));
                Add(EnemyUpgradeStats.StartSupply(type));
                Add(EnemyUpgradeStats.GrowthSupply(type));
                Add(EnemyUpgradeStats.Upgrade(type));
                Add(EnemyUpgradeStats.TimeChance(type));

                bool hasGolden = false;

                foreach (EnemyTraitDefinition trait in kind.Traits)
                {
                    Add(EnemyUpgradeStats.TraitChance(type, trait.Type));
                    hasGolden |= trait.Type == EnemyTraitType.Golden;
                }

                if (hasGolden)
                {
                    Add(EnemyUpgradeStats.TraitMultiplier(type, EnemyTraitType.Golden));
                    Add(EnemyUpgradeStats.TraitCritChance(type, EnemyTraitType.Golden));
                    Add(EnemyUpgradeStats.TraitCritRewardScale(type, EnemyTraitType.Golden));
                }
            }

            return stats;
        }

        private static JsonObject KindJson(EnemyStatTable stats, EnemyDefinition kind)
        {
            EnemyComposition composition = stats.CompositionOf(kind);
            int tiers = kind.Tiers.Count;

            var tierPossible = new List<object>();

            for (int tier = 0; tier < tiers; tier++)
                tierPossible.Add(composition.TierRatios[tier] > 0);

            // 이 판에서 황금 성질: 종류에 황금 성질이 있으면 golden은 노드 반영 수치다(생성 확률이 0이어도 있다).
            // 황금 개체가 나올 수 있는 것(goldenPossible)은 생성 확률이 0보다 클 때만이다. 표는 그때만 쓴다.
            EnemyTraitDefinition goldenTrait = null;
            float goldenChance = 0;

            for (int i = 0; i < composition.Traits.Count; i++)
            {
                if (composition.Traits[i].Type == EnemyTraitType.Golden)
                {
                    goldenTrait = composition.Traits[i];
                    goldenChance = composition.TraitChances[i];
                }
            }

            GoldenDefinition golden = goldenTrait?.Effect as GoldenDefinition;
            bool goldenPossible = golden != null && goldenChance > 0;

            var gold = new List<object>();
            var exp = new List<object>();
            var goldGolden = new List<object>();
            var critBonus = new List<object>();

            for (int tier = 0; tier < tiers; tier++)
            {
                var goldRow = new List<object>();
                var expRow = new List<object>();
                var goldenRow = new List<object>();
                var critRow = new List<object>();

                for (int size = SizeRule.Base; size <= composition.Size; size++)
                {
                    EnemyStats plain = stats.Of(kind, tier, null, size);
                    goldRow.Add(plain.Gold);
                    expRow.Add(plain.Exp);

                    if (goldenPossible)
                    {
                        long goldenGold = stats.Of(kind, tier, goldenTrait, size).Gold;
                        goldenRow.Add(goldenGold);
                        critRow.Add(DeathRewards.CritBonusOf(goldenGold, golden.CritRewardScale));
                    }
                }

                gold.Add(new JsonInline(goldRow));
                exp.Add(new JsonInline(expRow));

                if (goldenPossible)
                {
                    goldGolden.Add(new JsonInline(goldenRow));
                    critBonus.Add(new JsonInline(critRow));
                }
            }

            return new JsonObject
            {
                { "enemyId", BattleJson.IdOf(kind.Type) },
                { "pickup", kind.IsPickup },
                { "maxSize", composition.Size },
                { "tierPossible", new JsonInline(tierPossible) },
                { "goldenPossible", goldenPossible },
                { "timeChance", composition.TimeChance },
                { "golden", golden == null ? null : new JsonInline(new JsonObject
                    {
                        { "multiplier", golden.Multiplier },
                        { "critChance", golden.CritChance },
                        { "critRewardScale", golden.CritRewardScale },
                    })
                },
                { "gold", gold },
                { "goldGolden", goldenPossible ? goldGolden : null },
                { "exp", exp },
                { "critBonus", goldenPossible ? critBonus : null },
            };
        }

        private static IEnumerable<string> Messages(IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            foreach (ContentDiagnostic diagnostic in diagnostics)
                yield return diagnostic.ToString();
        }
    }
}
