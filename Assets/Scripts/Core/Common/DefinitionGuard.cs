using System;

namespace BlackHole.Core
{
    // 정의 생성자가 쓰는 수치 규칙. 로더는 같은 생성자를 호출해 이 규칙을 경로별 진단으로 모은다.
    // 규칙을 로더와 생성자에 두 번 쓰지 않는다.
    internal static class DefinitionGuard
    {
        public static float Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, "유한한 양수가 필요하다.");
            return value;
        }

        // 부호가 뜻을 가진 값(공전 방향 등)의 검사: 유한하고 0이 아니면 양수든 음수든 된다.
        public static float NonZeroFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value == 0)
                throw new ArgumentOutOfRangeException(name, "0이 아닌 유한한 값이 필요하다.");
            return value;
        }

        public static long NotNegative(long value, string name)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, "0 이상이 필요하다.");
            return value;
        }

        // 실행 중 요청 값(진행 시간)의 검사. 정의가 아니라 호출 계약이다.
        public static void Delta(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "0 이상의 유한한 값이 필요하다.");
        }
    }
}
