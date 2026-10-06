using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 한 종류의 정의:
    // - 이동 속도(부호는 공전 방향),
    // - 반지름(크기 1의 반지름)과 크기 1당 반지름 증가분,
    // - 색 등급 표(색마다의 베이스 HP·Gold·EXP),
    // - 붙을 수 있는 특수 성질(황금·전기·달·레이저·슈퍼노바 …),
    // - 변환 대상, 픽업이면 등장 주기.
    // 어떤 색이 어떤 비율로 나오는지는 그 종류의 질량(MassRule), 어떤 크기가 나오는지는 그 종류의 크기(SizeRule)가 정한다.
    // 둘 다 판 구성(EnemyComposition)의 값이고, 종류마다 따로다(소행성 노드는 행성·별에 닿지 않는다).
    public sealed class EnemyDefinition
    {
        public string Id { get; }

        // 공전 속도(초당 이동 거리). 0이 아닌 값이고, 부호가 공전 방향이다: 양수는 반시계, 음수는 시계방향.
        public float MoveSpeed { get; }

        // 크기 1의 반지름. 모든 색이 같다. 크기 k의 반지름은 여기에 SizeRule.RadiusMultiplier(k, RadiusStep)를 곱한다.
        public float Radius { get; }

        // 크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 0.35면 크기 2가 1.35배, 크기 3이 1.7배다.
        public float RadiusStep { get; }

        // 색 등급 표. 번호가 적의 색 등급(Enemy.Tier)이다. 공급되는 종류는 7색(EnemyContentInvariants), 픽업은 한 줄.
        public IReadOnlyList<EnemyTier> Tiers { get; }

        // 이 종류에 붙을 수 있는 특수 성질(ID 유일). 출현 때 성질마다의 생성 확률(판 구성, 기본 0%)로 최대 하나가 붙는다.
        // 픽업은 성질이 정확히 하나이고 언제나 붙는다(혜성 = 혜성 버프).
        public IReadOnlyList<EnemyTraitDefinition> Traits { get; }

        // 이 종류의 생성 요청 중 변환 비율(노드)만큼이 나오는 다음 종류의 ID(소행성 → 행성 → 별).
        public string UpgradesTo { get; }

        // 픽업의 등장 판정 주기(초). 0이면 공급되는 보통 종류다.
        // 픽업(혜성)은 적 공급·성장 공급·변환·전체 개체 수 상한과 무관하다: 주기마다 등장 확률(판 구성의 AppearChance)로 하나가 나온다.
        // 브레이커로 쳐서 획득하는 것이며, 사망 효과의 피해를 받지 않는다(성질이 언제나 붙으므로). 질량·크기가 없다.
        public float PickupPeriod { get; }

        public bool IsPickup => PickupPeriod > 0;

        public EnemyDefinition(
            string id,
            float moveSpeed,
            float radius,
            float radiusStep,
            IReadOnlyList<EnemyTier> tiers,
            IReadOnlyList<EnemyTraitDefinition> traits = null,
            string upgradesTo = null,
            float pickupPeriod = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID가 비어 있다.", nameof(id));

            if (upgradesTo == id)
                throw new ArgumentException("자기 자신으로 변환할 수 없다.", nameof(upgradesTo));

            if (float.IsNaN(pickupPeriod) || float.IsInfinity(pickupPeriod) || pickupPeriod < 0)
                throw new ArgumentOutOfRangeException(nameof(pickupPeriod), "0 이상의 유한한 값이 필요하다(0 = 픽업이 아님).");

            if (float.IsNaN(radiusStep) || float.IsInfinity(radiusStep) || radiusStep < 0)
                throw new ArgumentOutOfRangeException(nameof(radiusStep), "0 이상의 유한한 값이 필요하다(0 = 크기가 반지름을 바꾸지 않음).");

            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("색 등급이 하나 이상 필요하다.", nameof(tiers));

            var traitIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; traits != null && i < traits.Count; i++)
            {
                if (traits[i] == null)
                    throw new ArgumentException($"성질 {i}가 null이다.", nameof(traits));

                if (!traitIds.Add(traits[i].Id))
                    throw new ArgumentException($"성질 ID '{traits[i].Id}'가 중복됐다.", nameof(traits));
            }

            if (pickupPeriod > 0)
            {
                if (traits == null || traits.Count != 1)
                    throw new ArgumentException("픽업은 성질이 정확히 하나여야 한다(언제나 붙는 효과).", nameof(traits));

                if (!string.IsNullOrEmpty(upgradesTo))
                    throw new ArgumentException("픽업은 변환 대상을 가질 수 없다.", nameof(upgradesTo));
            }

            Id = id;
            MoveSpeed = DefinitionGuard.NonZeroFinite(moveSpeed, nameof(moveSpeed));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
            RadiusStep = radiusStep;
            Tiers = Array.AsReadOnly(Copy(tiers));
            Traits = traits == null ? Array.AsReadOnly(Array.Empty<EnemyTraitDefinition>()) : Array.AsReadOnly(Copy(traits));
            UpgradesTo = string.IsNullOrEmpty(upgradesTo) ? null : upgradesTo;
            PickupPeriod = pickupPeriod;
        }

        // 판 구성 composition에서 색 등급 tier·크기 size(1부터)·성질 trait(없으면 null)의 실행 수치.
        // HP   = 색의 HP × 크기 배율,
        // Gold = 색의 Gold × 크기 배율(반올림),
        // EXP  = 색의 EXP × 크기 배율(반올림),
        // 반지름 = 종류의 반지름 × 크기의 반지름 배율(종류의 증가분), 속도 = 종류의 속도.
        // 성질이 황금이면 Gold에 판 구성의 황금 배율을 한 번 더 곱한다(반올림). 다른 성질은 수치를 바꾸지 않는다.
        // trait는 판 구성의 성질(composition.Traits)이어야 한다 — 노드가 반영된 배율이 거기 있다.
        public EnemyStats StatsAt(EnemyComposition composition, int tier, EnemyTraitDefinition trait = null, int size = SizeRule.Base)
        {
            if (trait != null && composition.IndexOfTrait(trait) < 0)
                throw new ArgumentException($"'{trait.Id}'는 이 판 구성에서 '{Id}'의 성질이 아니다.", nameof(trait));

            if (tier < 0 || tier >= Tiers.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(tier), $"'{Id}'의 색 등급은 0부터 {Tiers.Count - 1}까지다. 받은 값: {tier}.");

            if (size < SizeRule.Base || size > composition.Size)
                throw new ArgumentOutOfRangeException(
                    nameof(size), $"이 판 구성에서 '{Id}'의 크기는 {SizeRule.Base}부터 {composition.Size}까지다. 받은 값: {size}.");

            EnemyTier row = Tiers[tier];
            float scale = SizeRule.StatMultiplier(size);
            long gold = Multiply(row.Gold, scale);

            if (trait?.Effect is GoldenDefinition golden)
                gold = Multiply(gold, golden.Multiplier);

            return new EnemyStats(
                row.MaxHealth * scale,
                MoveSpeed,
                Radius * SizeRule.RadiusMultiplier(size, RadiusStep),
                gold,
                Multiply(row.Exp, scale));
        }

        private static long Multiply(long value, double multiplier) =>
            checked((long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];

            return copy;
        }
    }
}
