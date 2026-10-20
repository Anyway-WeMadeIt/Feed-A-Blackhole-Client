using System;

namespace BlackHole.Core
{
    // 한 판 안에서 적 하나를 가리키는 식별자. 출현 순서대로 발급.
    public readonly struct EnemyId : IEquatable<EnemyId>
    {
        public int Value { get; }

        public EnemyId(int value)
        {
            Value = value;
        }

        public bool Equals(EnemyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EnemyId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Enemy {Value}";
    }
}
