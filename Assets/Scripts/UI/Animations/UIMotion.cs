using UnityEngine;

namespace BlackHole.Unity
{
    // UI 연출이 함께 쓰는 곡선. 화면·부속이 식을 각자 쓰지 않고 여기 것을 부른다. 게임을 모른다.
    internal static class UIMotion
    {
        // 0 → 1을 양 끝에서 느리게(SmoothStep). t는 0 ~ 1로 자른다.
        public static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        // 0 → 1을 처음에 빠르게(OutQuad). t는 0 ~ 1로 자른다.
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // 잠깐 커졌다 돌아오는 배율: t가 0 → 1로 가는 동안 1 → 1 + amount → 1(sin 반 바퀴). t가 1 이상이면 정확히 1.
        public static float Pop(float t, float amount)
        {
            if (t >= 1f)
                return 1f;

            return 1f + amount * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        // 목표를 지수 곡선으로 따라갈 때 이 프레임에 가는 몫(0 ~ 1): Lerp(now, target, Follow(k, dt)). k가 클수록 빨리 따라간다. 프레임 길이와 무관하게 같은 빠르기.
        public static float Follow(float sharpness, float deltaTime) => 1f - Mathf.Exp(-sharpness * deltaTime);
    }
}
