using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 질량 규칙: 한 종류의 질량(%)이 그 종류의 색 분포를 정한다. 질량은 수치(HP·Gold·EXP)에 곱하지 않는다.
    //
    // 질량 M%를 배수 m = M / 100으로 보고, 색 c(0부터)가 색 축의 칸 [c, c+1]을 차지한다고 하면:
    //   색 c의 비율 = (구간 [m − 2, m]이 칸 [c, c+1]과 겹치는 길이) ÷ 2.
    //   축 밖으로 나간 부분은 양 끝의 색에 붙인다(0 아래는 첫 색, 색 수 N 위는 마지막 색).
    // 구간 길이가 2라 동시에 나오는 색은 최대 3개다. 6색(빨주노초파보)이면:
    //   ≤100% 빨강만, 150% 빨:주 = 3:1, 200% 1:1, 250% 빨:주:노 = 1:2:1, 300% 주:노 = 1:1, …, 600% 파:보 = 1:1, ≥700% 보라만.
    // 마지막 색만 남는 질량은 (N + 1) × 100%다. 그 위로는 차이가 없다.
    public static class MassRule
    {
        // 노드를 사지 않은 질량(%).
        public const float Base = 100;

        private const float Window = 2;

        // 동시에 나오는 색의 최대 수(구간 길이 2 → 3).
        public const int MaxConcurrentTiers = 3;

        // 비율에서 지금 나오는 색의 수(0보다 큰 칸).
        public static int ActiveTiers(IReadOnlyList<float> ratios)
        {
            if (ratios == null) throw new ArgumentNullException(nameof(ratios));

            int active = 0;

            for (int i = 0; i < ratios.Count; i++)
            {
                if (ratios[i] > 0)
                    active++;
            }

            return active;
        }

        // 마지막 색의 톤 수(1 이상, 1이면 톤 없음): 화면에 나오는 색(visibleColors)이 MaxConcurrentTiers보다 적은 만큼.
        // 질량이 올라 파:보, 보라만 남으면(600%·700%+) 마지막 색을 톤으로 나눠 나오는 색의 수를 유지한다 — 후반이 한 색으로 단조로워지지 않게.
        // visibleColors는 이 종류의 색에 상위 종류(변환 사슬에서 다음 종류들, 이 판에 공급되는 것)의 색을 더한 수다(EnemySupply) —
        // 상위 종류가 나오기 시작하면 그 색이 새 주목점이고 마지막 색은 배경이 되므로 톤을 줄여 상위 색을 돋보이게 한다.
        // 톤은 색만 다르고 수치는 같다(EnemyStatTable·결산 장부는 톤을 모른다). 어느 톤이 붙는지는 출현 때 같은 몫으로 정해진다(EnemySupply).
        // 색이 MaxConcurrentTiers보다 적은 종류(픽업)는 나눌 것이 없다.
        public static int LastTierTones(int visibleColors, int tierCount) =>
            Math.Max(1, Math.Min(MaxConcurrentTiers, tierCount) - visibleColors);

        // 질량 mass(%)에서 색 tierCount개의 비율(합 1).
        public static float[] TierRatios(float mass, int tierCount)
        {
            if (float.IsNaN(mass) || float.IsInfinity(mass) || mass < 0)
                throw new ArgumentOutOfRangeException(nameof(mass), $"질량은 0 이상의 유한한 값이어야 한다. 받은 값: {mass}%.");

            if (tierCount < 1)
                throw new ArgumentOutOfRangeException(nameof(tierCount), "색이 하나 이상 필요하다.");

            double high = mass / 100.0;
            double low = high - Window;
            var ratios = new float[tierCount];

            for (int c = 0; c < tierCount; c++)
                ratios[c] = (float)(Overlap(low, high, c, c + 1) / Window);

            // 축 밖으로 나간 부분.
            ratios[0] += (float)(Math.Max(0, Math.Min(high, 0) - low) / Window);
            ratios[tierCount - 1] += (float)(Math.Max(0, high - Math.Max(low, tierCount)) / Window);

            return ratios;
        }

        private static double Overlap(double a0, double a1, double b0, double b1) =>
            Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));
    }
}
