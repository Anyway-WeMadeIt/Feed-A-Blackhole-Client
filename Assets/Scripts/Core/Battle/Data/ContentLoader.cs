using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // ContentData(저작 형식) -> GameContent(검증된 정의).
    // 오류가 하나라도 있으면 Content 없이 모든 진단을 돌려준다(부분 통과 금지).
    public static class ContentLoader
    {
        public static ContentLoadResult Load(ContentData data)
        {
            var diagnostics = new List<ContentDiagnostic>();

            if (data == null)
            {
                diagnostics.Add(new ContentDiagnostic(string.Empty, "콘텐츠 데이터가 null이다."));
                return Fail(diagnostics);
            }

            TimeLimitDefinition timeLimit = LoadBattleRules(data.BattleRules, diagnostics);
            BreakerDefinition breaker = LoadBreaker(data.Breaker, diagnostics);
            HqGrowthDefinition growth = HqGrowthLoader.Load(data.Growth, diagnostics);
            EnemyContent enemies = EnemyContentLoader.Load(data.Enemies, diagnostics);

            if (diagnostics.Count > 0)
                return Fail(diagnostics);

            GameContent content = new(timeLimit, breaker, enemies, growth);
            return new ContentLoadResult(content, diagnostics);
        }

        private static TimeLimitDefinition LoadBattleRules(BattleRulesData item, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic("BattleRules", "데이터가 없다."));
                return null;
            }

            return Guard("BattleRules.TimeLimit", into, () => new TimeLimitDefinition(item.TimeLimit, item.KillTimeBonus, item.MaxKillTimeBonus, item.KillTimeCooldown));
        }

        private static BreakerDefinition LoadBreaker(BreakerData item, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic("Breaker", "데이터가 없다."));
                return null;
            }

            return Guard("Breaker", into, () =>
                new BreakerDefinition(item.Damage, item.Interval, item.Radius, item.CritChance, item.CritDamage,
                    item.MoonDuration, item.MoonSpeedBonus, item.MoonRadiusBonus, item.CometDuration, item.CometCritDamageBonus,
                    item.PlanetBonusDamage, item.StarBonusDamage, item.GalaxyBonusDamage, item.HoldSlow, item.MoonSpeedDecay, cometCritDecay: item.CometCritDecay,
                    moonRadiusDecay: item.MoonRadiusDecay));
        }

        private static T Guard<T>(string at, List<ContentDiagnostic> into, Func<T> create) where T : class
        {
            try { return create(); }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(at, error.Message));
                return null;
            }
        }

        private static ContentLoadResult Fail(List<ContentDiagnostic> diagnostics) => new(null, diagnostics);
    }
}
