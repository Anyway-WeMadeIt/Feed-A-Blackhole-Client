using System;

namespace BlackHole.Core
{
    // 주기 출현 종류(혜성)의 출현 띠를 일반 출현 띠(EnemyPlacementData)의 바깥 반지름 기준 오프셋으로 적은 저작 형식.
    // 띠 = [일반 바깥 반지름 + InnerOffset, 일반 바깥 반지름 + OuterOffset]. 음수는 안쪽이다.
    [Serializable]
    public sealed class PeriodicSpawnPlacementData
    {
        public float InnerOffset;
        public float OuterOffset;
    }
}
