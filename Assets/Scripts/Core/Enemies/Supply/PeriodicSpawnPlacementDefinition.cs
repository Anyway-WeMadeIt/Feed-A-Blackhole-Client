using System;

namespace BlackHole.Core
{
    // 주기 출현 종류(혜성)의 출현 띠: 일반 출현 띠의 바깥 반지름을 기준으로 한 오프셋 두 개.
    // 띠 = [일반 바깥 반지름 + InnerOffset, 일반 바깥 반지름 + OuterOffset]. 음수는 안쪽이다.
    // 일반 띠를 기준으로 하므로 일반 띠가 바뀌어도(예: 성장도에 따라) 혜성 띠가 따라간다. 띠는 소환 때마다 그때의 일반 띠로 푼다(Resolve).
    // 혜성 띠가 일반 띠보다 바깥에 있는지, 겹치는지는 강제하지 않는다 — 오프셋을 고르는 저작자의 몫이다.
    // 오프셋의 저작 값도 성장도 0(전장 배율 1)의 것이다. 판은 일반 띠와 같은 배율로 넓힌 오프셋을 쓴다(Scaled).
    public sealed class PeriodicSpawnPlacementDefinition
    {
        public float InnerOffset { get; }
        public float OuterOffset { get; }

        public PeriodicSpawnPlacementDefinition(float innerOffset, float outerOffset)
        {
            if (float.IsNaN(innerOffset) || float.IsInfinity(innerOffset))
                throw new ArgumentOutOfRangeException(nameof(innerOffset), "유한한 값이 필요하다.");

            if (float.IsNaN(outerOffset) || float.IsInfinity(outerOffset))
                throw new ArgumentOutOfRangeException(nameof(outerOffset), "유한한 값이 필요하다.");

            if (innerOffset > outerOffset)
                throw new ArgumentOutOfRangeException(nameof(innerOffset), "바깥 오프셋보다 클 수 없다.");

            InnerOffset = innerOffset;
            OuterOffset = outerOffset;
        }

        // 두 오프셋에 scale(양수)을 곱한 정의.
        public PeriodicSpawnPlacementDefinition Scaled(float scale)
        {
            DefinitionGuard.Positive(scale, nameof(scale));
            return new PeriodicSpawnPlacementDefinition(InnerOffset * scale, OuterOffset * scale);
        }

        // 일반 출현 띠 normal에서 푼 혜성 출현 띠. 안쪽 반지름은 0 아래로 내려가지 않는다.
        // 바깥 반지름이 0 이하가 되면(일반 띠가 오프셋보다 작다) 띠가 될 수 없어 예외다.
        public EnemyPlacementDefinition Resolve(EnemyPlacementDefinition normal)
        {
            if (normal == null)
                throw new ArgumentNullException(nameof(normal));

            float outer = normal.MaxDistance + OuterOffset;

            if (outer <= 0)
                throw new ArgumentOutOfRangeException(nameof(normal),
                    $"일반 띠 바깥 반지름({normal.MaxDistance}) + 바깥 오프셋({OuterOffset})이 0 이하라 띠가 될 수 없다.");

            float inner = Math.Max(0, normal.MaxDistance + InnerOffset);
            return new EnemyPlacementDefinition(Math.Min(inner, outer), outer);
        }
    }
}
