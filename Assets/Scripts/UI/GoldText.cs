using System;
using System.Globalization;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // Gold 숫자 표기(설정 Number Format). 선택지 번호와 같은 순서다 — GameSettings.NumberFormatOptions.
    // 전체 자릿수(1,234,567) 표기는 두지 않는다 — 적 위 획득 글자와 고정 폭 상자에서 큰 수가 넘친다. 두 표기는 길이가 거의 일정하다.
    public enum NumberFormat
    {
        Short,      // 1.2M, 330K (OverThousand)
        Scientific, // 1.2e6
    }

    // Gold를 주어진 표기로 쓴다. 전투의 획득 글자·결산·업그레이드 화면이 모두 이것을 쓴다.
    // 표기는 설정(GameSettings.NumberFormatValue)에서 온다: 화면은 INumberFormatView로 받아 두고, 적 위 글자는 EnemyView가 설정에서 읽어 넘긴다.
    public static class GoldText
    {
        public static string Of(long gold, NumberFormat format)
        {
            return format == NumberFormat.Scientific ? Scientific(gold) : OverThousand.GoldThousand(gold);
        }

        // 1000 미만은 그대로, 그 위는 가수 한 자리 소수와 지수: 330,000,000 → 3.3e8. 소수가 0이면 뺀다(3e8).
        private static string Scientific(long gold)
        {
            if (gold == long.MinValue)
                gold = long.MinValue + 1;

            long magnitude = Math.Abs(gold);

            if (magnitude < 1000)
                return gold.ToString(CultureInfo.InvariantCulture);

            int exponent = (int)Math.Floor(Math.Log10(magnitude));
            // 반올림으로 10.0이 되면 지수를 올린다.
            double mantissa = Math.Round(magnitude / Math.Pow(10, exponent), 1);

            if (mantissa >= 10)
            {
                mantissa = 1;
                exponent++;
            }

            string body = mantissa.ToString("0.#", CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
            return gold < 0 ? "-" + body : body;
        }
    }

    // Gold 숫자를 보여 주는 화면. UISettingsSync가 시작할 때 한 번, 표기 설정이 바뀔 때마다 부른다(닫힌 화면도).
    // 화면은 표기를 간직하고, 마지막으로 보인 값을 새 표기로 다시 쓴다.
    public interface INumberFormatView
    {
        void SetNumberFormat(NumberFormat format);
    }
}
