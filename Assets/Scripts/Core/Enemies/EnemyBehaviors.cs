using System;

namespace BlackHole.Core
{
    internal static class EnemyBehaviors
    {
        // 기본 공전: 블랙홀(원점)을 중심으로 반지름을 지키며 돈다.
        public static Point2 NextPosition(Point2 position, EnemyStats stats, float delta) =>
            NextPosition(position, stats, delta, OrbitShape.Default, 0);

        // 모양이 있는 공전(OrbitShape): 기울기(Tilt)만큼 되돌려 돌리고 가로를 Stretch로 나눈 "원 좌표"에서 원운동을 한 뒤 다시 늘리고 돌린다.
        // 그래서 궤도는 Tilt만큼 기울어진, 가로축이 Stretch배인 타원이다. 공전 속도는 Speed배, 반지름은 초마다 Inward 비율만큼 줄되 minRadius 아래로는 가지 않는다.
        // 중력장·끌림이 옮긴 자리도 그대로 받는다 — 자리만 보고 다음 자리를 정한다.
        public static Point2 NextPosition(Point2 position, EnemyStats stats, float delta, OrbitShape orbit, float minRadius)
        {
            Point2 center = BattleSpace.Origin;
            float dx = position.X - center.X;
            float dy = position.Y - center.Y;

            if (!orbit.IsDefault)
            {
                // 원 좌표로: 기울기를 되돌리고 가로를 나눈다.
                float tilt = -orbit.Tilt * (float)Math.PI / 180;
                float cos = (float)Math.Cos(tilt);
                float sin = (float)Math.Sin(tilt);
                float ux = (dx * cos - dy * sin) / orbit.Stretch;
                float uy = dx * sin + dy * cos;
                dx = ux;
                dy = uy;
            }

            float radius = (float)Math.Sqrt(dx * dx + dy * dy);

            if (radius == 0)
                return position;

            float angle = (float)Math.Atan2(dy, dx);
            float turn = stats.MoveSpeed * orbit.Speed / radius * delta;

            if (orbit.Inward > 0)
                radius = Math.Max(Math.Min(radius, minRadius), radius * (1 - orbit.Inward * delta));

            float nx = radius * (float)Math.Cos(angle + turn);
            float ny = radius * (float)Math.Sin(angle + turn);

            if (!orbit.IsDefault)
            {
                // 다시 가로를 늘리고 기울인다.
                float tilt = orbit.Tilt * (float)Math.PI / 180;
                float cos = (float)Math.Cos(tilt);
                float sin = (float)Math.Sin(tilt);
                float sx = nx * orbit.Stretch;
                nx = sx * cos - ny * sin;
                ny = sx * sin + ny * cos;
            }

            return new Point2(center.X + nx, center.Y + ny);
        }
    }
}
