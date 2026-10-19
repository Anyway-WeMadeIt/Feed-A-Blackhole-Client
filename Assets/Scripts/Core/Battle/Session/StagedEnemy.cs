using System;

namespace BlackHole.Core
{
    // 판의 처음에 정한 자리에 놓는 적 하나(GameSession.BeginStaged). 성질은 없다.
    public readonly struct StagedEnemy
    {
        public EnemyDefinition Kind { get; }

        public int Tier { get; } // 색 등급(0부터, 0 = 빨강).

        public int Size { get; }

        public Point2 Position { get; } // 월드 자리(블랙홀 = 원점).

        public StagedEnemy(EnemyDefinition kind, int tier, int size, Point2 position)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));

            if (tier < 0 || tier >= kind.Tiers.Count)
                throw new ArgumentOutOfRangeException(nameof(tier), $"'{kind.Type}'의 색 등급은 0부터 {kind.Tiers.Count - 1}까지다.");

            if (size < SizeRule.Base || size > SizeRule.Max)
                throw new ArgumentOutOfRangeException(nameof(size), $"크기는 {SizeRule.Base}부터 {SizeRule.Max}까지다.");

            Tier = tier;
            Size = size;
            Position = position;
        }
    }
}
