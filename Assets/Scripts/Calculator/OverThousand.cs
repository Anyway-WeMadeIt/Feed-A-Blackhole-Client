using System;
using System.Globalization;

namespace BlackHole.Core
{
    public class OverThousand
    {
        public static string GoldThousand(long gold)
        {
            string[] suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

            // long.MinValue 보정 (Math.Abs에서 오버플로 방지)
            if (gold == long.MinValue)
                gold = long.MinValue + 1;

            bool isNegative = gold < 0;
            long absGold = Math.Abs(gold);

            // 자릿수로 단위 계산
            string digits = absGold.ToString(CultureInfo.InvariantCulture);

            int group = (digits.Length - 1) / 3;

            // 1M 이하이면 그대로 반환 (그렇게 긴 자릿수는 아니라)
            if (group < 2)
                return gold.ToString("N0", CultureInfo.InvariantCulture);

            // suffix 범위를 넘어가는 경우
            if (group >= suffixes.Length)
                group = suffixes.Length - 1;

            int integerLength = digits.Length - group * 3;
            string integerPart = digits.Substring(0, integerLength);

            // 소수점 첫째 자리
            char decimalDigit = digits[integerLength];

            string result = decimalDigit == '0'
                ? $"{integerPart}{suffixes[group]}"
                : $"{integerPart}.{decimalDigit}{suffixes[group]}";

            return isNegative ? "-" + result : result;
        }
    }
}
