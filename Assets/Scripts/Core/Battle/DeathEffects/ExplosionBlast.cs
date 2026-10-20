namespace BlackHole.Core
{
    // 폭발 한 번의 기록.
    public readonly struct ExplosionBlast
    {
        public long Sequence { get; }
        public Point2 Center { get; }
        // 실제로 터진 반지름(죽은 별의 크기 배율 반영).
        public float Radius { get; }
        // 피해를 준 적의 수.
        public int HitCount { get; }

        internal ExplosionBlast(long sequence, Point2 center, float radius, int hitCount)
        {
            Sequence = sequence;
            Center = center;
            Radius = radius;
            HitCount = hitCount;
        }
    }
}
