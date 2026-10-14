using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // EnemyContentData(저작 형식) → EnemyContent(검증된 정의).
    //
    // 오류가 하나라도 있으면 null을 돌려주고, 모든 진단을 into에 더한다(부분 통과 금지).
    // 여기서 새로 두는 규칙은 데이터 모양에 관한 것뿐이다(빠진 칸, 콘텐츠에 없는 종류의 참조).
    // 수치 규칙은 정의 생성자를, 콘텐츠 전체 규칙은 EnemyContentInvariants를 그대로 호출해 경로를 붙인다.
    //
    // 두 단계로 읽는다. 앞 단계에 오류가 있으면 뒤 단계를 보지 않는다(잘못된 정의가 거짓 참조 오류를 만들지 않게).
    // 1. 개별 정의: 적 종류(색 등급·특수 성질과 그 사망 효과), 출현 배치, 주기 출현 배치. 출현 배치 둘은 필수다.
    // 2. 정의끼리의 규칙: 적 종류 유일, 공급되는 종류의 색 수, 종류 사이 연결(변환 대상), 공급(픽업 제외), 주기 출현 띠가 일반 띠에서 풀리는가.
    public static class EnemyContentLoader
    {
        public static EnemyContent Load(EnemyContentData data, List<ContentDiagnostic> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));

            if (data == null)
            {
                into.Add(new ContentDiagnostic(string.Empty, "적 콘텐츠 데이터가 null이다."));
                return null;
            }

            int errors = into.Count;

            List<EnemyDefinition> enemies = LoadEnemies(data.Enemies, into);
            EnemyPlacementDefinition placement = LoadPlacement(data.EnemyPlacement, into);
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement = LoadPeriodicSpawnPlacement(data.PeriodicSpawnPlacement, into);

            if (into.Count > errors)
                return null;

            EnemyContentInvariants.CollectEnemies(enemies, into, out Dictionary<EnemyType, EnemyDefinition> enemiesByType);
            EnemyContentInvariants.CheckTierCounts(enemies, into);
            EnemyContentInvariants.CheckKindLinks(enemies, enemiesByType, into);
            List<SupplyRequest> startSupply = LoadSupplyList(data.StartSupply, "StartSupply", enemiesByType, into);
            EnemyContentInvariants.CheckSupplyKinds(startSupply, "StartSupply", into);
            EnemyContentInvariants.CheckPeriodicSpawnPlacement(placement, periodicSpawnPlacement, into);

            if (into.Count > errors)
                return null;

            return new EnemyContent(enemies, placement, startSupply, periodicSpawnPlacement);
        }

        private static List<EnemyDefinition> LoadEnemies(List<EnemyData> items, List<ContentDiagnostic> into)
        {
            var enemies = new List<EnemyDefinition>();

            if (items == null)
                return enemies;

            for (int i = 0; i < items.Count; i++)
            {
                EnemyData item = items[i];
                string at = item == null ? $"Enemies[{i}]" : $"Enemies[{item.Type}]";

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(at, "적 데이터가 null이다."));
                    continue;
                }

                int errors = into.Count;
                List<EnemyTraitDefinition> traits = LoadTraits(item.Traits, at + ".Traits", into);
                List<EnemyTier> tiers = LoadTiers(item.Tiers, at + ".Tiers", into);

                if (into.Count > errors)
                    continue;

                EnemyDefinition enemy = Guard(at, into, () =>
                    new EnemyDefinition(item.Type, item.MoveSpeed, item.Radius, item.RadiusStep, tiers, traits, item.UpgradesTo, item.SpawnPeriod,
                        item.RainCount, item.KillTimeBonus, item.RespawnDecay, item.RainCooldown, item.RainDelay));

                if (enemy != null)
                    enemies.Add(enemy);
            }

            return enemies;
        }

        // 줄마다 수치를 검사한다. 줄 수(하나 이상)는 EnemyDefinition이, 공급되는 종류의 색 수는 EnemyContentInvariants가 검사한다.
        private static List<EnemyTier> LoadTiers(List<EnemyTierData> items, string at, List<ContentDiagnostic> into)
        {
            var tiers = new List<EnemyTier>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                EnemyTierData item = items[i];

                if (item == null)
                {
                    into.Add(new ContentDiagnostic($"{at}[{i}]", "데이터가 없다."));
                    continue;
                }

                EnemyTier? tier = GuardValue($"{at}[{i}]", into, () => new EnemyTier(item.MaxHealth, item.Gold, item.Exp));

                if (tier.HasValue)
                    tiers.Add(tier.Value);
            }

            return tiers;
        }

        // 줄마다 성질 종류가 정하는 효과를 만든다. 성질 종류 유일·픽업의 성질 수는 EnemyDefinition이 검사한다.
        private static List<EnemyTraitDefinition> LoadTraits(List<EnemyTraitData> items, string at, List<ContentDiagnostic> into)
        {
            var traits = new List<EnemyTraitDefinition>();

            for (int i = 0; items != null && i < items.Count; i++)
            {
                EnemyTraitData item = items[i];
                string itemAt = item == null ? $"{at}[{i}]" : $"{at}[{item.Type}]";

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(itemAt, "데이터가 없다."));
                    continue;
                }

                int errors = into.Count;
                DeathEffectDefinition effect = LoadDeathEffect(item.Type, item.Effect, itemAt + ".Effect", into);

                if (into.Count > errors)
                    continue;

                EnemyTraitDefinition trait = Guard(itemAt, into, () => new EnemyTraitDefinition(item.Type, effect, item.MaxActive));

                if (trait != null)
                    traits.Add(trait);
            }

            return traits;
        }

        // 성질 종류가 사망 효과를 정한다: 황금 → Golden, 전기 → 연쇄 번개, 달 → 달 버프, 레이저 → 레이저, 슈퍼노바 → 폭발, 혜성 → 혜성 버프,
        // 중력 → 중력장.
        private static DeathEffectDefinition LoadDeathEffect(EnemyTraitType type, DeathEffectData item, string at, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic(at, "사망 효과의 수치가 없다."));
                return null;
            }

            switch (type)
            {
                case EnemyTraitType.Golden:
                    return Guard(at, into, () => new GoldenDefinition(item.Multiplier, item.CritChance, item.CritRewardScale));
                case EnemyTraitType.Electric:
                    return Guard(at, into, () => new ChainLightningDefinition(
                        item.Damage, item.Radius, item.MaxTargets, item.BranchChance, item.CritChance, item.CritMultiplier));
                case EnemyTraitType.Supernova:
                    return Guard(at, into, () => new ExplosionDefinition(item.Damage, item.Radius));
                case EnemyTraitType.Laser:
                    return Guard(at, into, () => new LaserBurstDefinition(item.Damage, item.Width, item.CritChance, item.CritMultiplier));
                case EnemyTraitType.Moon:
                    return new MoonBuffDefinition();
                case EnemyTraitType.Comet:
                    return new CometBuffDefinition();
                case EnemyTraitType.Gravity:
                    return Guard(at, into, () => new GravityFieldDefinition(item.Radius, item.Force, item.Duration));
                default:
                    into.Add(new ContentDiagnostic(at, $"알 수 없는 성질 종류 {(int)type}."));
                    return null;
            }
        }

        private static EnemyPlacementDefinition LoadPlacement(EnemyPlacementData item, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic("EnemyPlacement", "데이터가 없다."));
                return null;
            }

            return Guard("EnemyPlacement", into, () => new EnemyPlacementDefinition(item.MinDistance, item.MaxDistance));
        }

        private static PeriodicSpawnPlacementDefinition LoadPeriodicSpawnPlacement(PeriodicSpawnPlacementData item, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic("PeriodicSpawnPlacement", "데이터가 없다."));
                return null;
            }

            return Guard("PeriodicSpawnPlacement", into, () => new PeriodicSpawnPlacementDefinition(item.InnerOffset, item.OuterOffset));
        }

        private static List<SupplyRequest> LoadSupplyList(
            List<SupplyData> items,
            string section,
            IReadOnlyDictionary<EnemyType, EnemyDefinition> enemies,
            List<ContentDiagnostic> into)
        {
            var requests = new List<SupplyRequest>();

            if (items == null)
                return requests;

            for (int i = 0; i < items.Count; i++)
            {
                SupplyData item = items[i];
                string at = $"{section}[{i}]";

                if (item == null)
                {
                    into.Add(new ContentDiagnostic(at, "공급 데이터가 null이다."));
                    continue;
                }

                if (item.Enemy == null)
                {
                    into.Add(new ContentDiagnostic(at + ".Enemy", "적 종류가 비어 있다."));
                    continue;
                }

                if (!enemies.TryGetValue(item.Enemy.Value, out EnemyDefinition enemy))
                {
                    into.Add(new ContentDiagnostic(at + ".Enemy", $"적 종류 목록에 없는 종류 '{item.Enemy.Value}'."));
                    continue;
                }

                SupplyRequest? request = GuardValue(at, into, () => new SupplyRequest(enemy, item.Count));

                if (request.HasValue)
                    requests.Add(request.Value);
            }

            return requests;
        }

        // ── 공통 ────────────────────────────────────────────────────────────

        // 정의 생성자의 규칙 위반을 그 자리의 진단으로 바꾼다.
        private static T Guard<T>(string at, List<ContentDiagnostic> into, Func<T> create) where T : class
        {
            try { return create(); }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(at, error.Message));
                return null;
            }
        }

        private static T? GuardValue<T>(string at, List<ContentDiagnostic> into, Func<T> create) where T : struct
        {
            try { return create(); }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(at, error.Message));
                return null;
            }
        }
    }
}
