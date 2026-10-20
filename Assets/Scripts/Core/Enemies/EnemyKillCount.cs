using System;

namespace BlackHole.Core
{
    // 한 종류의 처치 수.
    public readonly struct EnemyKillCount
    {
        public EnemyDefinition Enemy { get; }
        public int Count { get; }

        public EnemyKillCount(EnemyDefinition enemy, int count)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Count = count;
        }
    }
}
