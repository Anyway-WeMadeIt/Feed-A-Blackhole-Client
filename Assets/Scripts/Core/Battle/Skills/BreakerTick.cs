namespace BlackHole.Core
{
    // Breaker Tick 하나의 기록. 화면은 이것을 읽어 그릴 뿐 피해를 다시 계산하지 않는다.
    public readonly struct BreakerTick
    {
        // 이 Breaker의 몇 번째 Tick인가(1부터). 같은 Tick을 두 번 그리지 않는 데 쓴다.
        public int Number { get; }
        // 공격 원의 중심(그 Tick의 조준점). 조준점이 없던 빈 Tick이면 null이다.
        public Point2? Center { get; }
        public float Radius { get; }
        // 피해를 준 적의 수.
        public int HitCount { get; }
        // 이 Tick이 치명타였는가. 맞은 적 모두가 같은 결과를 받는다. 맞은 적이 없으면 false다.
        public bool IsCritical { get; }

        internal BreakerTick(int number, Point2? center, float radius, int hitCount, bool isCritical)
        {
            Number = number;
            Center = center;
            Radius = radius;
            HitCount = hitCount;
            IsCritical = isCritical;
        }
    }
}
