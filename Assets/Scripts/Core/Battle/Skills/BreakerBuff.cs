namespace BlackHole.Core
{
    // Breaker가 받은 처치 버프 중첩 하나의 기록. 화면은 이것을 읽어 그릴 뿐 효과를 다시 계산하지 않는다.
    // 중첩은 받은 것마다 따로 있고 따로 끝난다. 새로 받아도 이미 있는 중첩의 시간은 바뀌지 않는다.
    // 중첩당 수치는 판 동안 고정이라 기록하지 않는다: 효과는 중첩 수 × Breaker 정의의 중첩당 보너스다(BreakerSkill).
    public readonly struct BreakerBuff
    {
        // 이 Breaker가 받은 몇 번째 버프인가(1부터, 모든 버프 종류가 같은 번호를 이어 쓴다).
        // 한 Step에 중첩이 새로 들고 끝나 개수가 같아도, 화면이 새 중첩을 번호로 알아볼 수 있다.
        public int Number { get; }
        // 받은 순간의 지속 시간(초).
        public float Duration { get; }
        // 남은 시간(초). 0이 되는 Step에 목록에서 빠진다.
        public float Remaining { get; }

        internal BreakerBuff(int number, float duration) : this(number, duration, duration)
        {
        }

        private BreakerBuff(int number, float duration, float remaining)
        {
            Number = number;
            Duration = duration;
            Remaining = remaining;
        }

        internal BreakerBuff Aged(float delta) => new(Number, Duration, Remaining - delta);
    }
}
