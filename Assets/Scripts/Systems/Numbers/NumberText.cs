using System;
using System.Globalization;

namespace BlackHole.Systems.Numbers
{
    // 수를 화면 글자로 쓴다. 게임을 모른다 — 무슨 수(Gold·비용·피해)인지는 부르는 쪽이 정한다.
    // 큰 수를 줄이는 방식이 둘이고, 화면마다 지금 쓰는 쪽을 그대로 쓴다(하나로 합치면 보이는 숫자가 바뀐다).
    // - Short: 1,000,000 미만은 자릿수 구분(950,000), 그 위는 버림 한 자리(1.2M). Gold 표기(NumberFormat.Short).
    // - Compact: 1,000부터 반올림 두 자리(1.5K, 233.65M). 노드 비용·결산 피해.
    public static class NumberText
    {
        private static readonly string[] Units = { string.Empty, "K", "M", "B", "T", "Qa", "Qi" };

        // 설정한 표기로 쓴다: Short(1.2M) 또는 Scientific(1.2e6).
        public static string Of(long value, NumberFormat format) =>
            format == NumberFormat.Scientific ? Scientific(value) : Short(value);

        // 1,000,000 미만은 그대로(자릿수 구분), 그 위는 단위와 버림 한 자리 소수: 1,234,567 → 1.2M, 3,000,000 → 3M.
        public static string Short(long value)
        {
            // long.MinValue 보정 (Math.Abs에서 오버플로 방지)
            if (value == long.MinValue)
                value = long.MinValue + 1;

            bool isNegative = value < 0;
            long absValue = Math.Abs(value);

            // 자릿수로 단위 계산
            string digits = absValue.ToString(CultureInfo.InvariantCulture);

            int group = (digits.Length - 1) / 3;

            // 1M 이하이면 그대로 반환 (그렇게 긴 자릿수는 아니라)
            if (group < 2)
                return value.ToString("N0", CultureInfo.InvariantCulture);

            // 단위 범위를 넘어가는 경우
            if (group >= Units.Length)
                group = Units.Length - 1;

            int integerLength = digits.Length - group * 3;
            string integerPart = digits.Substring(0, integerLength);

            // 소수점 첫째 자리
            char decimalDigit = digits[integerLength];

            string result = decimalDigit == '0'
                ? $"{integerPart}{Units[group]}"
                : $"{integerPart}.{decimalDigit}{Units[group]}";

            return isNegative ? "-" + result : result;
        }

        // 1000 미만은 그대로, 그 위는 가수 한 자리 소수와 지수: 330,000,000 → 3.3e8. 소수가 0이면 뺀다(3e8).
        public static string Scientific(long value)
        {
            if (value == long.MinValue)
                value = long.MinValue + 1;

            long magnitude = Math.Abs(value);

            if (magnitude < 1000)
                return value.ToString(CultureInfo.InvariantCulture);

            int exponent = (int)Math.Floor(Math.Log10(magnitude));
            // 반올림으로 10.0이 되면 지수를 올린다.
            double mantissa = Math.Round(magnitude / Math.Pow(10, exponent), 1);

            if (mantissa >= 10)
            {
                mantissa = 1;
                exponent++;
            }

            string body = mantissa.ToString("0.#", CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
            return value < 0 ? "-" + body : body;
        }

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
