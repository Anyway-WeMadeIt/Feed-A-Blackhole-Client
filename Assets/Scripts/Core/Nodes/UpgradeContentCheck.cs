using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드를 모두 산 경우(배치된 모든 노드를 마지막 Rank까지)에도 판을 조립할 수 있는가.
    // 모두 산 수치 값으로 판 조립의 규칙을 미리 돌려 본다.
    public static class UpgradeContentCheck
    {
        public static IReadOnlyList<ContentDiagnostic> Check(GameContent content, NodeTree nodes)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            var diagnostics = new List<ContentDiagnostic>();
            UpgradeStatValues upgrades = NodePurchase.StatsFor(nodes, node => node.MaxRank);

            try
            {
                content.Breaker.Upgraded(upgrades);
            }
            catch (ArgumentException error)
            {
                diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Breaker", error.Message));
            }

            try
            {
                HqUpgradeStats.GrowthTimeFrom(upgrades);
            }
            catch (ArgumentException error)
            {
                diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Hq", error.Message));
            }

            try
            {
                content.TimeLimit.Upgraded(upgrades);
            }
            catch (ArgumentException error)
            {
                diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).BattleRules", error.Message));
            }

            EnemyContent enemies = content.Enemies;
            var compositions = new Dictionary<EnemyDefinition, EnemyComposition>();

            foreach (EnemyDefinition kind in enemies.Enemies)
            {
                try
                {
                    compositions.Add(kind, EnemyComposition.From(kind, upgrades));
                }
                catch (ArgumentException error)
                {
                    diagnostics.Add(new ContentDiagnostic($"Nodes(모두 산 경우).Enemies[{kind.Type}]", error.Message));
                }
            }

            // 판 구성끼리 맞물리는 규칙(변환 대상이 판에 있는가 등)은 적 수치 표가 본다. 종류마다의 판 구성이 모두 계산될 때만 본다.
            if (compositions.Count == enemies.Enemies.Count)
            {
                try
                {
                    new EnemyStatTable(enemies.Enemies, compositions);
                }
                catch (ArgumentException error)
                {
                    diagnostics.Add(new ContentDiagnostic("Nodes(모두 산 경우).Enemies", error.Message));
                }
            }

            return diagnostics.AsReadOnly();
        }
    }
}
