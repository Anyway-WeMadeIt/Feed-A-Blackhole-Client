#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;

namespace BlackHole.Unity
{
    // 숫자를 StringBuilder에 할당 없이 붙인다.
    // StringBuilder.Append(int/float)는 내부에서 ToString()을 불러 매번 문자열을 만든다. 성능 HUD가 스스로 GC 할당을 만들면
    // 'GC 할당' 측정값을 더럽히므로, 숫자를 한 글자씩 직접 쓴다. 문화권과 관계없이 소수점은 늘 '.'이다(CSV용).
    internal static class PerfText
    {
        public static StringBuilder Int(StringBuilder sb, long value)
        {
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }

            if (value >= 10)
                Int(sb, value / 10);

            sb.Append((char)('0' + (int)(value % 10)));
            return sb;
        }

        // 소수 decimals자리(0~3)까지 반올림해 붙인다. 값이 없으면(NaN·무한대) '-'.
        public static StringBuilder Fixed(StringBuilder sb, double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return sb.Append('-');

            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }

            long scale = decimals <= 0 ? 1 : decimals == 1 ? 10 : decimals == 2 ? 100 : 1000;
            long scaled = (long)(value * scale + 0.5);

            Int(sb, scaled / scale);

            if (scale == 1)
                return sb;

            sb.Append('.');
            long fraction = scaled % scale;

            for (long digit = scale / 10; digit > 0; digit /= 10)
            {
                sb.Append((char)('0' + (int)(fraction / digit)));
                fraction %= digit;
            }

            return sb;
        }

        // 두 자리로 채운 정수(시각 표시용).
        public static StringBuilder Two(StringBuilder sb, int value)
        {
            if (value < 10)
                sb.Append('0');

            return Int(sb, value);
        }
    }
}
#endif
