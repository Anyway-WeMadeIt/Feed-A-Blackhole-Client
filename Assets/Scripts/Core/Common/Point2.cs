using System;

namespace BlackHole.Core
{
    // 규칙에서 쓰는 평면 좌표. Unity 좌표와의 변환은 호스트의 책임이다.
    public readonly struct Point2 : IEquatable<Point2>
    {
        public float X { get; }
        public float Y { get; }

        public Point2(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x), "좌표는 유한해야 한다.");
            X = x;
            Y = y;
        }

        public float DistanceSquared(Point2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(Point2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Point2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X}, {Y})";
    }
}
