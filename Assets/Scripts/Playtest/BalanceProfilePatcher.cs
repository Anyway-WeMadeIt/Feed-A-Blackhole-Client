#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 밸런스 프로필의 경로로 콘텐츠 저작 데이터(ContentData)와 노드 시트 데이터(NodeContentData)의 값을 읽고 쓴다.
    // 로더(ContentLoader·NodeContentLoader)에 넘기기 전에 부르므로, 패치한 값도 원래 검증을 그대로 거친다.
    // 로컬 테스트 프로필에서 경로별 값을 덧씌운다.
    //
    // 경로는 슬래시로 나눈다. 종류·성질은 계약 ID처럼 열거형 이름의 첫 글자만 소문자다(asteroid, golden).
    //   battle/<timeLimit|killTimeBonus>
    //   breaker/<damage|interval|radius|critChance|critDamage|moonDuration|moonSpeedBonus|moonRadiusBonus|cometDuration|cometCritDamageBonus|planetBonusDamage|starBonusDamage>
    //   placement/<minDistance|maxDistance>           적 출현 띠(HQ로부터 거리, 전장 배율 1 기준)
    //   enemy/<종류>/<moveSpeed|radius|radiusStep|spawnPeriod|rainCount>
    //   enemy/<종류>/tier/<색 등급, 1부터>/<hp|gold|exp>
    //   enemy/<종류>/trait/<성질>/<multiplier|critRewardScale|damage|radius|maxTargets|branchChance|critChance|critMultiplier|healthFraction|width|maxActive>
    //   supply/<종류>/count                       시작 공급에 없는 종류면 더한다(없는 종류의 지금 값은 0)
    //   growth/levelExp/<Level, 1부터>             그 Level에 닿는 누적 EXP
    //   growth/milestone/<번호, 1부터>/<level|targetGold|fieldScale>
    //   node/<노드 ID>/<Rank>/cost
    //   node/<노드 ID>/<Rank>/<StatId>             그 Rank의 효과 값
    // 모든 패치를 먼저 검사하고, 하나라도 틀리면 아무것도 바꾸지 않는다(부분 적용 없음).
    internal static class BalanceProfilePatcher
    {
        // 값의 종류. 실수는 float, 정수는 int, 큰 정수(Gold·EXP·비용)는 long 칸이다.
        internal enum SlotKind
        {
            Real,
            Int,
            Long,
        }

        // 경로가 가리키는 값 하나(읽기·쓰기).
        internal sealed class Slot
        {
            public SlotKind Kind;
            public Func<double> Get;
            public Action<double> Set;
        }

        // 성질마다 쓰는 사망 효과 칸(EnemyContentLoader.LoadDeathEffect와 같다). 모든 성질은 maxActive도 있다.
        private static readonly Dictionary<EnemyTraitType, string[]> TraitFields = new Dictionary<EnemyTraitType, string[]>
        {
            { EnemyTraitType.Golden, new[] { "multiplier", "critChance", "critRewardScale" } },
            { EnemyTraitType.Electric, new[] { "damage", "radius", "maxTargets", "branchChance", "critChance", "critMultiplier" } },
            { EnemyTraitType.Supernova, new[] { "healthFraction", "radius" } },
            { EnemyTraitType.Laser, new[] { "damage", "width", "critChance", "critMultiplier" } },
            { EnemyTraitType.Moon, Array.Empty<string>() },
            { EnemyTraitType.Comet, Array.Empty<string>() },
        };

        private static readonly string[] BattleFields = { "timeLimit", "killTimeBonus" };

        private static readonly string[] BreakerFields =
        {
            "damage", "interval", "radius", "critChance", "critDamage", "moonDuration", "moonSpeedBonus", "moonRadiusBonus",
            "cometDuration", "cometCritDamageBonus", "planetBonusDamage", "starBonusDamage",
        };

        private static readonly string[] PlacementFields = { "minDistance", "maxDistance" };
        private static readonly string[] EnemyFields = { "moveSpeed", "radius", "radiusStep", "spawnPeriod", "rainCount" };
        private static readonly string[] TierFields = { "hp", "gold", "exp" };
        private static readonly string[] MilestoneFields = { "level", "targetGold", "fieldScale" };

        public static IReadOnlyList<string> Apply(BalanceProfile profile, ContentData content, NodeContentData nodes)
        {
            var errors = new List<string>();
            var setters = new List<Action>();

            for (int i = 0; i < profile.patches.Count; i++)
            {
                BalanceProfile.Patch patch = profile.patches[i];
                string path = patch?.path;

                if (string.IsNullOrEmpty(path))
                {
                    errors.Add($"patches[{i}]: 경로가 비어 있다.");
                    continue;
                }

                if (!TryResolve(path, content, nodes, out Slot slot, out string error) || !Accepts(slot.Kind, patch.value, out error))
                {
                    errors.Add($"patches[{i}] '{path}': {error}");
                    continue;
                }

                double value = patch.value;
                setters.Add(() => slot.Set(value));
            }

            if (errors.Count == 0)
            {
                foreach (Action setter in setters)
                    setter();
            }

            return errors;
        }

        // 경로의 지금 값.
        public static bool TryRead(string path, ContentData content, NodeContentData nodes, out double value, out string error)
        {
            if (!TryResolve(path, content, nodes, out Slot slot, out error))
            {
                value = 0;
                return false;
            }

            value = slot.Get();
            return true;
        }

        // 값이 그 칸의 종류에 맞는가(유한한 실수, 정수 범위).
        public static bool Accepts(SlotKind kind, double value, out string error)
        {
            switch (kind)
            {
                case SlotKind.Real:
                    if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
                    {
                        error = $"유한한 실수가 필요하다: {value}.";
                        return false;
                    }

                    break;
                case SlotKind.Int:
                    if (value != Math.Floor(value) || value < int.MinValue || value > int.MaxValue)
                    {
                        error = $"정수가 필요하다: {value}.";
                        return false;
                    }

                    break;
                default:
                    // double이 정수를 정확히 담는 범위(2^53)까지만 받는다.
                    if (value != Math.Floor(value) || Math.Abs(value) > 9007199254740992d)
                    {
                        error = $"정수가 필요하다: {value}.";
                        return false;
                    }

                    break;
            }

            error = null;
            return true;
        }

        public static bool TryResolve(string path, ContentData content, NodeContentData nodes, out Slot slot, out string error)
        {
            if (string.IsNullOrEmpty(path))
            {
                slot = null;
                error = "경로가 비어 있다.";
                return false;
            }

            slot = Resolve(path.Split('/'), content, nodes, out error);
            return slot != null;
        }

        // 노드가 아닌 모든 경로(값 목록, 대응표 검사). 순서: battle, breaker, placement, supply, enemy, growth.
        public static List<string> ContentPaths(ContentData content)
        {
            var paths = new List<string>();

            foreach (string field in BattleFields)
                paths.Add("battle/" + field);

            foreach (string field in BreakerFields)
                paths.Add("breaker/" + field);

            foreach (string field in PlacementFields)
                paths.Add("placement/" + field);

            foreach (SupplyData supply in content.Enemies.StartSupply)
            {
                if (supply?.Enemy != null)
                    paths.Add($"supply/{ContractIds.Of(supply.Enemy.Value.ToString())}/count");
            }

            foreach (EnemyData enemy in content.Enemies.Enemies)
            {
                string kind = "enemy/" + ContractIds.Of(enemy.Type.ToString());

                foreach (string field in EnemyFields)
                    paths.Add($"{kind}/{field}");

                for (int tier = 1; tier <= enemy.Tiers.Count; tier++)
                {
                    foreach (string field in TierFields)
                        paths.Add($"{kind}/tier/{tier}/{field}");
                }

                foreach (EnemyTraitData trait in enemy.Traits)
                {
                    string at = $"{kind}/trait/{ContractIds.Of(trait.Type.ToString())}";

                    if (trait.Effect != null && TraitFields.TryGetValue(trait.Type, out string[] fields))
                    {
                        foreach (string field in fields)
                            paths.Add($"{at}/{field}");
                    }

                    paths.Add($"{at}/maxActive");
                }
            }

            for (int level = 1; level <= content.Growth.LevelExp.Count; level++)
                paths.Add($"growth/levelExp/{level}");

            for (int milestone = 1; milestone <= content.Growth.Milestones.Count; milestone++)
            {
                foreach (string field in MilestoneFields)
                    paths.Add($"growth/milestone/{milestone}/{field}");
            }

            return paths;
        }

        // 노드 한 Rank의 경로: 비용, 그리고 효과(StatId)마다 하나.
        public static List<string> NodePaths(NodeContentData nodes, string nodeId, int rank)
        {
            var paths = new List<string>();
            string at = $"node/{nodeId}/{rank.ToString(CultureInfo.InvariantCulture)}";

            if (nodes.Costs.Exists(c => c.NodeId == nodeId && c.Rank == rank))
                paths.Add(at + "/cost");

            foreach (NodeEffectRowData effect in nodes.Effects)
            {
                if (effect.NodeId == nodeId && effect.Rank == rank)
                    paths.Add($"{at}/{effect.StatId}");
            }

            return paths;
        }

        private static Slot Resolve(string[] path, ContentData content, NodeContentData nodes, out string error)
        {
            switch (path[0])
            {
                case "battle" when path.Length == 2:
                    return Battle(path[1], content.BattleRules, out error);
                case "breaker" when path.Length == 2:
                    return Breaker(path[1], content.Breaker, out error);
                case "placement" when path.Length == 2:
                    return Placement(path[1], content.Enemies, out error);
                case "enemy":
                    return Enemy(path, content.Enemies, out error);
                case "supply" when path.Length == 3 && path[2] == "count":
                    return Supply(path[1], content.Enemies, out error);
                case "growth":
                    return Growth(path, content.Growth, out error);
                case "node" when path.Length == 4:
                    return Node(path[1], path[2], path[3], nodes, out error);
                default:
                    error = "알 수 없는 경로다.";
                    return null;
            }
        }

        private static Slot Battle(string field, BattleRulesData rules, out string error)
        {
            switch (field)
            {
                case "timeLimit": return Real(() => rules.TimeLimit, v => rules.TimeLimit = v, out error);
                case "killTimeBonus": return Real(() => rules.KillTimeBonus, v => rules.KillTimeBonus = v, out error);
                default: return Unknown(field, out error);
            }
        }

        private static Slot Breaker(string field, BreakerData breaker, out string error)
        {
            switch (field)
            {
                case "damage": return Real(() => breaker.Damage, v => breaker.Damage = v, out error);
                case "interval": return Real(() => breaker.Interval, v => breaker.Interval = v, out error);
                case "radius": return Real(() => breaker.Radius, v => breaker.Radius = v, out error);
                case "critChance": return Real(() => breaker.CritChance, v => breaker.CritChance = v, out error);
                case "critDamage": return Real(() => breaker.CritDamage, v => breaker.CritDamage = v, out error);
                case "moonDuration": return Real(() => breaker.MoonDuration, v => breaker.MoonDuration = v, out error);
                case "moonSpeedBonus": return Real(() => breaker.MoonSpeedBonus, v => breaker.MoonSpeedBonus = v, out error);
                case "moonRadiusBonus": return Real(() => breaker.MoonRadiusBonus, v => breaker.MoonRadiusBonus = v, out error);
                case "cometDuration": return Real(() => breaker.CometDuration, v => breaker.CometDuration = v, out error);
                case "cometCritDamageBonus": return Real(() => breaker.CometCritDamageBonus, v => breaker.CometCritDamageBonus = v, out error);
                case "planetBonusDamage": return Real(() => breaker.PlanetBonusDamage, v => breaker.PlanetBonusDamage = v, out error);
                case "starBonusDamage": return Real(() => breaker.StarBonusDamage, v => breaker.StarBonusDamage = v, out error);
                default: return Unknown(field, out error);
            }
        }

        private static Slot Placement(string field, EnemyContentData enemies, out string error)
        {
            EnemyPlacementData placement = enemies.EnemyPlacement;

            if (placement == null)
            {
                error = "출현 배치 데이터가 없다.";
                return null;
            }

            switch (field)
            {
                case "minDistance": return Real(() => placement.MinDistance, v => placement.MinDistance = v, out error);
                case "maxDistance": return Real(() => placement.MaxDistance, v => placement.MaxDistance = v, out error);
                default: return Unknown(field, out error);
            }
        }

        private static Slot Enemy(string[] path, EnemyContentData enemies, out string error)
        {
            if (path.Length < 3)
                return Unknown(string.Join("/", path), out error);

            EnemyData enemy = enemies.Enemies.Find(e => ContractIds.Of(e.Type.ToString()) == path[1]);

            if (enemy == null)
            {
                error = $"적 종류가 없다: '{path[1]}'.";
                return null;
            }

            if (path.Length == 3)
            {
                switch (path[2])
                {
                    case "moveSpeed": return Real(() => enemy.MoveSpeed, v => enemy.MoveSpeed = v, out error);
                    case "radius": return Real(() => enemy.Radius, v => enemy.Radius = v, out error);
                    case "radiusStep": return Real(() => enemy.RadiusStep, v => enemy.RadiusStep = v, out error);
                    case "spawnPeriod": return Real(() => enemy.SpawnPeriod, v => enemy.SpawnPeriod = v, out error);
                    case "rainCount": return Int(() => enemy.RainCount, v => enemy.RainCount = v, out error);
                    default: return Unknown(path[2], out error);
                }
            }

            if (path.Length == 5 && path[2] == "tier")
            {
                if (!TryIndex(path[3], enemy.Tiers.Count, out int index, out error))
                    return null;

                EnemyTierData tier = enemy.Tiers[index];

                switch (path[4])
                {
                    case "hp": return Real(() => tier.MaxHealth, v => tier.MaxHealth = v, out error);
                    case "gold": return Long(() => tier.Gold, v => tier.Gold = v, out error);
                    case "exp": return Long(() => tier.Exp, v => tier.Exp = v, out error);
                    default: return Unknown(path[4], out error);
                }
            }

            if (path.Length == 5 && path[2] == "trait")
            {
                EnemyTraitData trait = enemy.Traits.Find(t => ContractIds.Of(t.Type.ToString()) == path[3]);

                if (trait == null)
                {
                    error = $"'{path[1]}'에 그 성질이 없다: '{path[3]}'.";
                    return null;
                }

                if (path[4] == "maxActive")
                    return Int(() => trait.MaxActive, v => trait.MaxActive = v, out error);

                DeathEffectData effect = trait.Effect;

                if (effect == null)
                {
                    error = "성질에 사망 효과 데이터가 없다.";
                    return null;
                }

                switch (path[4])
                {
                    case "multiplier": return Real(() => effect.Multiplier, v => effect.Multiplier = v, out error);
                    case "critRewardScale": return Real(() => effect.CritRewardScale, v => effect.CritRewardScale = v, out error);
                    case "damage": return Real(() => effect.Damage, v => effect.Damage = v, out error);
                    case "radius": return Real(() => effect.Radius, v => effect.Radius = v, out error);
                    case "maxTargets": return Int(() => effect.MaxTargets, v => effect.MaxTargets = v, out error);
                    case "branchChance": return Real(() => effect.BranchChance, v => effect.BranchChance = v, out error);
                    case "critChance": return Real(() => effect.CritChance, v => effect.CritChance = v, out error);
                    case "critMultiplier": return Real(() => effect.CritMultiplier, v => effect.CritMultiplier = v, out error);
                    case "healthFraction": return Real(() => effect.HealthFraction, v => effect.HealthFraction = v, out error);
                    case "width": return Real(() => effect.Width, v => effect.Width = v, out error);
                    default: return Unknown(path[4], out error);
                }
            }

            return Unknown(string.Join("/", path), out error);
        }

        private static Slot Supply(string kind, EnemyContentData enemies, out string error)
        {
            EnemyType? type = null;

            foreach (EnemyType candidate in Enum.GetValues(typeof(EnemyType)))
            {
                if (ContractIds.Of(candidate.ToString()) == kind)
                    type = candidate;
            }

            if (type == null)
            {
                error = $"적 종류가 없다: '{kind}'.";
                return null;
            }

            SupplyData Find() => enemies.StartSupply.Find(s => s.Enemy == type);

            return Int(() => Find()?.Count ?? 0, v =>
            {
                SupplyData supply = Find();

                if (supply != null)
                    supply.Count = v;
                else
                    enemies.StartSupply.Add(new SupplyData { Enemy = type, Count = v });
            }, out error);
        }

        private static Slot Growth(string[] path, HqGrowthData growth, out string error)
        {
            if (path.Length == 3 && path[1] == "levelExp")
            {
                if (!TryIndex(path[2], growth.LevelExp.Count, out int index, out error))
                    return null;

                return Long(() => growth.LevelExp[index], v => growth.LevelExp[index] = v, out error);
            }

            if (path.Length == 4 && path[1] == "milestone")
            {
                if (!TryIndex(path[2], growth.Milestones.Count, out int index, out error))
                    return null;

                HqMilestoneData milestone = growth.Milestones[index];

                switch (path[3])
                {
                    case "level": return Int(() => milestone.Level, v => milestone.Level = v, out error);
                    case "targetGold": return Long(() => milestone.TargetGold, v => milestone.TargetGold = v, out error);
                    case "fieldScale": return Real(() => milestone.FieldScale, v => milestone.FieldScale = v, out error);
                    default: return Unknown(path[3], out error);
                }
            }

            return Unknown(string.Join("/", path), out error);
        }

        private static Slot Node(string nodeId, string rankText, string field, NodeContentData nodes, out string error)
        {
            if (!int.TryParse(rankText, NumberStyles.None, CultureInfo.InvariantCulture, out int rank))
            {
                error = $"Rank는 1 이상의 정수다: '{rankText}'.";
                return null;
            }

            if (field == "cost")
            {
                NodeCostRowData cost = nodes.Costs.Find(c => c.NodeId == nodeId && c.Rank == rank);

                if (cost == null)
                {
                    error = $"노드 가격 행이 없다: '{nodeId}' Rank {rank}.";
                    return null;
                }

                return Long(() => cost.Cost, v => cost.Cost = v, out error);
            }

            NodeEffectRowData effect = nodes.Effects.Find(e => e.NodeId == nodeId && e.Rank == rank && e.StatId == field);

            if (effect == null)
            {
                error = $"노드 효과 행이 없다: '{nodeId}' Rank {rank} '{field}'.";
                return null;
            }

            return Real(() => effect.Value, v => effect.Value = v, out error);
        }

        // 1부터 센 번호 text를 0부터의 인덱스로. 범위 밖이면 false.
        private static bool TryIndex(string text, int count, out int index, out string error)
        {
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= count)
            {
                index = number - 1;
                error = null;
                return true;
            }

            index = -1;
            error = $"번호는 1부터 {count}까지다: '{text}'.";
            return false;
        }

        private static Slot Real(Func<float> get, Action<float> set, out string error)
        {
            error = null;
            return new Slot { Kind = SlotKind.Real, Get = () => Decimal(get()), Set = v => set((float)v) };
        }

        // float 값을 사람이 적은 그대로의 십진수로(0.35f → 0.35, 0.3499999940… 아님). 비교·기록·묶음에 쓴다.
        public static double Decimal(float value) =>
            double.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

        private static Slot Long(Func<long> get, Action<long> set, out string error)
        {
            error = null;
            return new Slot { Kind = SlotKind.Long, Get = () => get(), Set = v => set((long)v) };
        }

        private static Slot Int(Func<int> get, Action<int> set, out string error)
        {
            error = null;
            return new Slot { Kind = SlotKind.Int, Get = () => get(), Set = v => set((int)v) };
        }

        private static Slot Unknown(string field, out string error)
        {
            error = $"알 수 없는 칸이다: '{field}'.";
            return null;
        }
    }
}
#endif
