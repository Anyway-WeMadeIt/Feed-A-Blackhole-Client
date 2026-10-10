#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 테스트 세팅의 확정 정보. 노드 Rank와 이정표 단계(성장도)만으로 판의 모든 수치가 정해진다:
    // - 노드 → 수치 값(UpgradeStatValues) → Breaker·제한 시간·적 구성(크기·색 비율·성질과 확률·공급)
    // - 이정표 단계 → 블랙홀의 시작·목표 Level, 전장 배율, 이정표 목표 Gold
    // - 시작 Level은 판 안의 진행이다. 원래 길(EXP → Level업 → 성장 공급·성장 시간)로 채운다.
    // 게임과 같은 길(ScenarioRunner → NodePurchase.StatsFor → GameSessionFactory → Begin → 첫 Step)로 판을 조립해 그 결과를 읽는다.
    // 따로 계산하지 않으므로 창에 보이는 값이 곧 판의 값이다.
    internal static class TestSetupPreview
    {
        // 시드가 0(매번 다름)인 세팅을 미리볼 때 쓰는 시드. 판 시작의 적 수는 시드와 관계없고 위치만 바뀐다.
        public const int PreviewSeed = 1;
        // 시작 Level을 채운 뒤 Level업(성장 공급·성장 시간)이 일어나도록 진행하는 아주 짧은 Step(초).
        private const float FirstStep = 0.001f;

        public static TestSetupReport Build(PlaytestScenario setup, GameContent content, NodeTree tree)
        {
            var report = new TestSetupReport();
            var progress = new ProgressState();

            if (!ScenarioRunner.TryApply(setup, progress, tree, content.Growth, report.Errors, report.Warnings))
                return report;

            report.Progress = progress;
            SummarizeNodes(report, progress, tree);

            UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, tree);
            GameSession session = GameSessionFactory.Create(content, progress, setup.seed != 0 ? setup.seed : PreviewSeed, upgrades);
            session.Begin();
            session.SetAimPoint(null);

            if (setup.startLevel > session.World.Hq.Level)
            {
                try
                {
                    BattleCheats.ReachLevel(session, setup.startLevel);
                }
                catch (ArgumentException error)
                {
                    report.Warnings.Add($"시작 Level {setup.startLevel}: {error.Message}");
                }
            }

            session.Advance(FirstStep);

            AddHq(report, session, content, upgrades);
            AddBreaker(report, session.World.Breaker);
            AddEnemies(report, session.World, content);
            AddStats(report, upgrades);
            report.Stats = NoteStats.Capture(session, content, upgrades, NoteStats.AtPreview);
            return report;
        }

        // 산 노드 수·Rank 합·총 비용, 그리고 게임 순서로는 살 수 없는 노드(시작 노드와 산 노드로 이어지지 않음).
        private static void SummarizeNodes(TestSetupReport report, ProgressState progress, NodeTree tree)
        {
            var neighbors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach ((string a, string b) in tree.Graph.Links)
            {
                Neighbors(neighbors, a).Add(b);
                Neighbors(neighbors, b).Add(a);
            }

            var reached = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();

            foreach (NodeDefinition node in tree.Nodes)
            {
                int rank = progress.RankOf(node.Id);

                if (rank <= 0)
                    continue;

                report.OwnedNodes++;
                report.OwnedRanks += rank;

                for (int r = 1; r <= rank && r <= node.MaxRank; r++)
                    report.TotalCost += node.RankAt(r).Cost;

                if (tree.Graph.IsStart(node.Id) && reached.Add(node.Id))
                    queue.Enqueue(node.Id);
            }

            while (queue.Count > 0)
            {
                string id = queue.Dequeue();

                if (!neighbors.TryGetValue(id, out List<string> next))
                    continue;

                foreach (string other in next)
                {
                    if (progress.Owns(other) && reached.Add(other))
                        queue.Enqueue(other);
                }
            }

            foreach (NodeDefinition node in tree.Nodes)
            {
                if (progress.Owns(node.Id) && !reached.Contains(node.Id))
                    report.Unreachable.Add(node.Id);
            }

            if (report.Unreachable.Count > 0)
                report.Warnings.Add($"게임 순서로는 살 수 없는 노드 {report.Unreachable.Count}개(시작 노드에서 산 노드로 이어지지 않는다): {string.Join(", ", report.Unreachable)}");
        }

        private static List<string> Neighbors(Dictionary<string, List<string>> map, string id)
        {
            if (!map.TryGetValue(id, out List<string> list))
            {
                list = new List<string>();
                map.Add(id, list);
            }

            return list;
        }

        private static void AddHq(TestSetupReport report, GameSession session, GameContent content, UpgradeStatValues upgrades)
        {
            Hq hq = session.World.Hq;
            HqGrowthDefinition growth = hq.Growth;
            TimeLimitDefinition limit = content.TimeLimit.Upgraded(upgrades);
            int raised = hq.Level - hq.StartLevel;
            float start = session.Remaining + session.Elapsed;

            TestSetupReport.Section section = report.Add("블랙홀");
            section.Row("이정표 단계", $"{hq.Stage} / {growth.MaxStage}");
            section.Row("Level", hq.GoalLevel != HqGrowthDefinition.NoGoal
                ? $"{hq.Level} (단계 시작 {hq.StartLevel}, 목표 {hq.GoalLevel})"
                : $"{hq.Level} (단계 시작 {hq.StartLevel}, 목표 없음)");

            HqMilestone next = growth.NextMilestoneAt(hq.Stage);
            if (next != null)
                section.Row("이정표 목표 Gold", next.TargetGold.ToString("N0", CultureInfo.InvariantCulture));

            section.Row("전장 배율", $"×{Number(hq.FieldScale)}");
            section.Row("판 시작 제한 시간", raised > 0 && hq.GrowthTime > 0
                ? $"{Number(start)}초 (기본 {Number(limit.Duration)} + Level업 {raised}회 × 성장 시간 {Number(hq.GrowthTime)})"
                : $"{Number(start)}초");
            section.Row("처치 추가 시간", $"{Number(limit.KillTimeBonus)}초");
            section.Row("Level업 성장 시간", $"{Number(hq.GrowthTime)}초");
        }

        private static void AddBreaker(TestSetupReport report, BreakerSkill breaker)
        {
            BreakerDefinition d = breaker.Definition;
            TestSetupReport.Section section = report.Add("Breaker");
            section.Row("피해", Number(d.Damage));
            section.Row("공격 주기", $"{Number(d.Interval)}초");
            section.Row("반경", Number(d.Radius));
            section.Row("치명 확률", Percent(d.CritChance));
            section.Row("치명 배율", $"×{Number(d.CritDamage)}");
            section.Row("달 버프", $"{Number(d.MoonDuration)}초 · 속도 +{Percent(d.MoonSpeedBonus)} · 반경 +{Percent(d.MoonRadiusBonus)}");
            section.Row("혜성 버프", $"{Number(d.CometDuration)}초 · 치명 피해 +{Percent(d.CometCritDamageBonus)}");
            section.Row("행성·별 보너스 피해", $"{Number(d.PlanetBonusDamage)} · {Number(d.StarBonusDamage)}");
        }

        private static void AddEnemies(TestSetupReport report, World world, GameContent content)
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

            foreach (EnemyDefinition kind in content.Enemies.Enemies)
            {
                EnemyComposition composition = world.Stats.CompositionOf(kind);
                TestSetupReport.Section section = report.Add($"적 · {PlaytestNames.Of(kind.Type)}");

                var startCount = $"{Count(alive, kind.Type)}마리";
                var traitParts = new List<string>();
                foreach (EnemyTraitDefinition trait in composition.Traits)
                {
                    int count = Count(aliveTraits, (kind.Type, trait.Type));
                    if (count > 0)
                        traitParts.Add($"{PlaytestNames.Of(trait.Type)} {count}");
                }
                section.Row("판 시작에 있는 수", traitParts.Count > 0 ? $"{startCount} ({string.Join(", ", traitParts)})" : startCount);

                if (kind.IsPickup)
                {
                    section.Row("출현", $"{Number(kind.SpawnPeriod)}초마다 {Percent(composition.SpawnChance)} · 비 {Percent(composition.RainChance)}");
                }
                else
                {
                    section.Row("크기", composition.Size > SizeRule.Base ? $"{SizeRule.Base} ~ {composition.Size}" : $"{SizeRule.Base}");
                    section.Row("질량", $"{Number(composition.Mass)}%");
                    section.Row("색 등급 비율", TierRatios(composition.TierRatios));
                    if (composition.StartSupplyBonus > 0)
                        section.Row("시작 공급 추가", $"+{composition.StartSupplyBonus}");
                    if (composition.GrowthPercent > 0)
                        section.Row("Level업 성장 공급", $"시작 수의 {Number(composition.GrowthPercent)}%");
                    if (composition.UpgradeCount > 0)
                        section.Row("시작 때 다음 종류로 승격", $"{composition.UpgradeCount}마리");

                    EnemyStats first = world.Stats.Of(kind, 0);
                    section.Row("1등급·크기 1", $"HP {Number(first.MaxHealth)} · Gold {first.Gold:N0} · EXP {first.Exp:N0} · 속도 {Number(first.MoveSpeed)}");
                }

                for (int i = 0; i < composition.Traits.Count; i++)
                {
                    EnemyTraitDefinition trait = composition.Traits[i];
                    string chance = kind.IsPickup ? "언제나" : Percent(composition.TraitChances[i]);
                    section.Row($"성질 · {PlaytestNames.Of(trait.Type)}", $"{chance} · {EffectOf(trait)}");
                }
            }
        }

        private static void AddStats(TestSetupReport report, UpgradeStatValues upgrades)
        {
            TestSetupReport.Section section = report.Add("바뀐 수치 (기본 → 지금)");

            foreach (UpgradeStatDefinition stat in upgrades.Stats)
            {
                if (upgrades.SumOf(stat.Stat) == 0)
                    continue;

                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                section.Row(stat.StatId, $"{Number(stat.DefaultValue)}{unit} → {Number(upgrades.ValueOf(stat.Stat))}{unit}");
            }

            if (section.Rows.Count == 0)
                section.Row("없음", "산 노드가 없다");
        }

        // 성질 효과의 수치(노드 반영). 효과마다 이름이 달라 공개 수치 속성을 그대로 읽는다.
        private static string EffectOf(EnemyTraitDefinition trait)
        {
            var parts = new List<string>();

            foreach (PropertyInfo property in trait.Effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(trait.Effect);

                if (value is float f)
                    parts.Add($"{property.Name} {Number(f)}");
                else if (value is int n)
                    parts.Add($"{property.Name} {n}");
            }

            if (trait.MaxActive > 0)
                parts.Add($"동시 {trait.MaxActive}");

            return parts.Count > 0 ? string.Join(" · ", parts) : "-";
        }

        private static string TierRatios(IReadOnlyList<float> ratios)
        {
            var parts = new List<string>();

            for (int i = 0; i < ratios.Count; i++)
            {
                if (ratios[i] > 0)
                    parts.Add($"{i + 1}: {Percent(ratios[i])}");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "-";
        }

        private static int Count<TKey>(Dictionary<TKey, int> map, TKey key) => map.TryGetValue(key, out int count) ? count : 0;

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Percent(float fraction) => (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    // 확정 정보: 제목 있는 칸(항목·값)의 목록, 세팅을 넣지 못한 이유(Errors), 알아 둘 것(Warnings).
    internal sealed class TestSetupReport
    {
        public List<Section> Sections { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        // 게임 순서로는 살 수 없는 산 노드.
        public List<string> Unreachable { get; } = new();
        // 세팅을 넣은 진행 상태(자동 구매를 풀어 정확한 Rank가 된다). 넣지 못했으면 null.
        public ProgressState Progress { get; set; }
        public int OwnedNodes { get; set; }
        public int OwnedRanks { get; set; }
        public long TotalCost { get; set; }
        // 메모에 남기는 계산된 수치(NoteStats). 세팅을 넣지 못했으면 null.
        public JsonObject Stats { get; set; }

        public bool Succeeded => Progress != null;

        public Section Add(string title)
        {
            var section = new Section(title);
            Sections.Add(section);
            return section;
        }

        public sealed class Section
        {
            public string Title { get; }
            public List<(string Label, string Value)> Rows { get; } = new();

            public Section(string title) => Title = title;

            public void Row(string label, string value) => Rows.Add((label, value));
        }
    }
}
#endif
