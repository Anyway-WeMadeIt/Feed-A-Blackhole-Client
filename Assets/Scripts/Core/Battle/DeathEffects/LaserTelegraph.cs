namespace BlackHole.Core
{
    // 레이저의 경로와 발사까지 남은 시간.
    // 피해는 예고가 끝난 후 계산(LaserBurst).
    public readonly struct LaserTelegraph
    {
        public Point2 Start { get; }
        public Point2 End { get; }
        public float Remaining { get; } // 발사까지 남은 시간(초).
        public float Duration { get; }  // 예고 전체 시간(초).

        internal LaserTelegraph(Point2 start, Point2 end, float remaining, float duration)
        {
            Start = start;
            End = end;
            Remaining = remaining;
            Duration = duration;
        }
    }
}
