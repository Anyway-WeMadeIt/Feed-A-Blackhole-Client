using System;

namespace BlackHole.Core
{
    // HQ(원점)를 둘러싼 출현 띠.
    [Serializable]
    public sealed class EnemyPlacementData
    {
        public float MinDistance;
        public float MaxDistance;
    }
}
