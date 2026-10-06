using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // ContentData(저작 형식) → GameContent(검증된 정의).
    //
    // 오류가 하나라도 있으면 Content 없이 모든 진단을 돌려준다(부분 통과 금지).
    // 판 설정과 스킬은 여기서 읽고, 적 콘텐츠는 EnemyContentLoader가, 블랙홀 성장은 HqGrowthLoader가 읽는다.
    // 여기서 새로 두는 규칙은 데이터 모양에 관한 것뿐이다(빠진 칸). 수치 규칙은 정의 생성자를 그대로 호출해 경로를 붙인다.
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

            TimeLimitDefinition timeLimit = LoadSession(data.Session, diagnostics);
            BreakerDefinition breaker = LoadBreaker(data.Breaker, diagnostics);
            HqGrowthDefinition growth = HqGrowthLoader.Load(data.Growth, diagnostics);
            EnemyContent enemies = EnemyContentLoader.Load(data.Enemies, diagnostics);

            if (diagnostics.Count > 0)
                return Fail(diagnostics);

            return new ContentLoadResult(new GameContent(timeLimit, breaker, enemies, growth), diagnostics);
        }

        private static TimeLimitDefinition LoadSession(SessionData item, List<ContentDiagnostic> into)
        {
            if (item == null)
            {
                into.Add(new ContentDiagnostic("Session", "데이터가 없다."));
                return null;
            }

            return Guard("Session.TimeLimit", into, () => new TimeLimitDefinition(item.TimeLimit));
        }

        // ── 스킬 ────────────────────────────────────────────────────────────

        // 없으면 판에 Breaker가 없다.
        private static BreakerDefinition LoadBreaker(BreakerData item, List<ContentDiagnostic> into)
        {
            if (item == null)
                return null;

            return Guard("Breaker", into, () =>
                new BreakerDefinition(item.Damage, item.Interval, item.Radius, item.CritChance, item.CritDamage,
                    item.MoonDuration, item.MoonSpeedBonus, item.MoonRadiusBonus, item.CometDuration, item.CometCritDamageBonus,
                    item.PlanetBonus, item.StarBonus));
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

        private static ContentLoadResult Fail(List<ContentDiagnostic> diagnostics) =>
            new ContentLoadResult(null, diagnostics);
    }
}
