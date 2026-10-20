using System;

namespace BlackHole.Core
{
    // UpgradeStatUnit·UpgradeStatValueType의 이름은 시트 글자와 같아야 한다.
    // 노드 콘텐츠 로더가 시트 칸을 이름으로 찾는다(NodeContentLoader.TryName).
    // 그래서 코드에서 이름으로 쓰지 않는 값(Flat, Float)도 시트가 쓰고 있음
    public enum UpgradeStatUnit
    {
        Flat,
        Percent,
    }

    public enum UpgradeStatValueType
    {
        Float,
        Int,
    }

    public sealed class UpgradeStatDefinition
    {
        public UpgradeStat Stat { get; }
        public string StatId { get; } // 시트에 적힌 이름(breaker.critChance). 표시와 노드 그림 키가 쓴다.
        public UpgradeStatValueType ValueType { get; }
        public UpgradeStatUnit Unit { get; }
        public float DefaultValue { get; }
        public float Min { get; } // 시트 칸이 비어 있으면 각각 음·양의 무한대다.
        public float Max { get; }
        public bool Enabled { get; } // 끈 수치를 어떻게 다룰지는 정하지 않았다.

        public UpgradeStatDefinition(
            UpgradeStat stat,
            string statId,
            UpgradeStatValueType valueType,
            UpgradeStatUnit unit,
            float defaultValue,
            float min,
            float max,
            bool enabled)
        {
            Stat = stat;
            StatId = statId ?? throw new ArgumentNullException(nameof(statId));
            ValueType = valueType;
            Unit = unit;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            Enabled = enabled;
        }
    }
}
