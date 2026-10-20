using System;

namespace BlackHole.Core
{
    // 처치 장부의 칸: (종류, 색 등급, 크기, 황금 여부). 처치 하나의 Gold·EXP는 이 넷으로 정해진다(EnemyDefinition.StatsAt).
    // 수거형(혜성)은 색 등급 0, 크기 1, 황금 아님이다.
    // 서버가 결산을 검사하는 단위다(계약 3.3의 kills, 5.3). 세는 일은 W3-1.
    public readonly struct KillBucket : IEquatable<KillBucket>
    {
        public EnemyDefinition Enemy { get; }

        // 색 등급(0부터).
        public int Tier { get; }

        // 크기(1부터).
        public int Size { get; }

        // 성질이 황금(GoldenDefinition)인가.
        public bool Golden { get; }

        public KillBucket(EnemyDefinition enemy, int tier, int size, bool golden)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Tier = tier;
            Size = size;
            Golden = golden;
        }

        public bool Equals(KillBucket other) =>
            ReferenceEquals(Enemy, other.Enemy) && Tier == other.Tier && Size == other.Size && Golden == other.Golden;

        public override bool Equals(object obj) => obj is KillBucket other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Enemy?.GetHashCode() ?? 0;
                hash = hash * 31 + Tier;
                hash = hash * 31 + Size;
                return hash * 2 + (Golden ? 1 : 0);
            }
        }
    }

    // 칸 하나의 처치 수(1 이상).
    public readonly struct KillBucketCount
    {
        public KillBucket Bucket { get; }
        public int Count { get; }

        public KillBucketCount(KillBucket bucket, int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "처치 수는 1 이상이다.");

            Bucket = bucket;
            Count = count;
        }
    }
}
