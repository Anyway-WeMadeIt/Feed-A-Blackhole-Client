using System;

namespace BlackHole.Core
{
    // 출현 위치의 공유 정의:
    // - HQ(원점)를 둘러싼 원형 띠. 적은 띠 안의 무작위 지점에 나온다.
    // - 저작 값은 성장도 0(전장 배율 1)의 띠다. 판은 그 판의 전장 배율만큼 넓힌 띠를 쓴다(Scaled, SessionAssembler).
    public sealed class EnemyPlacementDefinition
    {
        public float MinDistance { get; }
        public float MaxDistance { get; }

        public EnemyPlacementDefinition(float minDistance, float maxDistance)
        {
            if (float.IsNaN(minDistance) || float.IsInfinity(minDistance) || minDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(minDistance), "0 이상의 유한한 값이 필요하다.");

            MaxDistance = DefinitionGuard.Positive(maxDistance, nameof(maxDistance));

            if (minDistance > maxDistance)
                throw new ArgumentOutOfRangeException(nameof(minDistance), "최대 거리보다 클 수 없다.");

            MinDistance = minDistance;
        }

        // 두 반지름에 scale(양수)을 곱한 띠.
        public EnemyPlacementDefinition Scaled(float scale)
        {
            DefinitionGuard.Positive(scale, nameof(scale));
            return new EnemyPlacementDefinition(MinDistance * scale, MaxDistance * scale);
        }

        internal Point2 Pick(BattleRandom random)
        {
            float angle = random.NextFloat() * 2 * (float)Math.PI;
            float min2 = MinDistance * MinDistance;
            float max2 = MaxDistance * MaxDistance;
            float distance = (float)Math.Sqrt(min2 + random.NextFloat() * (max2 - min2));
            Point2 center = BattleSpace.Origin;

            return new Point2(
                center.X + distance * (float)Math.Cos(angle),
                center.Y + distance * (float)Math.Sin(angle));
        }
    }
}
