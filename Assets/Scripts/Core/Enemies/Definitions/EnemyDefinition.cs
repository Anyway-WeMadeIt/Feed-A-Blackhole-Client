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
        public EnemyType Type { get; }

        // 공전 속도(초당 이동 거리). 0이 아닌 값이고, 부호가 공전 방향이다: 양수는 반시계, 음수는 시계방향.
        public float MoveSpeed { get; }

        // 크기 1의 반지름. 모든 색이 같다. 크기 k의 반지름은 여기에 SizeRule.RadiusMultiplier(k, RadiusStep)를 곱한다.
        public float Radius { get; }

        // 크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 0.35면 크기 2가 1.35배, 크기 3이 1.7배다.
        public float RadiusStep { get; }

        // 색 등급 표. 번호가 적의 색 등급(Enemy.Tier)이다. 공급되는 종류는 6색(EnemyContentInvariants), 픽업은 한 줄.
        public IReadOnlyList<EnemyTier> Tiers { get; }

        // 이 종류에 붙을 수 있는 특수 성질(종류마다 하나). 출현 때 성질마다의 생성 확률(판 구성, 기본 0%)로 최대 하나가 붙는다.
        // 픽업은 성질이 정확히 하나이고 언제나 붙는다(혜성 = 혜성 버프).
        public IReadOnlyList<EnemyTraitDefinition> Traits { get; }

        // 판 시작 때 이 종류의 시작 공급 중 변환 수(노드)만큼이 바뀌는 다음 종류(소행성 → 행성 → 별 → 은하). 없으면 null.
        public EnemyType? UpgradesTo { get; }

        // 주기 출현의 판정 주기(초). 0이면 공급되는 보통 종류다.
        // 주기 출현 종류는 적 공급·성장 공급·변환과 무관하다: 주기마다 등장 확률(판 구성의 SpawnChance)로 하나가 나온다.
        public float SpawnPeriod { get; }

        // 픽업(혜성): 브레이커로 쳐서 획득하는 것이며, 사망 효과의 피해를 받지 않는다(성질이 언제나 붙으므로). 질량·크기가 없다.
        // 지금은 주기 출현 종류만 픽업이다.
        public bool IsPickup => SpawnPeriod > 0;

        // 주기 출현 한 번이 혜성 비일 때 한꺼번에 나오는 수(원작 "혜성이 내릴 확률"). 0이면 혜성 비가 없다 — 혜성 비 확률 노드를 사도 하나씩 나온다.
        public int RainCount { get; }

        // 이 종류가 파괴될 때 시간 추가가 성공하면 더하는 초. 0이면 판 설정(TimeLimitDefinition.KillTimeBonus)을 쓴다.
        public float KillTimeBonus { get; }

        // 재생성 세대 감쇠(0~1). n세대(재생성으로 n번 거쳐 나온) 적의 재생성 확률 = RespawnChance × RespawnDecay^n. 0이면 1세대까지만, 1이면 감쇠 없음.
        public float RespawnDecay { get; }

        // 혜성 비 쿨타임(초): 혜성 비가 내린 뒤 이 시간 동안은 혜성 비 판정을 건너뛴다(하나씩은 나온다). 0이면 없음.
        public float RainCooldown { get; }

        // 판 시작 뒤 이 시간(초)까지는 혜성 비가 내리지 않는다. 0이면 없음.
        public float RainDelay { get; }

        public EnemyDefinition(
            EnemyType type,
            float moveSpeed,
            float radius,
            float radiusStep,
            IReadOnlyList<EnemyTier> tiers,
            IReadOnlyList<EnemyTraitDefinition> traits = null,
            EnemyType? upgradesTo = null,
            float spawnPeriod = 0,
            int rainCount = 0,
            float killTimeBonus = 0,
            float respawnDecay = 0,
            float rainCooldown = 0,
            float rainDelay = 0)
        {
            if (!Enum.IsDefined(typeof(EnemyType), type))
                throw new ArgumentOutOfRangeException(nameof(type), $"알 수 없는 적 종류 {(int)type}.");

            if (upgradesTo == type)
                throw new ArgumentException("자기 자신으로 변환할 수 없다.", nameof(upgradesTo));

            if (float.IsNaN(spawnPeriod) || float.IsInfinity(spawnPeriod) || spawnPeriod < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnPeriod), "0 이상의 유한한 값이 필요하다(0 = 주기 출현이 아님).");

            if (rainCount < 0)
                throw new ArgumentOutOfRangeException(nameof(rainCount), "0 이상이어야 한다(0 = 혜성 비가 없음).");

            if (rainCount > 0 && spawnPeriod <= 0)
                throw new ArgumentException("혜성 비 수는 주기 출현 종류에만 둘 수 있다.", nameof(rainCount));

            if (float.IsNaN(radiusStep) || float.IsInfinity(radiusStep) || radiusStep < 0)
                throw new ArgumentOutOfRangeException(nameof(radiusStep), "0 이상의 유한한 값이 필요하다(0 = 크기가 반지름을 바꾸지 않음).");

            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("색 등급이 하나 이상 필요하다.", nameof(tiers));

            var traitTypes = new HashSet<EnemyTraitType>();

            for (int i = 0; traits != null && i < traits.Count; i++)
            {
                if (traits[i] == null)
                    throw new ArgumentException($"성질 {i}가 null이다.", nameof(traits));

                if (!traitTypes.Add(traits[i].Type))
                    throw new ArgumentException($"성질 '{traits[i].Type}'가 중복됐다.", nameof(traits));
            }

            if (spawnPeriod > 0)
            {
                if (traits == null || traits.Count != 1)
                    throw new ArgumentException("픽업은 성질이 정확히 하나여야 한다(언제나 붙는 효과).", nameof(traits));

                if (upgradesTo.HasValue)
                    throw new ArgumentException("픽업은 변환 대상을 가질 수 없다.", nameof(upgradesTo));
            }

            Type = type;
            MoveSpeed = DefinitionGuard.NonZeroFinite(moveSpeed, nameof(moveSpeed));
            Radius = DefinitionGuard.Positive(radius, nameof(radius));
            RadiusStep = radiusStep;
            Tiers = Array.AsReadOnly(Copy(tiers));
            Traits = traits == null ? Array.AsReadOnly(Array.Empty<EnemyTraitDefinition>()) : Array.AsReadOnly(Copy(traits));
            UpgradesTo = upgradesTo;
            SpawnPeriod = spawnPeriod;
            RainCount = rainCount;

            if (float.IsNaN(killTimeBonus) || float.IsInfinity(killTimeBonus) || killTimeBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(killTimeBonus), "0 이상의 유한한 값이 필요하다(0 = 판 설정).");

            KillTimeBonus = killTimeBonus;

            if (float.IsNaN(respawnDecay) || respawnDecay < 0 || respawnDecay > 1)
                throw new ArgumentOutOfRangeException(nameof(respawnDecay), "0 이상 1 이하가 필요하다(0 = 1세대까지만).");

            RespawnDecay = respawnDecay;

            if (float.IsNaN(rainCooldown) || float.IsInfinity(rainCooldown) || rainCooldown < 0)
                throw new ArgumentOutOfRangeException(nameof(rainCooldown), "0 이상의 유한한 값이 필요하다.");

            if (float.IsNaN(rainDelay) || float.IsInfinity(rainDelay) || rainDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(rainDelay), "0 이상의 유한한 값이 필요하다.");

            RainCooldown = rainCooldown;
            RainDelay = rainDelay;
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
                throw new ArgumentException($"'{trait.Type}'는 이 판 구성에서 '{Type}'의 성질이 아니다.", nameof(trait));

            if (tier < 0 || tier >= Tiers.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(tier), $"'{Type}'의 색 등급은 0부터 {Tiers.Count - 1}까지다. 받은 값: {tier}.");

            if (size < SizeRule.Base || size > composition.Size)
                throw new ArgumentOutOfRangeException(
                    nameof(size), $"이 판 구성에서 '{Type}'의 크기는 {SizeRule.Base}부터 {composition.Size}까지다. 받은 값: {size}.");

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
