using System;

namespace BlackHole.Core
{
    // 한 판의 시간제 종료 판정.
    // 경과 시간은 GameSession이 소유
    public sealed class TimeLimitRule
    {
        public TimeLimitDefinition Definition { get; }

        // 이 판의 제한 시간(초).
        public float Limit { get; private set; }

        // 이 판에서 제한 시간에 더한 초(Extend의 합).
        public float ExtendedSeconds { get; private set; }

        internal TimeLimitRule(TimeLimitDefinition definition)
        {
            Definition = definition;
            Limit = definition.Duration;
        }

        public float Remaining(float elapsed) => Math.Max(0, Limit - elapsed);

        // 진행 전: 이번 진행이 제한 시간을 넘지 않게 자른다.
        internal float LimitStep(float elapsed, float step) => Math.Min(step, Limit - elapsed);

        // 진행 후: 지금까지의 진행으로 판의 종료를 판정한다.
        internal bool HasExpired(float elapsed) => elapsed >= Limit;

        // 테스트 도구(BattleCheats)가 부른다. 제한 시간을 늘리되 판 통계의 더해진 시간(ExtendedSeconds)에는 세지 않는다.
        internal void ExtendUncounted(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "0 이상의 유한한 값이 필요하다.");

            Limit += seconds;
        }

        // 이 판의 제한 시간을 늘린다(블랙홀 Level업)
        internal void Extend(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "0 이상의 유한한 값이 필요하다.");

            Limit += seconds;
            ExtendedSeconds += seconds;
        }
    }
}
