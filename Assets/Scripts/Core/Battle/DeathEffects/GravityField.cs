namespace BlackHole.Core
{
    // 지금 켜져 있는 중력장 하나의 상태. 화면은 이것을 읽어 그릴 뿐 끌림을 다시 계산하지 않는다.
    // 처리(DeathEffects.Resolve)마다 지금 상태로 다시 채운다. 시간이 다 된 중력장은 목록에서 빠진다.
    public readonly struct GravityField
    {
        public long Sequence { get; }  // 판 안에서 늘어나는 번호(번개·폭발·레이저와 같은 줄). 화면이 같은 중력장을 알아보는 데 쓴다.
        public Point2 Center { get; }  // 고정된 중심(중력장 성질 적이 죽은 자리).
        public float Radius { get; }   // 끌어들이는 범위(노드 반영).
        public float Remaining { get; } // 남은 시간(초).
        public float Duration { get; }  // 전체 시간(초).
        public int Captured { get; }    // 마지막 이동에서 끌린 적의 수.

        internal GravityField(long sequence, Point2 center, float radius, float remaining, float duration, int captured)
        {
            Sequence = sequence;
            Center = center;
            Radius = radius;
            Remaining = remaining;
            Duration = duration;
            Captured = captured;
        }
    }
}
