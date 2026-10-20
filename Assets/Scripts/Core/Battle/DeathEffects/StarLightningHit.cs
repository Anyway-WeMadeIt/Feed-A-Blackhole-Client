namespace BlackHole.Core
{
    // 전기 별의 번개가 한 번 옮겨 간 기록: 어디서 어디로, 치명타였는가. 화면(StarLightningBolts)은 이것을 읽어 그릴 뿐 피해를 다시 계산하지 않는다.
    // 전기 소행성의 LightningHit과 따로 쌓인다(DeathEffects.StarLightningHits).
    public readonly struct StarLightningHit
    {
        // 판 안에서 늘어나는 번호(사망 효과 전체가 공유). 같은 기록을 두 번 그리지 않는 데 쓴다.
        public long Sequence { get; }
        public Point2 From { get; }
        public Point2 To { get; }
        public bool Critical { get; }

        internal StarLightningHit(long sequence, Point2 from, Point2 to, bool critical)
        {
            Sequence = sequence;
            From = from;
            To = to;
            Critical = critical;
        }
    }
}
