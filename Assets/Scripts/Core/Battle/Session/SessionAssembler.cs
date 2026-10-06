using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 진행 상태는 방장의 것 하나다:
    // - 방장이 노드를 사고(Gold·산 노드), 그 결과가 판 전체에 반영됨.
    // 스탯과 처치 버프는 판 안의 모든 참가자가 함께 받음.
    //
    // 조립한 판은 준비 단계(Preparing)다:
    // - 이 판의 판 구성과 적 수치를 확정하고, 판 안의 참가자(조준점·스킬)를 만듬.
    public static class SessionAssembler
    {
        public const int DefaultSeed = 0;

        public static GameSession CreateBattle(GameContent content, PlayerState progress) =>
            CreateBattle(content, progress, DefaultSeed);

        public static GameSession CreateBattle(GameContent content, PlayerState progress, int seed, UpgradeTable upgrades = null)
        {
            UpgradeTable table = upgrades ?? new UpgradeTable(Array.Empty<Upgrade>());
            EnemyContent enemies = content.Enemies;

            var battlePlayers = new List<BattlePlayer>
            {
                new BattlePlayer(
                    progress.Id,
                    content.Breaker?.Upgraded(table),
                    seed),
            };

            // 이 판의 블랙홀: 성장도가 시작 Level(마지막 이정표)과 목표 Level(다음 이정표)을 정한다.
            var hq = new Hq(content.Growth, HqUpgradeStats.GrowthTimeFrom(table), progress.GrowthStage);

            // 전투 Session이 시작되기 전,
            // 적의 수치(Gold 포함)와 색·크기·성질 비율을 결정해둠.
            // 모두 업그레이드 표로만 정해진다. 성장도는 블랙홀(시작·목표 Level)에만 들어간다.
            var stats = new EnemyStatTable(
                enemies.Enemies,
                CompositionsOf(enemies, table));

            // 출현 띠(혜성 띠 포함)는 이 판의 전장 배율만큼 넓힌다: 이정표마다 카메라와 함께 넓어진다.
            var world = new World(
                seed,
                stats,
                enemies.EnemyPlacement?.Scaled(hq.FieldScale),
                enemies.PickupPlacement?.Scaled(hq.FieldScale),
                enemies.MaxAliveEnemies,
                hq,
                battlePlayers);

            return new GameSession(
                world,
                new TimeLimitRule(content.TimeLimit),
                seed,
                progress,
                table,
                StartSupplyOf(enemies, stats));
        }

        // 적 종류마다의 판 구성.
        private static Dictionary<EnemyDefinition, EnemyComposition> CompositionsOf(
            EnemyContent enemies,
            UpgradeTable upgrades)
        {
            var compositions = new Dictionary<EnemyDefinition, EnemyComposition>();

            foreach (EnemyDefinition kind in enemies.Enemies)
                compositions.Add(kind, EnemyComposition.From(kind, upgrades));

            return compositions;
        }

        // 기본 시작 적 목록에,
        // 업그레이드로 추가된 시작 적 수를 합쳐서,
        // 최종 시작시 등장 할 적 목록을 만듬.
        private static IReadOnlyList<SupplyRequest> StartSupplyOf(
            EnemyContent enemies,
            EnemyStatTable stats)
        {
            var supply = new List<SupplyRequest>();
            var bonused = new HashSet<EnemyDefinition>();

            foreach (SupplyRequest request in enemies.StartSupply)
            {
                int bonus = stats.CompositionOf(request.Enemy).StartSupplyBonus;

                if (bonus > 0 && bonused.Add(request.Enemy))
                    supply.Add(new SupplyRequest(request.Enemy, request.Count + bonus));
                else
                    supply.Add(request);
            }

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                EnemyComposition composition = stats.CompositionOf(kind);
                int bonus = composition.StartSupplyBonus;

                if (bonus > 0 && bonused.Add(kind))
                    supply.Add(new SupplyRequest(kind, bonus));
            }

            return supply.AsReadOnly();
        }
    }
}
