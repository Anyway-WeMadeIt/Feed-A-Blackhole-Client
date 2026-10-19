using System;

namespace BlackHole.Core
{
    // 이정표 하나: Level(1 이상, Level 사다리 안), 목표 잔액(Gold, 0 이상), 닿은 뒤의 전장 배율(1 이상).
    [Serializable]
    public sealed class HqMilestoneData
    {
        public int Level;
        public long TargetGold;
        public float FieldScale;

        // 공전 모양(OrbitShape). OrbitStretch 1·OrbitTilt 0·OrbitInward 0·OrbitSpeed 1이 바뀌지 않은 공전이다.
        public float OrbitStretch = 1;
        public float OrbitTilt;
        public float OrbitInward;
        public float OrbitSpeed = 1;
    }
}
