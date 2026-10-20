using System;
using System.Globalization;

namespace BlackHole.Unity
{
    // 화면에 쓰는 수 글자.
    internal static class NumberText
    {
        private static readonly string[] Units = { string.Empty, "K", "M", "B", "T", "Qa", "Qi" };

        // 큰 수를 줄인다: 950 → 950, 1,500 → 1.5K, 2,600,000,000,000 → 2.6T. 노드 비용은 1,000조(1Qa)까지 오른다.
        public static string Compact(long value) => Compact((double)value);

        // 소수 합계(결산의 스킬별 피해)도 같은 단위로 줄인다: 233,650,000.4 → 233.65M.
        public static string Compact(double value)
        {
            double scaled = value;
            int unit = 0;

            while (Math.Abs(scaled) >= 1000 && unit < Units.Length - 1)
            {
                scaled /= 1000;
                unit++;
            }

            return scaled.ToString("0.##", CultureInfo.InvariantCulture) + Units[unit];
        }

        // 시트 단위의 수치 값: 270, 2.5, 1365.
        public static string Value(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        // 부호를 붙인 효과 값: +50, -1.
        public static string Signed(float value) => (value >= 0 ? "+" : string.Empty) + Value(value);
    }
}
