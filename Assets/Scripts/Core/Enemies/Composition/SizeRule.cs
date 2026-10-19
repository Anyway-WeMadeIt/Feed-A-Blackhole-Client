using System;

namespace BlackHole.Core
{
    // 크기 규칙: 한 종류의 크기 s(1부터)는 크기 1부터 s까지의 적이 같은 몫으로 섞여 나오게 한다. 종류와 색은 바꾸지 않는다.
    // 별만 예외: 가장 큰 크기부터 StarSizeWindow개(s − 2 ~ s)만 섞여 나온다(MinSize). 별 크기는 블랙홀 구역(star.size-03)에서만 4를 넘으므로
    // 그 앞 구역(s ≤ 3)은 1 ~ s 그대로이고, 블랙홀 구역에서 크기를 살 때마다 별 전체가 한 칸씩 커진다 [사용자].
    // 크기 k인 적은 색의 베이스 수치에 선형 배율을 받는다:
    // - HP·Gold·EXP: 1 + (k − 1) × 1.0    (크기 2면 2배, 3이면 3배)
    // - 반지름:       1 + (k − 1) × 증가분 (증가분은 종류의 것, EnemyDefinition.RadiusStep)
    //   원작 실측(C0 화면 지름): 소행성 34·45·57px → 0.35, 행성 127·138·149·161px → 0.09, 별 286·383·475px → 0.33.
    public static class SizeRule
    {
        // 노드를 사지 않은 크기.
        public const int Base = 1;

        // 노드 저작 실수를 막는 상한. 판 조립이 크기마다 수치를 미리 계산하므로 둔다.
        public const int Max = 20;

        private const float StatStep = 1.0f;

        // 별이 한 판에 섞여 나오는 크기의 수(가장 큰 크기부터).
        public const int StarSizeWindow = 3;

        // 크기 s인 종류 type이 나오는 가장 작은 크기. 별은 s − StarSizeWindow + 1(1 이상), 나머지는 1.
        public static int MinSize(EnemyType type, int size) =>
            type == EnemyType.Star ? Math.Max(Base, size - StarSizeWindow + 1) : Base;

        public static float StatMultiplier(int size) => 1 + (Require(size) - Base) * StatStep;

        // radiusStep: 크기 1당 반지름 증가분(크기 1의 반지름 대비, 0 이상). 종류마다 다르다.
        public static float RadiusMultiplier(int size, float radiusStep) => 1 + (Require(size) - Base) * radiusStep;

        private static int Require(int size)
        {
            if (size < Base || size > Max)
                throw new ArgumentOutOfRangeException(nameof(size), $"크기는 {Base}부터 {Max}까지다. 받은 값: {size}.");

            return size;
        }
    }
}
