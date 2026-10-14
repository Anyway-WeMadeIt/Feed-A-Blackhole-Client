using System;

namespace BlackHole.Core
{
    public readonly struct SupplyRequest
    {
        public EnemyDefinition Enemy { get; }
        public int Count { get; }

        // 재생성 세대. 0이면 보통 공급, n이면 재생성을 n번 거친 요청(Enemy.RespawnGeneration이 된다).
        public int RespawnGeneration { get; }

        // 재생성(파괴 때 RespawnChance 성공)으로 나온 요청인가.
        public bool Respawn => RespawnGeneration > 0;

        public SupplyRequest(EnemyDefinition enemy, int count, int respawnGeneration = 0)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "1 이상의 정수가 필요하다.");

            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Count = count;
            if (respawnGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(respawnGeneration), "0 이상의 정수가 필요하다.");

            RespawnGeneration = respawnGeneration;
        }
    }
}
