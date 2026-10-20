using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 콘텐츠 전체의 규칙: 적 종류는 한 번씩만 있다.
    // EnemyContent 생성자(첫 오류로 생성 실패)와 EnemyContentLoader(경로별 진단 수집)가 함께 쓴다.
    // 개별 정의의 수치 규칙은 각 정의 생성자에 있다 — 여기서 다시 보지 않는다.
    // 정의 객체로 해석되는 참조(공급의 적)는 EnemyContentLoader가 이 색인으로 해석하며 진단한다.
    internal static class EnemyContentInvariants
    {
        // 공급되는 종류(소행성·행성·별)의 색 수: 빨주노초파보. 질량 규칙(MassRule)은 색 수와 무관하게 동작하지만 콘텐츠는 6색으로 맞춘다.
        public const int SuppliedTierCount = 6;

        // 공급되는 종류는 색이 SuppliedTierCount개다. 픽업은 색 수를 보지 않는다.
        public static void CheckTierCounts(IReadOnlyList<EnemyDefinition> enemies, ICollection<ContentDiagnostic> into)
        {
            foreach (EnemyDefinition kind in enemies)
            {
                if (kind != null && !kind.IsPickup && kind.Tiers.Count != SuppliedTierCount)
                    into.Add(new ContentDiagnostic($"Enemies[{kind.Type}].Tiers",
                        $"공급되는 종류는 색이 {SuppliedTierCount}개(빨주노초파보)여야 한다. 지금 {kind.Tiers.Count}개."));
            }
        }

        // 주기 출현 띠는 일반 출현 띠에서 풀었을 때 띠가 되어야 한다(바깥 반지름이 0보다 크다).
        public static void CheckPeriodicSpawnPlacement(
            EnemyPlacementDefinition placement,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement,
            ICollection<ContentDiagnostic> into)
        {
            try
            {
                periodicSpawnPlacement.Resolve(placement);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                into.Add(new ContentDiagnostic("PeriodicSpawnPlacement",
                    $"일반 출현 띠(바깥 반지름 {placement.MaxDistance})에서 풀면 띠가 되지 않는다: {ex.Message}"));
            }
        }

        // 공급은 픽업을 가리킬 수 없다 — 픽업은 등장 주기마다 나온다.
        public static void CheckSupplyKinds(IReadOnlyList<SupplyRequest> supply, string section, ICollection<ContentDiagnostic> into)
        {
            for (int i = 0; i < supply.Count; i++)
            {
                if (supply[i].Enemy.IsPickup)
                    into.Add(new ContentDiagnostic($"{section}[{i}].Enemy", $"'{supply[i].Enemy.Type}'는 픽업이라 공급할 수 없다."));
            }
        }

        public static void CollectEnemies(
            IReadOnlyList<EnemyDefinition> enemies,
            ICollection<ContentDiagnostic> into,
            out Dictionary<EnemyType, EnemyDefinition> enemiesByType)
        {
            enemiesByType = new Dictionary<EnemyType, EnemyDefinition>();

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyDefinition enemy = enemies[i];

                if (enemy == null)
                    into.Add(new ContentDiagnostic($"Enemies[{i}]", "적 정의가 null이다."));
                else if (!enemiesByType.TryAdd(enemy.Type, enemy))
                    into.Add(new ContentDiagnostic($"Enemies[{i}]", $"적 종류 '{enemy.Type}'가 중복됐다."));
            }
        }

        // 종류 사이의 연결(BLACKHOLE_LEVEL_PLAN 4.3): 변환 대상은 콘텐츠에 있고 픽업이 아니어야 한다.
        // 변환 사슬은 제자리로 돌아오지 않는다(한 마리의 변환이 끝나야 한다).
        public static void CheckKindLinks(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyDictionary<EnemyType, EnemyDefinition> enemiesByType,
            ICollection<ContentDiagnostic> into)
        {
            foreach (EnemyDefinition kind in enemies)
            {
                if (kind == null)
                    continue;

                string at = $"Enemies[{kind.Type}]";

                if (kind.UpgradesTo.HasValue)
                {
                    if (!enemiesByType.TryGetValue(kind.UpgradesTo.Value, out EnemyDefinition target))
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"적 종류 목록에 없는 종류 '{kind.UpgradesTo.Value}'."));
                    else if (target.IsPickup)
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"'{target.Type}'는 픽업이라 변환 대상이 될 수 없다."));
                }

                var seen = new HashSet<EnemyType> { kind.Type };

                for (EnemyDefinition next = kind; next.UpgradesTo.HasValue && enemiesByType.TryGetValue(next.UpgradesTo.Value, out next);)
                {
                    if (!seen.Add(next.Type))
                    {
                        into.Add(new ContentDiagnostic(at + ".UpgradesTo", $"변환 사슬이 '{next.Type}'에서 다시 돈다."));
                        break;
                    }
                }
            }
        }
    }
}
