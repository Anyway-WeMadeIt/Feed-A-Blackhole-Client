namespace BlackHole.Core
{
    // 레이저 별 효과가 발동한 기록: 어디서 어디까지, 어떤 정의로, 치명타였는가, 맞힌 적의 수.
    public readonly struct LaserBurst
    {
        public long Sequence { get; }  // 판 안에서 늘어나는 번호(번개·폭발과 같은 줄).
        public Point2 Start { get; }   // 레이저가 시작한 자리(화면 밖).
        public Point2 End { get; }     // 레이저가 끝난 자리(화면 밖). Start -> End로 별이 죽은 자리를 지나 화면을 가로지른다.
        public LaserBurstDefinition Definition { get; } // 이 판의 정의(너비, 피해 등).
        public bool Critical { get; }
        public int HitCount { get; } // 피해를 준 적의 수.

        internal LaserBurst(
            long sequence,
            Point2 start,
            Point2 end,
            LaserBurstDefinition definition,
            bool critical,
            int hitCount)
        {
            Sequence = sequence;
            Start = start;
            End = end;
            Definition = definition;
            Critical = critical;
            HitCount = hitCount;
        }
    }
}
