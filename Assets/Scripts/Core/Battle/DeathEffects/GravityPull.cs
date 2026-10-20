using System;

namespace BlackHole.Core
{
    // 중력장 하나가 적 하나를 이번 이동에서 옮기는 규칙: 나선으로 끌어당겨 작은 원반(두께 있는 띠)에 머물게 한다.
    // - 범위 가장자리: 중심으로 끌리며 조금씩 옆으로 휜다. 가장자리 띠(EdgeBand) 안에서는 끌림이 서서히 커진다.
    // - 안으로 들어올수록: 회전이 빨라져 곡선이 뚜렷해진다(EdgeSpin → DiscSpin).
    // - 원반 근처: 원반에 닿기 전부터(ApproachBand) 안쪽 이동이 줄어 원반에서 0이 되고, 회전만 남는다. 끝까지 빨라지지 않아 중심에서 꺾이거나 떨리지 않는다.
    // - 켜질 때(GrabSeconds)와 꺼지기 전(ReleaseSeconds): 끌림·회전이 짧게 약해지고 그만큼 평소 공전이 섞인다.
    // 적마다 원반 띠 안의 머무는 반경과 회전 속도가 조금씩 다르다(적 번호와 중력장 번호로 정한다 — 같은 판이면 늘 같다).
    // 그래서 한 원 둘레에 줄지어 서지 않고 띠 안에 흩어져 돈다. 원반 안쪽이 더 빨리 돌아 서로 미끄러지며 띠를 따라 퍼진다.
    // 거리는 중력장 범위에 대한 비율이다(범위 노드가 원반도 함께 키운다). 판정 위치도 이대로 옮겨진다.
    internal static class GravityPull
    {
        // 원반 띠 [임시]: 범위의 이 비율 사이의 반경에 머문다.
        private const float DiscInner = 0.12f;
        private const float DiscOuter = 0.32f;
        // 머무는 반경까지 범위의 이 비율만큼 남으면 안쪽 이동이 줄기 시작한다.
        private const float ApproachBand = 0.3f;
        // 범위 가장자리의 이 비율 폭 안에서 끌림이 0에서 1로 커진다(밖에서 들어오는 적이 갑자기 꺾이지 않게).
        private const float EdgeBand = 0.2f;
        // 회전 속도(라디안/초): 가장자리, 원반(띠 가운데 반경 기준).
        private const float EdgeSpin = 0.5f;
        private const float DiscSpin = 2.2f;
        // 적마다 회전 속도를 이 비율 안에서 다르게 한다.
        private const float SpinJitter = 0.15f;
        // 머무는 반경 안쪽에 있던 적이 밖으로 밀려나는 속도(안쪽으로 끄는 속도에 대한 비율).
        private const float OutwardScale = 0.5f;
        // 켜질 때 붙잡는 시간, 꺼지기 전 풀리는 시간(초).
        private const float GrabSeconds = 0.25f;
        private const float ReleaseSeconds = 0.4f;
        // 회전 방향: 시계방향(은하 자전·붕괴와 같은 방향). 1이면 반시계방향.
        private const float SpinDirection = -1f;

        private const float DiscMiddle = (DiscInner + DiscOuter) * 0.5f;
        private const float CenterEpsilon = 1e-4f;

        // 중력장의 이번 세기(0 ~ 1): 켜질 때 붙잡고, 꺼지기 전 풀린다.
        public static float Strength(float elapsed, float remaining) =>
            Smooth(Clamp01(elapsed / GrabSeconds)) * Smooth(Clamp01(remaining / ReleaseSeconds));

        // 이 적이 중력장에 끌리는 정도(0 ~ 1): 세기 × 가장자리 띠. 0이면 끌리지 않는다.
        public static float Weight(Enemy enemy, Point2 center, float radius, float strength)
        {
            float reach = radius + enemy.Stats.Radius;
            float dx = enemy.Position.X - center.X;
            float dy = enemy.Position.Y - center.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return strength * Smooth(Clamp01((reach - distance) / (EdgeBand * radius)));
        }

        // weight만큼 끌어 옮긴다(Weight가 0보다 클 때만 부른다). 나머지(1 - weight)는 부르는 쪽이 평소처럼 공전시킨다.
        public static void Move(Enemy enemy, Point2 center, float radius, float force, long fieldSequence, float weight, float delta)
        {
            float dx = enemy.Position.X - center.X;
            float dy = enemy.Position.Y - center.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            float bandAt = Hash01(enemy.Id.Value, fieldSequence, 0);
            float spinAt = Hash01(enemy.Id.Value, fieldSequence, 1);
            float discFraction = DiscInner + (DiscOuter - DiscInner) * bandAt;
            float disc = radius * discFraction;

            // 안쪽 이동: 멀리서는 force, 머무는 반경에 가까워지며 부드럽게 0. 반경을 넘어가지 않는다.
            float gap = distance - disc;
            float approach = Ease(gap / (ApproachBand * radius));
            float next;

            if (gap > 0)
                next = Math.Max(disc, distance - force * approach * weight * delta);
            else
                next = Math.Min(disc, distance - force * OutwardScale * approach * weight * delta);

            // 회전: 가장자리에서 원반으로 들어올수록 빨라지고 원반에서는 그대로. 원반 안쪽일수록 빨라(반경에 반비례) 한쪽에서 몰려온 적도 띠를 따라 퍼진다.
            float discSpin = DiscSpin * (DiscMiddle / discFraction) * (1 + SpinJitter * (2 * spinAt - 1));
            // depth: 가장자리 0 → 머무는 반경 1. 들어오는 중간부터 회전이 붙어 곡선이 보이고, 원반 근처에서는 거의 그대로다.
            float depth = Clamp01((radius - distance) / (radius - disc));
            float spin = EdgeSpin + (discSpin - EdgeSpin) * depth * (2 - depth);

            float angle = distance > CenterEpsilon ? (float)Math.Atan2(dy, dx) : spinAt * 2 * (float)Math.PI;
            angle += SpinDirection * spin * weight * delta;

            enemy.MoveTo(new Point2(
                center.X + next * (float)Math.Cos(angle),
                center.Y + next * (float)Math.Sin(angle)));
        }

        // 남은 거리 비율 x(머무는 반경에서 0, 접근 띠 바깥에서 ±1)에 대한 안쪽 이동 속도 비율. 0 근처에서 곧게 줄어 반경에 부드럽게 붙는다.
        private static float Ease(float x)
        {
            if (x >= 1)
                return 1;

            if (x <= -1)
                return -1;

            return x * (2 - Math.Abs(x));
        }

        private static float Smooth(float t) => t * t * (3 - 2 * t);

        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;

        // 적 번호·중력장 번호·용도마다 고정된 0 이상 1 미만의 값(판의 난수를 쓰지 않는다 — 이동이 난수 순서를 바꾸지 않게).
        private static float Hash01(int id, long sequence, int salt)
        {
            unchecked
            {
                ulong x = (ulong)(uint)id * 0x9E3779B97F4A7C15UL;
                x ^= (ulong)sequence * 0xC2B2AE3D27D4EB4FUL;
                x ^= (ulong)(uint)salt * 0x165667B19E3779F9UL;
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return (x >> 40) / (float)(1UL << 24);
            }
        }
    }
}
