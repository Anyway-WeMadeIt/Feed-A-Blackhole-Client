using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 설정된 비율에 가깝게 항목을 분배하는 누적 몫 방식.
    // 각 항목의 몫을 누적해 1 이상이 되면 선택하고 1을 차감한다.
    // 초기 몫은 난수로 분산해 판마다 선택 순서를 다르게 한다.
    // 단순 확률 추첨보다 한 판 안에서 목표 구성 비율을 안정적으로 유지한다.
    internal sealed class QuotaPicker
    {
        // 비율이 0보다 큰 칸의 번호(앞에서부터)와, 그 칸까지 넘어왔을 때 그 칸을 고를 비율(남은 비율 가운데 그 칸의 몫).
        private readonly int[] _entries;
        private readonly double[] _shares;
        private readonly double[] _quota;

        // ratios는 합이 0보다 커야 한다(합이 1이 아니어도 된다). 처음 몫을 흩뜨리는 데 random을 칸 수만큼 쓴다.
        public QuotaPicker(IReadOnlyList<float> ratios, BattleRandom random)
        {
            double remaining = 0;
            int count = 0;

            for (int i = 0; i < ratios.Count; i++)
            {
                remaining += ratios[i];

                if (ratios[i] > 0)
                    count++;
            }

            if (!(remaining > 0))
                throw new ArgumentException("비율의 합이 0보다 커야 한다.", nameof(ratios));

            _entries = new int[count];
            _shares = new double[count];
            _quota = new double[count];

            for (int i = 0, entry = 0; i < ratios.Count; i++)
            {
                float start = random.NextFloat();

                if (ratios[i] <= 0)
                    continue;

                _entries[entry] = i;
                _shares[entry] = ratios[i] / remaining;
                _quota[entry] = start;
                remaining -= ratios[i];
                entry++;
            }
        }

        public int Pick()
        {
            int last = _entries.Length - 1;

            for (int entry = 0; entry < last; entry++)
            {
                _quota[entry] += _shares[entry];

                if (_quota[entry] >= 1)
                {
                    _quota[entry] -= 1;
                    return _entries[entry];
                }
            }

            return _entries[last];
        }
    }
}
