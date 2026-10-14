using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 칸별 처치 장부(W3-1)가 판의 결과와 맞는지 검사한다. 서버가 결산 로그를 검증하는 식과 같다(계획서 S3·S5·S6·S7):
    // - 시작 EXP + Σ(칸 처치 수 × 칸 EXP) == Hq.Exp
    // - Σ(칸 처치 수 × 칸 Gold) + GoldenCritBonusGold == EarnedGold
    // - Σ(칸 처치 수) == TotalKills
    // - GoldenCritCount ≤ 황금 칸 처치 수, TimeBonusCount ≤ 수거형이 아닌 처치 수
    // 칸의 Gold·EXP는 그 판의 수치 표(World.Stats)에서 읽는다. 황금이 아닌 성질은 수치를 바꾸지 않으므로 성질 없음(null)으로 읽는다.
    // 판 실행기가 에디터 전용이라 NUnit 테스트 대신 스모크 실행이 판마다 이 검사를 부른다(작업지시 W3-1).
    internal static class KillLedgerCheck
    {
        // 맞으면 null, 아니면 어긋난 항목을 적은 글을 돌려준다.
        public static string Check(BattleRawData raw, World world)
        {
            Hq hq = world.Hq;
            EnemyStatTable stats = world.Stats;
            long startExp = hq.Growth.ExpToReach(hq.StartLevel) ?? 0;

            long bucketExp = 0;
            long bucketGold = 0;
            int bucketKills = 0;
            int goldenKills = 0;
            int pickupKills = 0;
            var seen = new HashSet<KillBucket>();
            var problems = new List<string>();

            foreach (KillBucketCount entry in raw.KillBuckets)
            {
                KillBucket bucket = entry.Bucket;

                if (!seen.Add(bucket))
                    problems.Add($"칸 중복: {Describe(bucket)}");

                EnemyStats bucketStats = stats.Of(bucket.Enemy, bucket.Tier, bucket.Golden ? GoldenTraitOf(stats, bucket.Enemy) : null, bucket.Size);
                bucketExp += bucketStats.Exp * entry.Count;
                bucketGold += bucketStats.Gold * entry.Count;
                bucketKills += entry.Count;
                goldenKills += bucket.Golden ? entry.Count : 0;
                pickupKills += bucket.Enemy.IsPickup ? entry.Count : 0;
            }

            if (startExp + bucketExp != hq.Exp)
                problems.Add($"EXP: 시작 {startExp:N0} + 칸 합 {bucketExp:N0} = {startExp + bucketExp:N0} ≠ Hq.Exp {hq.Exp:N0}");

            if (bucketGold + raw.GoldenCritBonusGold != raw.EarnedGold)
                problems.Add($"Gold: 칸 합 {bucketGold:N0} + 치명 보너스 {raw.GoldenCritBonusGold:N0} ≠ 번 Gold {raw.EarnedGold:N0}");

            if (bucketKills != raw.TotalKills)
                problems.Add($"처치 수: 칸 합 {bucketKills} ≠ TotalKills {raw.TotalKills}");

            if (raw.GoldenCritCount > goldenKills)
                problems.Add($"황금 치명 {raw.GoldenCritCount}번 > 황금 처치 {goldenKills}");

            if (raw.TimeBonusCount > bucketKills - pickupKills)
                problems.Add($"시간 추가 {raw.TimeBonusCount}번 > 수거형이 아닌 처치 {bucketKills - pickupKills}");

            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        // 이 판에서 이 종류의 황금 성질. 황금 칸이 있는데 성질이 없으면 장부가 틀린 것이라 예외로 알린다.
        private static EnemyTraitDefinition GoldenTraitOf(EnemyStatTable stats, EnemyDefinition kind)
        {
            foreach (EnemyTraitDefinition trait in stats.CompositionOf(kind).Traits)
            {
                if (trait.Effect is GoldenDefinition)
                    return trait;
            }

            throw new System.InvalidOperationException($"'{kind.Type}'에 황금 칸이 있지만 이 판 구성에 황금 성질이 없다.");
        }

        private static string Describe(KillBucket bucket) =>
            $"({bucket.Enemy.Type}, 색 {bucket.Tier}, 크기 {bucket.Size}, {(bucket.Golden ? "황금" : "보통")})";
    }
}
