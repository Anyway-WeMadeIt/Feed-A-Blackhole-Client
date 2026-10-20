using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 스탯:
    // - 종류 별 구성(질량과 색 비율·크기·성질 확률·성질 수치). 모두 산 노드의 수치 값으로만 정해진다(블랙홀 성장도와 무관).
    // - (종류, 성질, 크기, 색 등급)마다의 실행 수치.
    //
    // 전투 Session이 시작되기 전, 미리 값을 한 번 정하여 사용.
    // 출현하는 모든 적은 이 표의 수치를 받는다.
    public sealed class EnemyStatTable
    {
        private readonly Dictionary<EnemyDefinition, Row> _rows = new();
        private readonly Dictionary<EnemyDefinition, EnemyDefinition> _upgradeTargets = new();

        public IReadOnlyList<EnemyDefinition> Kinds { get; }

        internal EnemyStatTable(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyDictionary<EnemyDefinition, EnemyComposition> compositions)
        {
            var kinds = new EnemyDefinition[enemies.Count];

            for (int i = 0; i < kinds.Length; i++)
            {
                EnemyDefinition kind = enemies[i];
                EnemyComposition composition = compositions != null && compositions.TryGetValue(kind, out EnemyComposition chosen)
                    ? chosen
                    : EnemyComposition.Base(kind);

                if (composition.Traits.Count != kind.Traits.Count)
                    throw new ArgumentException($"'{kind.Type}'의 판 구성 성질 수가 종류의 성질 수와 다르다.", nameof(compositions));

                if (composition.TierRatios.Count != kind.Tiers.Count)
                    throw new ArgumentException($"'{kind.Type}'의 판 구성 색 비율 수가 종류의 색 등급 수와 다르다.", nameof(compositions));

                if (composition.TraitChanceSum > 1 + 1e-4f)
                    throw new ArgumentException($"'{kind.Type}'의 성질 확률 합이 100%를 넘는다({composition.TraitChanceSum * 100:0.##}%).", nameof(compositions));

                // [성질 칸][크기 − 1][색 등급]. 성질 칸 0은 성질 없음, i + 1은 판 구성의 성질 i다.
                // 크기는 이 판에 열린 것(1 ~ Size)만 있다.
                var stats = new EnemyStats[composition.Traits.Count + 1][][];

                for (int slot = 0; slot < stats.Length; slot++)
                {
                    EnemyTraitDefinition trait = slot == 0 ? null : composition.Traits[slot - 1];
                    stats[slot] = new EnemyStats[composition.Size][];

                    for (int size = SizeRule.Base; size <= composition.Size; size++)
                    {
                        EnemyStats[] row = stats[slot][size - SizeRule.Base] = new EnemyStats[kind.Tiers.Count];

                        for (int tier = 0; tier < kind.Tiers.Count; tier++)
                            row[tier] = kind.StatsAt(composition, tier, trait, size);
                    }
                }

                _rows.Add(kind, new Row(composition, stats));
                kinds[i] = kind;
            }

            if (compositions != null)
            {
                foreach (EnemyDefinition kind in compositions.Keys)
                {
                    if (kind == null || !_rows.ContainsKey(kind))
                        throw new ArgumentException($"이 판의 적 종류가 아니다: '{kind?.Type}'.", nameof(compositions));
                }
            }

            Kinds = Array.AsReadOnly(kinds);
            LinkKinds(kinds);
        }

        // 판 시작 때 이 종류의 변환 수만큼 바뀌어 나오는 다음 종류(변환 사슬). 없으면 null이다.
        public EnemyDefinition UpgradeTargetOf(EnemyDefinition kind)
        {
            Require(kind);
            return _upgradeTargets.TryGetValue(kind, out EnemyDefinition target) ? target : null;
        }

        private void LinkKinds(EnemyDefinition[] kinds)
        {
            var byType = new Dictionary<EnemyType, EnemyDefinition>();

            foreach (EnemyDefinition kind in kinds)
                byType[kind.Type] = kind;

            foreach (EnemyDefinition kind in kinds)
            {
                if (!kind.UpgradesTo.HasValue)
                    continue;

                if (!byType.TryGetValue(kind.UpgradesTo.Value, out EnemyDefinition target))
                    throw new ArgumentException($"'{kind.Type}'가 가리키는 종류 '{kind.UpgradesTo.Value}'가 이 판에 없다.");

                _upgradeTargets.Add(kind, target);
            }
        }

        // 이 판에서 이 종류의 판 구성(질량·색 비율·크기·공급·변환·성질 확률·성질 수치·등장 확률).
        public EnemyComposition CompositionOf(EnemyDefinition kind) => RowOf(kind).Composition;

        // 이 판에서 이 종류의 색 비율(색 등급 표 순서). 그 종류의 질량으로 정해진다.
        public IReadOnlyList<float> TierRatiosOf(EnemyDefinition kind) => RowOf(kind).Composition.TierRatios;

        // 이 판에서 이 종류·색 등급·크기(1부터)·성질(없으면 null)이 받는 수치. 성질은 이 판 구성의 것(CompositionOf(kind).Traits)이다.
        public EnemyStats Of(EnemyDefinition kind, int tier, EnemyTraitDefinition trait = null, int size = SizeRule.Base)
        {
            Row row = RowOf(kind);
            int slot = 0;

            if (trait != null)
            {
                int index = row.Composition.IndexOfTrait(trait);

                if (index < 0)
                    throw new ArgumentException($"'{trait.Type}'는 이 판에서 '{kind.Type}'의 성질이 아니다.", nameof(trait));

                slot = index + 1;
            }

            EnemyStats[][] stats = row.Stats[slot];
            int sizeIndex = size - SizeRule.Base;

            if (sizeIndex < 0 || sizeIndex >= stats.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(size), $"이 판에서 '{kind.Type}'의 크기는 {SizeRule.Base}부터 {stats.Length}까지다. 받은 값: {size}.");

            if (tier < 0 || tier >= stats[sizeIndex].Length)
                throw new ArgumentOutOfRangeException(nameof(tier), $"'{kind.Type}'의 색 등급은 0부터 {stats[sizeIndex].Length - 1}까지다. 받은 값: {tier}.");

            return stats[sizeIndex][tier];
        }

        // 이 판의 종류가 아니면 예외다.
        internal void Require(EnemyDefinition kind) => RowOf(kind);

        private Row RowOf(EnemyDefinition kind)
        {
            if (kind == null || !_rows.TryGetValue(kind, out Row row))
                throw new ArgumentException($"이 판의 적 종류가 아니다: '{kind?.Type}'.", nameof(kind));

            return row;
        }

        private readonly struct Row
        {
            public readonly EnemyComposition Composition;
            // [성질 칸][크기 − 1][색 등급].
            public readonly EnemyStats[][][] Stats;

            public Row(EnemyComposition composition, EnemyStats[][][] stats)
            {
                Composition = composition;
                Stats = stats;
            }
        }
    }
}
