using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 수치마다 기본값 + 산 효과 값의 합을 Min·Max로 자른 값. 시트 단위 그대로다(Percent면 125 = 125%).
    // 모든 수치(UpgradeStat)의 정의가 있다 — 시트에 빠진 수치는 노드 콘텐츠 로드가 막는다(NodeContentLoader).
    //
    // 전투 쪽은 늘어난 양(GainOf)을 자기 기본값(SO)에 더한다. 시트 기본값은 늘어난 양의 기준점과 Min·Max 자르기에만 쓰인다.
    // Percent 수치의 늘어난 양은 %p다: 비율·확률에는 ÷ 100을 더하고, 크기·속도 같은 기본 100% 수치는 (1 + 늘어난 양 ÷ 100)을 곱한다.
    public sealed class UpgradeStatValues
    {
        private readonly Dictionary<UpgradeStat, UpgradeStatDefinition> _stats = new();
        private readonly IReadOnlyDictionary<UpgradeStat, double> _sums;

        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }

        public UpgradeStatValues(IReadOnlyList<UpgradeStatDefinition> stats, IReadOnlyDictionary<UpgradeStat, double> sums)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _sums = sums ?? throw new ArgumentNullException(nameof(sums));

            foreach (UpgradeStatDefinition stat in stats)
                _stats.Add(stat.Stat, stat);
        }

        public UpgradeStatDefinition DefinitionOf(UpgradeStat stat) => _stats[stat];

        // 산 효과 값의 합(자르기 전).
        public float SumOf(UpgradeStat stat) => (float)Sum(stat);

        public float ValueOf(UpgradeStat stat)
        {
            UpgradeStatDefinition definition = _stats[stat];
            return (float)Math.Min(definition.Max, Math.Max(definition.Min, definition.DefaultValue + Sum(stat)));
        }

        // 기본값에서 늘어난 양(Min·Max로 자른 값 − 시트 기본값). 노드를 사지 않았으면 0이다.
        public float GainOf(UpgradeStat stat) => ValueOf(stat) - _stats[stat].DefaultValue;

        private double Sum(UpgradeStat stat) => _sums.TryGetValue(stat, out double sum) ? sum : 0;
    }
}
