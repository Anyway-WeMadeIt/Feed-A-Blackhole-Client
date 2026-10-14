using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 진행 상태(ProgressState)와 산 노드의 수치 값(UpgradeStatValues)으로 한 판(GameSession)을 만든다:
    // - 수치 값이 Breaker 수치, 제한 시간, 블랙홀 성장 시간, 적의 판 구성·수치를 정함.
    // - 이 판의 판 구성과 적 수치를 확정하고, Breaker를 만듬.
    //
    // 만든 판은 아직 시작 전이다(GameSession.Begin으로 시작).
    public static class GameSessionFactory
    {
        public static GameSession Create(
            GameContent content,
            ProgressState progress,
            int seed,
            UpgradeStatValues upgrades)
        {
            EnemyContent enemies = content.Enemies;

            // 이 판의 블랙홀: 성장도가 시작 Level(마지막 이정표)과 목표 Level(다음 이정표)을 정한다.
            var hq = new Hq(content.Growth, HqUpgradeStats.GrowthTimeFrom(upgrades), progress.GrowthStage);

            // 전투 Session이 시작되기 전,
            // 적의 수치(Gold 포함)와 색·크기·성질 비율을 결정해둠.
            // 모두 수치 값으로만 정해진다. 성장도는 블랙홀(시작·목표 Level)에만 들어간다.
            var stats = new EnemyStatTable(
                enemies.Enemies,
                CompositionsOf(enemies, upgrades));

            // 전투 시작 공급(변환 반영). Level업마다의 성장 공급은 이 시작 수로 정해진다.
            IReadOnlyList<SupplyRequest> startSupply = StartSupplyOf(enemies, stats);

            // 출현 띠(혜성 띠 포함)는 이 판의 전장 배율만큼 넓힌다: 이정표마다 카메라와 함께 넓어진다.
            var world = new World(
                seed,
                stats,
                enemies.EnemyPlacement.Scaled(hq.FieldScale),
                enemies.PeriodicSpawnPlacement.Scaled(hq.FieldScale),
                hq,
                content.Breaker.Upgraded(upgrades),
                GrowthSupplyOf(startSupply, stats),
                content.TimeLimit.KillTimeBonus,
                content.TimeLimit.KillTimeCooldown);

            TimeLimitRule timeLimit = new(content.TimeLimit.Upgraded(upgrades));

            return new GameSession(
                world,
                timeLimit,
                seed,
                progress,
                startSupply,
                new RunStartInfo(seed, progress.GrowthStage, hq.StartLevel, timeLimit.Definition.Duration, startSupply));
        }

        // 적 종류마다의 판 구성.
        private static Dictionary<EnemyDefinition, EnemyComposition> CompositionsOf(
            EnemyContent enemies,
            UpgradeStatValues upgrades)
        {
            var compositions = new Dictionary<EnemyDefinition, EnemyComposition>();

            foreach (EnemyDefinition kind in enemies.Enemies)
                compositions.Add(kind, EnemyComposition.From(kind, upgrades));

            return compositions;
        }

        // 최종 전투 시작 공급(종류마다 하나, 처음 나온 종류 순서):
        // 1. 콘텐츠의 시작 적 목록을 종류마다 합치고, 업그레이드로 추가된 시작 적 수를 더한다(콘텐츠에 없는 종류는 뒤에 붙는다).
        // 2. 변환 사슬 앞쪽부터(소행성 → 행성을 먼저, 그다음 행성 → 별) 종류마다 변환 수만큼 다음 종류로 옮긴다.
        //    가진 수보다 많이 옮기지 않는다. 그래서 행성 → 별은 소행성에서 바뀐 행성까지 센다.
        // 수가 0이 된 종류는 빠진다.
        private static IReadOnlyList<SupplyRequest> StartSupplyOf(
            EnemyContent enemies,
            EnemyStatTable stats)
        {
            var order = new List<EnemyDefinition>();
            var counts = new Dictionary<EnemyDefinition, int>();

            void Add(EnemyDefinition kind, int count)
            {
                if (!counts.TryGetValue(kind, out int have))
                    order.Add(kind);

                counts[kind] = checked(have + count);
            }

            foreach (SupplyRequest request in enemies.StartSupply)
                Add(request.Enemy, request.Count);

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                int bonus = stats.CompositionOf(kind).StartSupplyBonus;

                if (bonus > 0)
                    Add(kind, bonus);
            }

            foreach (EnemyDefinition kind in ChainOrder(stats))
            {
                int have = counts.TryGetValue(kind, out int count) ? count : 0;
                int moved = Math.Min(have, stats.CompositionOf(kind).UpgradeCount);

                if (moved <= 0)
                    continue;

                counts[kind] = have - moved;
                Add(stats.UpgradeTargetOf(kind), moved);
            }

            var supply = new List<SupplyRequest>();

            foreach (EnemyDefinition kind in order)
            {
                if (counts[kind] > 0)
                    supply.Add(new SupplyRequest(kind, counts[kind]));
            }

            return supply.AsReadOnly();
        }

        // 변환 사슬에서 앞 종류가 먼저 오는 순서(콘텐츠 종류 순서를 지키는 위상 정렬). 사슬이 도는 콘텐츠는 로드가 막는다(EnemyContentInvariants).
        private static List<EnemyDefinition> ChainOrder(EnemyStatTable stats)
        {
            var incoming = new Dictionary<EnemyDefinition, int>();

            foreach (EnemyDefinition kind in stats.Kinds)
                incoming[kind] = 0;

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                EnemyDefinition target = stats.UpgradeTargetOf(kind);

                if (target != null)
                    incoming[target]++;
            }

            var ready = new Queue<EnemyDefinition>();

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                if (incoming[kind] == 0)
                    ready.Enqueue(kind);
            }

            var order = new List<EnemyDefinition>();

            while (ready.Count > 0)
            {
                EnemyDefinition kind = ready.Dequeue();
                order.Add(kind);
                EnemyDefinition target = stats.UpgradeTargetOf(kind);

                if (target != null && --incoming[target] == 0)
                    ready.Enqueue(target);
            }

            return order;
        }

        // Level업 한 번의 성장 공급: 종류마다 이 판 시작 수(변환 반영) × 성장 공급 %(반올림, .5는 올림). 0마리인 종류는 빠진다.
        // 시작 수가 0인 종류(예: 소행성 → 행성 변환이 없는 판의 행성)는 성장 공급 %가 있어도 나오지 않는다.
        private static IReadOnlyList<SupplyRequest> GrowthSupplyOf(
            IReadOnlyList<SupplyRequest> startSupply,
            EnemyStatTable stats)
        {
            var supply = new List<SupplyRequest>();

            foreach (SupplyRequest start in startSupply)
            {
                double count = start.Count * (double)stats.CompositionOf(start.Enemy).GrowthPercent / 100;
                int whole = (int)Math.Round(count, MidpointRounding.AwayFromZero);

                if (whole > 0)
                    supply.Add(new SupplyRequest(start.Enemy, whole));
            }

            return supply.AsReadOnly();
        }
    }
}
