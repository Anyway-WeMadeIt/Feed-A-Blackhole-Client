using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 전투 시작 시, 산 노드의 수치 값으로만 결정되는 적 한 종류의 구성 값:
    // 질량과 그로 정해지는 색 비율,
    // 크기,
    // 더할 시작 공급 수,
    // Level업마다의 성장 공급 %(이 판 시작 수 대비),
    // 판 시작 때 다음 종류로 바꾸는 수,
    // 특수 성질마다의 생성 확률과 노드가 반영된 성질,
    // 파괴될 때의 재생성·시간 추가 확률,
    // 픽업이면 등장 확률과 혜성 비 확률.
    public readonly struct EnemyComposition
    {
        private static readonly IReadOnlyList<EnemyTraitDefinition> NoTraits = Array.AsReadOnly(Array.Empty<EnemyTraitDefinition>());
        private static readonly IReadOnlyList<float> NoChances = Array.AsReadOnly(Array.Empty<float>());

        // 질량(%, 기본 100). 색 비율(TierRatios)을 정한다(MassRule).
        public float Mass { get; }

        // 색 등급마다 나오는 비율(종류의 색 등급 표 순서, 합 1). 질량으로 정해진다.
        public IReadOnlyList<float> TierRatios { get; }

        // 크기(기본 1). 크기 1부터 이 값까지가 같은 몫으로 섞여 나온다(SizeRule).
        public int Size { get; }

        // 콘텐츠의 전투 시작 공급에 더해 이 종류를 몇 마리 더 공급하는가.
        public int StartSupplyBonus { get; }

        // 블랙홀이 Level업할 때마다 이 종류를 이 판 시작 수(변환 반영)의 몇 % 요청하는가(0 이상, 100 = 시작 수만큼). 노드를 사야 0보다 크다.
        // 마리 수는 판 조립이 시작 수와 곱해 반올림한다(GameSessionFactory). %로 두는 것은 소수 오차 없이 곱하기 위해서다.
        public float GrowthPercent { get; }

        // 판 시작 때 이 종류의 시작 공급 중 변환 대상 종류(EnemyDefinition.UpgradesTo)로 바꾸는 수(0 이상). 노드를 사야 0보다 크다.
        public int UpgradeCount { get; }

        // 이 판의 성질(종류의 성질 순서, 노드가 반영된 수치 — 예: 황금 배율). 출현한 적은 이 객체를 받는다.
        public IReadOnlyList<EnemyTraitDefinition> Traits { get; }

        // Traits[i]가 붙는 확률(0 ~ 1). 합은 1 이하다. 기본 0 — 확률 노드를 사야 붙는다. 픽업은 쓰지 않는다(성질이 언제나 붙는다).
        public IReadOnlyList<float> TraitChances { get; }

        // 주기 출현 종류면: 출현 주기마다 하나가 나올 확률(0 ~ 1). 기본 0 — 확률 노드를 사야 나온다. 아니면 0이다.
        public float SpawnChance { get; }

        // 주기 출현 종류면: 나올 때 혜성 비(종류의 RainCount만큼 한꺼번에)가 될 확률(0 ~ 1). 기본 0.
        public float RainChance { get; }

        // 이 종류가 파괴될 때 같은 종류를 하나 새로 요청할 확률(0 ~ 1). 기본 0. 픽업은 0이다.
        public float RespawnChance { get; }

        // 이 종류가 파괴될 때 판의 제한 시간이 늘어날 확률(0 ~ 1). 기본 0. 픽업은 0이다.
        public float TimeChance { get; }

        public EnemyComposition(
            float mass,
            IReadOnlyList<float> tierRatios,
            int size = SizeRule.Base,
            int startSupplyBonus = 0,
            float growthPercent = 0,
            int upgradeCount = 0,
            IReadOnlyList<EnemyTraitDefinition> traits = null,
            IReadOnlyList<float> traitChances = null,
            float spawnChance = 0,
            float rainChance = 0,
            float respawnChance = 0,
            float timeChance = 0)
        {
            if (tierRatios == null || tierRatios.Count == 0)
                throw new ArgumentException("색 비율이 하나 이상 필요하다.", nameof(tierRatios));

            if ((traits?.Count ?? 0) != (traitChances?.Count ?? 0))
                throw new ArgumentException("성질 수와 성질 확률 수가 다르다.", nameof(traitChances));

            if (float.IsNaN(growthPercent) || float.IsInfinity(growthPercent) || growthPercent < 0)
                throw new ArgumentOutOfRangeException(nameof(growthPercent), "0 이상의 유한한 값이 필요하다.");

            if (upgradeCount < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeCount), "0 이상이어야 한다.");

            Mass = mass;
            TierRatios = tierRatios;
            Size = size;
            StartSupplyBonus = startSupplyBonus;
            GrowthPercent = growthPercent;
            UpgradeCount = upgradeCount;
            Traits = traits ?? NoTraits;
            TraitChances = traitChances ?? NoChances;
            SpawnChance = spawnChance;
            RainChance = rainChance;
            RespawnChance = respawnChance;
            TimeChance = timeChance;
        }

        // 성질 확률의 합.
        public float TraitChanceSum
        {
            get
            {
                float sum = 0;

                for (int i = 0; i < TraitChances.Count; i++)
                    sum += TraitChances[i];

                return sum;
            }
        }

        // 이 판 구성의 성질 중 trait(같은 객체)의 번호. 없으면 -1.
        public int IndexOfTrait(EnemyTraitDefinition trait)
        {
            for (int i = 0; i < Traits.Count; i++)
            {
                if (ReferenceEquals(Traits[i], trait))
                    return i;
            }

            return -1;
        }

        // 노드를 하나도 사지 않은 판 구성: 질량 100%(첫 색만), 크기 1, 성질은 종류의 것 그대로, 확률은 모두 0.
        public static EnemyComposition Base(EnemyDefinition kind) =>
            new(MassRule.Base, RatiosOf(kind, MassRule.Base),
                traits: kind.Traits, traitChances: new float[kind.Traits.Count]);

        // 산 노드의 수치 값으로 이 종류의 판 구성을 계산한다. 종류마다 닿는 수치는 EnemyUpgradeStats가 정한다.
        // 질량은 수치 값 그대로, 나머지는 기본값(크기 1, 확률·공급 0)에 늘어난 양을 더한다.
        public static EnemyComposition From(EnemyDefinition kind, UpgradeStatValues upgrades)
        {
            EnemyType type = kind.Type;
            float Gain(UpgradeStat? stat) => stat.HasValue ? upgrades.GainOf(stat.Value) : 0;

            UpgradeStat? massStat = EnemyUpgradeStats.Mass(type);
            float mass = massStat.HasValue ? upgrades.ValueOf(massStat.Value) : MassRule.Base;
            int size = Whole(SizeRule.Base + Gain(EnemyUpgradeStats.Size(type)));
            int startSupply = Whole(Gain(EnemyUpgradeStats.StartSupply(type)));
            float growth = NotNegative(Gain(EnemyUpgradeStats.GrowthSupply(type)), type, "성장 공급 %");
            int upgrade = Whole(Gain(EnemyUpgradeStats.Upgrade(type)));
            float spawnChance = Percent(Gain(EnemyUpgradeStats.SpawnChance(type)), type, "등장 확률");
            float rain = Percent(Gain(EnemyUpgradeStats.RainChance(type)), type, "혜성 비 확률");
            float respawn = Percent(Gain(EnemyUpgradeStats.RespawnChance(type)), type, "재생성 확률");
            float time = Percent(Gain(EnemyUpgradeStats.TimeChance(type)), type, "시간 추가 확률");

            if (float.IsNaN(mass) || float.IsInfinity(mass) || mass < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Type}'의 질량은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {mass}%.");

            if (size < SizeRule.Base || size > SizeRule.Max)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Type}'의 크기는 {SizeRule.Base}부터 {SizeRule.Max}까지다. 업그레이드 합: {size}.");

            if (upgrade < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Type}'의 변환 수는 0 이상이어야 한다. 업그레이드 합: {upgrade}.");

            // 변환 대상이 없는 종류는 바꿔 줄 종류가 없다. 판 조립과 로드 때의 검사(UpgradeContentCheck)가 이 예외를 본다.
            if (upgrade > 0 && !kind.UpgradesTo.HasValue)
                throw new ArgumentException($"'{kind.Type}'에는 변환 대상이 없어 변환 수를 둘 수 없다. 업그레이드 합: {upgrade}.", nameof(upgrades));

            // 픽업은 공급되지 않고 주기마다 등장 확률로 나온다. 공급·질량·크기 노드와 등장 확률은 서로의 종류에만 뜻이 있다.
            if (kind.IsPickup && (startSupply > 0 || growth > 0))
                throw new ArgumentException($"'{kind.Type}'는 픽업이라 공급되지 않는다. 공급 수 노드를 둘 수 없다.", nameof(upgrades));

            if (kind.IsPickup && (mass != MassRule.Base || size != SizeRule.Base))
                throw new ArgumentException($"'{kind.Type}'는 픽업이라 질량·크기가 없다. 질량·크기 노드를 둘 수 없다.", nameof(upgrades));

            if (!kind.IsPickup && spawnChance > 0)
                throw new ArgumentException($"'{kind.Type}'는 픽업이 아니라 등장 확률을 둘 수 없다. 특수 성질은 성질 확률(trait.<성질>.chance)을 쓴다.", nameof(upgrades));

            if (!kind.IsPickup && rain > 0)
                throw new ArgumentException($"'{kind.Type}'는 픽업이 아니라 혜성 비 확률을 둘 수 없다.", nameof(upgrades));

            if (kind.IsPickup && (respawn > 0 || time > 0))
                throw new ArgumentException($"'{kind.Type}'는 픽업이라 파괴 때의 재생성·시간 추가 확률을 둘 수 없다.", nameof(upgrades));

            var traits = new EnemyTraitDefinition[kind.Traits.Count];
            var chances = new float[kind.Traits.Count];
            float chanceSum = 0;

            for (int i = 0; i < traits.Length; i++)
            {
                EnemyTraitDefinition trait = kind.Traits[i];
                traits[i] = Upgraded(kind, trait, upgrades);

                // 픽업의 성질은 언제나 붙으므로 확률이 없다.
                if (kind.IsPickup)
                    continue;

                chances[i] = Percent(Gain(EnemyUpgradeStats.TraitChance(type, trait.Type)), type, $"'{trait.Type}' 성질 확률");
                chanceSum += chances[i];
            }

            // 성질은 한 마리에 하나다(배타). 합이 100%를 넘으면 어느 성질도 약속한 몫을 받을 수 없다.
            if (chanceSum > 1 + 1e-4f)
                throw new ArgumentException($"'{kind.Type}'의 성질 확률 합이 100%를 넘는다({chanceSum * 100:0.##}%).", nameof(upgrades));

            return new EnemyComposition(mass, RatiosOf(kind, mass), size, startSupply, growth, upgrade,
                Array.AsReadOnly(traits), Array.AsReadOnly(chances), spawnChance, rain, respawn, time);
        }

        private static IReadOnlyList<float> RatiosOf(EnemyDefinition kind, float mass) =>
            Array.AsReadOnly(MassRule.TierRatios(mass, kind.Tiers.Count));

        // 노드가 성질의 수치를 바꾸는 것: 사망 효과의 수치(황금 배율, 번개·레이저·폭발)와 동시 상한.
        // 바뀌지 않으면 종류의 성질 객체를 그대로 쓴다.
        private static EnemyTraitDefinition Upgraded(EnemyDefinition kind, EnemyTraitDefinition trait, UpgradeStatValues upgrades)
        {
            EnemyTraitDefinition upgraded = trait;
            DeathEffectDefinition effect = UpgradedEffect(kind, trait, upgrades);

            if (effect != null)
                upgraded = upgraded.With(effect);

            UpgradeStat? maxActiveStat = EnemyUpgradeStats.TraitMaxActive(kind.Type, trait.Type);
            int maxActive = Whole(trait.MaxActive + (maxActiveStat.HasValue ? upgrades.GainOf(maxActiveStat.Value) : 0));

            if (maxActive < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"'{kind.Type}'의 '{trait.Type}' 동시 상한은 0 이상이어야 한다. 업그레이드 합: {maxActive}.");

            if (maxActive != trait.MaxActive)
                upgraded = upgraded.WithMaxActive(maxActive);

            return upgraded;
        }

        // 노드가 반영된 사망 효과. 수치가 하나도 바뀌지 않았으면 null이다.
        // 피해·횟수·시간은 늘어난 양을, 확률·배율은 늘어난 %p ÷ 100을 더하고,
        // 너비·폭발과 중력장 반지름·중력장 힘은 (1 + 늘어난 %p ÷ 100)배 한다.
        // 확률·비율은 1을 넘지 않게 자른다. 그 밖의 한계 밖 값(0 이하의 피해·너비·반지름 등)은 효과 정의의 예외다 — UpgradeContentCheck가 로드 때 찾는다.
        private static DeathEffectDefinition UpgradedEffect(EnemyDefinition kind, EnemyTraitDefinition trait, UpgradeStatValues upgrades)
        {
            float Gain(Func<EnemyType, EnemyTraitType, UpgradeStat?> statOf)
            {
                UpgradeStat? stat = statOf(kind.Type, trait.Type);
                return stat.HasValue ? upgrades.GainOf(stat.Value) : 0;
            }

            float Add(Func<EnemyType, EnemyTraitType, UpgradeStat?> statOf, float baseValue) => baseValue + Gain(statOf);
            float AddPercent(Func<EnemyType, EnemyTraitType, UpgradeStat?> statOf, float baseValue) => baseValue + Gain(statOf) / 100;
            float Scale(Func<EnemyType, EnemyTraitType, UpgradeStat?> statOf, float baseValue) => baseValue * (1 + Gain(statOf) / 100);

            switch (trait.Effect)
            {
                case GoldenDefinition golden:
                {
                    float multiplier = AddPercent(EnemyUpgradeStats.TraitMultiplier, golden.Multiplier);
                    float crit = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitCritChance, golden.CritChance));
                    float critReward = AddPercent(EnemyUpgradeStats.TraitCritRewardScale, golden.CritRewardScale);

                    bool same = multiplier == golden.Multiplier && crit == golden.CritChance && critReward == golden.CritRewardScale;
                    return same ? null : new GoldenDefinition(multiplier, crit, critReward);
                }
                case ChainLightningDefinition chain:
                {
                    float damage = Add(EnemyUpgradeStats.TraitDamage, chain.Damage);
                    float radius = Scale(EnemyUpgradeStats.TraitRadius, chain.Radius);
                    int maxTargets = Whole(Add(EnemyUpgradeStats.TraitMaxTargets, chain.MaxTargets));
                    float branch = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitBranchChance, chain.BranchChance));
                    float crit = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitCritChance, chain.CritChance));
                    float critMultiplier = AddPercent(EnemyUpgradeStats.TraitCritMultiplier, chain.CritMultiplier);

                    bool same = damage == chain.Damage && radius == chain.Radius && maxTargets == chain.MaxTargets
                                && branch == chain.BranchChance && crit == chain.CritChance && critMultiplier == chain.CritMultiplier;
                    return same ? null : new ChainLightningDefinition(damage, radius, maxTargets, branch, crit, critMultiplier);
                }
                case StarLightningDefinition star:
                {
                    float damage = Add(EnemyUpgradeStats.TraitDamage, star.Damage);
                    float radius = Scale(EnemyUpgradeStats.TraitRadius, star.Radius);
                    int maxTargets = Whole(Add(EnemyUpgradeStats.TraitMaxTargets, star.MaxTargets));
                    float branch = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitBranchChance, star.BranchChance));
                    float crit = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitCritChance, star.CritChance));
                    float critMultiplier = AddPercent(EnemyUpgradeStats.TraitCritMultiplier, star.CritMultiplier);

                    bool same = damage == star.Damage && radius == star.Radius && maxTargets == star.MaxTargets
                                && branch == star.BranchChance && crit == star.CritChance && critMultiplier == star.CritMultiplier;
                    return same ? null : new StarLightningDefinition(damage, radius, maxTargets, star.Branches, branch, crit, critMultiplier);
                }
                case LaserBurstDefinition laser:
                {
                    float damage = Add(EnemyUpgradeStats.TraitDamage, laser.Damage);
                    float width = Scale(EnemyUpgradeStats.TraitWidth, laser.Width);
                    float crit = Math.Min(1, AddPercent(EnemyUpgradeStats.TraitCritChance, laser.CritChance));
                    float critMultiplier = AddPercent(EnemyUpgradeStats.TraitCritMultiplier, laser.CritMultiplier);

                    bool same = damage == laser.Damage && width == laser.Width && crit == laser.CritChance && critMultiplier == laser.CritMultiplier;
                    return same ? null : new LaserBurstDefinition(damage, width, crit, critMultiplier);
                }
                case ExplosionDefinition explosion:
                {
                    float damage = Add(EnemyUpgradeStats.TraitDamage, explosion.Damage);
                    float radius = Scale(EnemyUpgradeStats.TraitRadius, explosion.Radius);

                    bool same = damage == explosion.Damage && radius == explosion.Radius;
                    return same ? null : new ExplosionDefinition(damage, radius);
                }
                case GravityFieldDefinition gravity:
                {
                    float radius = Scale(EnemyUpgradeStats.TraitRadius, gravity.Radius);
                    float force = Scale(EnemyUpgradeStats.TraitForce, gravity.Force);
                    float duration = Add(EnemyUpgradeStats.TraitDuration, gravity.Duration);

                    bool same = radius == gravity.Radius && force == gravity.Force && duration == gravity.Duration;
                    return same ? null : new GravityFieldDefinition(radius, force, duration);
                }
                default:
                    return null;
            }
        }

        private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        // 상한 없는 0 이상의 값(성장 공급 %는 100을 넘을 수 있다).
        private static float NotNegative(float value, EnemyType kind, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), $"'{kind}'의 {label}는 0 이상의 유한한 값이어야 한다. 업그레이드 합: {value}.");
            }

            return value;
        }

        private static float Percent(float value, EnemyType kind, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), $"'{kind}'의 {label}은 0 이상의 유한한 값이어야 한다. 업그레이드 합: {value}.");
            }

            return Math.Min(1, value / 100);
        }
    }
}
