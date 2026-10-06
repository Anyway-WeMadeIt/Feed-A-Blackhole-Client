using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // [임시 다리] 시트 수치(UpgradeStats)의 값을 전투 쪽이 지금 읽는 옛 업그레이드 표(UpgradeTable, 옛 수치 이름)로 옮긴다.
    // 전투 쪽이 시트 수치(UpgradeStatValues)를 직접 읽게 되면 지운다.
    //
    // 넘기는 것은 "기본값에서 늘어난 양"(하한·상한으로 자른 값 − 기본값)이다. 기본값 자체는 전투 쪽 콘텐츠(Skills·적 종류 시트)의 값을 그대로 쓴다.
    // 단위는 옛 표의 것으로 바꾼다: 옛 표의 확률·보너스는 0.25 = 25%(× 0.01), 적 성질·픽업 확률은 25 = 25%(EnemyComposition이 ÷100).
    // 범위·속도(기본 100%)는 옛 표의 Percent(기본값 × (1 + 늘어난 비율))로, 나머지는 Add로 넘긴다.
    // 적 질량(massScale)은 시트와 전투가 같은 단위(%, 전투 기본 100 = MassRule.Base)라 늘어난 %를 그대로 더한다.
    //
    // 여기 없는 수치는 노드를 사도 아직 전투에 효과가 없다. 옛 이름이 없거나(구현 필요), 뜻이 달라 그대로 옮길 수 없는 수치다:
    // goldenAsteroid.rewardScale(시트 50% ↔ 전투 ×50).
    public static class NodeUpgradeBridge
    {
        private const float Percent = 0.01f;

        private static readonly Route[] _routes =
        {
            new Route("growth.time", HqUpgradeStats.GrowthTime, UpgradeOperation.Add, 1),
            new Route("breaker.damage", BreakerUpgradeStats.Damage, UpgradeOperation.Add, 1),
            new Route("breaker.radius", BreakerUpgradeStats.Radius, UpgradeOperation.Percent, Percent),
            new Route("breaker.speed", BreakerUpgradeStats.Speed, UpgradeOperation.Percent, Percent),
            new Route("breaker.critChance", BreakerUpgradeStats.CritChance, UpgradeOperation.Add, Percent),
            new Route("breaker.critBonus", BreakerUpgradeStats.CritDamage, UpgradeOperation.Add, Percent),
            new Route("asteroid.count", EnemyUpgradeStats.StartSupply("asteroid"), UpgradeOperation.Add, 1),
            new Route("asteroid.size", EnemyUpgradeStats.Size("asteroid"), UpgradeOperation.Add, 1),
            new Route("asteroid.massScale", EnemyUpgradeStats.Mass("asteroid"), UpgradeOperation.Add, 1),
            new Route("planet.size", EnemyUpgradeStats.Size("planet"), UpgradeOperation.Add, 1),
            new Route("planet.massScale", EnemyUpgradeStats.Mass("planet"), UpgradeOperation.Add, 1),
            new Route("star.size", EnemyUpgradeStats.Size("star"), UpgradeOperation.Add, 1),
            new Route("star.massScale", EnemyUpgradeStats.Mass("star"), UpgradeOperation.Add, 1),
            new Route("electricAsteroid.spawnChance", EnemyUpgradeStats.TraitChance("asteroid", "electric"), UpgradeOperation.Add, 1),
            new Route("goldenAsteroid.spawnChance", EnemyUpgradeStats.TraitChance("asteroid", "golden"), UpgradeOperation.Add, 1),
            new Route("moonPlanet.spawnChance", EnemyUpgradeStats.TraitChance("planet", "moon"), UpgradeOperation.Add, 1),
            new Route("moonPlanet.speedScale", BreakerUpgradeStats.MoonSpeedBonus, UpgradeOperation.Add, Percent),
            new Route("moonPlanet.rangeScale", BreakerUpgradeStats.MoonRadiusBonus, UpgradeOperation.Add, Percent),
            new Route("moonPlanet.duration", BreakerUpgradeStats.MoonDuration, UpgradeOperation.Add, 1),
            new Route("comet.spawnChance", EnemyUpgradeStats.Chance("comet"), UpgradeOperation.Add, 1),
            new Route("comet.duration", BreakerUpgradeStats.CometDuration, UpgradeOperation.Add, 1),
            new Route("comet.critBonus", BreakerUpgradeStats.CometCritDamageBonus, UpgradeOperation.Add, Percent),
            new Route("electricStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "electric"), UpgradeOperation.Add, 1),
            new Route("laserStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "laser"), UpgradeOperation.Add, 1),
            new Route("supernovaStar.spawnChance", EnemyUpgradeStats.TraitChance("star", "supernova"), UpgradeOperation.Add, 1),

            new Route("breaker.planetDamageBonus", BreakerUpgradeStats.PlanetDamageBonus, UpgradeOperation.Add, 1),
            new Route("breaker.starDamageBonus", BreakerUpgradeStats.StarDamageBonus, UpgradeOperation.Add, 1),
        };

        private static readonly HashSet<string> _routed = RoutedStats();

        // 이 수치가 전투에 이어져 있는가(노드를 사면 지금 효과가 있는가).
        public static bool IsRouted(string statId) => _routed.Contains(statId);

        public static UpgradeTable ToUpgradeTable(UpgradeStatValues values)
        {
            var upgrades = new List<Upgrade>();

            foreach (Route route in _routes)
            {
                // 시트에서 수치가 빠졌으면 옮길 것이 없다.
                if (!values.Has(route.StatId))
                    continue;

                float gained = values.ValueOf(route.StatId) - values.DefinitionOf(route.StatId).DefaultValue;

                if (gained != 0)
                    upgrades.Add(new Upgrade(route.Target, route.Operation, gained * route.Scale));
            }

            return new UpgradeTable(upgrades);
        }

        private static HashSet<string> RoutedStats()
        {
            var routed = new HashSet<string>(StringComparer.Ordinal);

            foreach (Route route in _routes)
                routed.Add(route.StatId);

            return routed;
        }

        private readonly struct Route
        {
            public readonly string StatId;
            public readonly string Target;
            public readonly UpgradeOperation Operation;
            public readonly float Scale;

            public Route(string statId, string target, UpgradeOperation operation, float scale)
            {
                StatId = statId;
                Target = target;
                Operation = operation;
                Scale = scale;
            }
        }
    }
}
