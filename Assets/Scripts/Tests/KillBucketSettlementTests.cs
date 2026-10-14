using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using BlackHole.Core;
using BlackHole.Simulation;

namespace BlackHole.Tests
{
    public sealed class KillBucketSettlementTests
    {
        private static readonly int[] Seeds = Enumerable.Range(1, 20).ToArray();

        [Test]
        public void StatTable_GoldAndExp_DependOnlyOnBucket()
        {
            EnemyStatTable table = RunBattle(Seeds[0]).World.Stats;
            var mismatches = new List<string>();

            foreach (EnemyDefinition kind in table.Kinds)
            {
                EnemyComposition composition = table.CompositionOf(kind);

                for (int size = SizeRule.Base; size <= composition.Size; size++)
                {
                    for (int tier = 0; tier < kind.Tiers.Count; tier++)
                    {
                        EnemyStats plain = table.Of(kind, tier, null, size);
                        bool hasGolden = false;
                        EnemyStats golden = default;

                        foreach (EnemyTraitDefinition trait in composition.Traits)
                        {
                            EnemyStats stats = table.Of(kind, tier, trait, size);
                            string at = $"{kind.Type} tier {tier} size {size} trait {trait.Type}";

                            if (trait.Effect is GoldenDefinition)
                            {
                                if (!hasGolden)
                                {
                                    golden = stats;
                                    hasGolden = true;
                                }
                                else if (!SameReward(stats, golden))
                                {
                                    mismatches.Add($"{at}: 다른 황금 성질과 Gold·EXP가 다르다 ({Describe(stats)} vs {Describe(golden)})");
                                }
                            }
                            else if (!SameReward(stats, plain))
                            {
                                mismatches.Add($"{at}: 성질 없음과 Gold·EXP가 다르다 ({Describe(stats)} vs {Describe(plain)})");
                            }
                        }
                    }
                }
            }

            Assert.IsEmpty(mismatches, string.Join("\n", mismatches));
        }

        [Test]
        public void KillBuckets_SumTo_ExpAndEarnedGold([ValueSource(nameof(Seeds))] int seed)
        {
            BattleRun run = RunBattle(seed);
            BattleRawData raw = run.Raw;
            Hq hq = run.World.Hq;
            EnemyStatTable table = run.World.Stats;

            long startExp = hq.Growth.ExpToReach(hq.StartLevel) ?? 0;
            long bucketExp = 0;
            long bucketGold = 0;

            foreach (KillBucketCount kill in raw.KillBuckets)
            {
                EnemyStats stats = StatsOf(table, kill.Bucket);
                bucketExp = checked(bucketExp + (long)kill.Count * stats.Exp);
                bucketGold = checked(bucketGold + (long)kill.Count * stats.Gold);
            }

            Assert.AreEqual(hq.Exp, startExp + bucketExp, "EXP : Σ (칸 처치 수 * 칸 EXP) + 시작 EXP");
            Assert.AreEqual(raw.EarnedGold, bucketGold + raw.GoldenCritBonusGold,
                "Gold : Σ (칸 처치 수 * 칸 Gold) + Gold 치명타 보너스");
            Assert.AreEqual(hq.Exp, raw.Exp, "BattleRawData.Exp는 판이 끝난 Hq.Exp와 같아야 한다.");
        }

        [Test]
        public void KillBuckets_AreConsistentWithKindCounts([ValueSource(nameof(Seeds))] int seed)
        {
            BattleRawData raw = RunBattle(seed).Raw;

            // 칸은 중복되지 않는다(계약 5.2). 처치 수 >= 1은 KillBucketCount가 막는다.
            Assert.AreEqual(raw.KillBuckets.Count, raw.KillBuckets.Select(k => k.Bucket).Distinct().Count(), "칸 중복");

            // 로그에 실리는 키(enemyId, tier, size, golden)로도 중복이 없어야 한다. 칸은 EnemyDefinition 참조로 비교하므로
            // 같은 종류의 정의가 둘이면 칸은 둘이지만 로그에서는 같은 조합이 두 번 나온다.
            // enemyId는 종류(EnemyDefinition.Type)에서 나온다고 본다(BattleRecord.Kills도 Type으로 묶는다).
            Assert.AreEqual(raw.KillBuckets.Count, raw.KillBuckets.Select(LogKey).Distinct().Count(), "로그 키 중복");

            // 칸별 합계는 기존 종류별 집계와 같아야 한다.
            Assert.AreEqual(raw.TotalKills, raw.KillBuckets.Sum(k => k.Count), "칸 합계 != TotalKills");

            var byKind = new Dictionary<EnemyDefinition, int>();
            foreach (KillBucketCount kill in raw.KillBuckets)
                byKind[kill.Bucket.Enemy] = byKind.TryGetValue(kill.Bucket.Enemy, out int n) ? n + kill.Count : kill.Count;

            Assert.AreEqual(raw.Kills.Count, byKind.Count, "종류 수");
            foreach (EnemyKillCount kill in raw.Kills)
                Assert.AreEqual(kill.Count, byKind[kill.Enemy], $"{kill.Enemy.Type} 처치 수");

            // 수거형(혜성)은 색 등급 0, 크기 1, 황금 아님(KillBucket 주석).
            foreach (KillBucketCount kill in raw.KillBuckets.Where(k => k.Bucket.Enemy.IsPickup))
            {
                Assert.AreEqual(0, kill.Bucket.Tier, "혜성 색 등급");
                Assert.AreEqual(SizeRule.Base, kill.Bucket.Size, "혜성 크기");
                Assert.IsFalse(kill.Bucket.Golden, "혜성 황금");
            }

            // 치명타 없이 보너스가 생기거나 보너스가 음수일 수 없다.
            Assert.GreaterOrEqual(raw.GoldenCritBonusGold, 0);
            if (raw.GoldenCritCount == 0)
                Assert.AreEqual(0, raw.GoldenCritBonusGold, "치명타 0회인데 보너스가 있다");

            // 황금 치명타는 황금 적 처치에서만, 시간 추가는 수거형이 아닌 처치에서만 일어난다(DeathRewards, DeathBonuses).
            int goldenKills = raw.KillBuckets.Where(k => k.Bucket.Golden).Sum(k => k.Count);
            int nonPickupKills = raw.KillBuckets.Where(k => !k.Bucket.Enemy.IsPickup).Sum(k => k.Count);
            Assert.LessOrEqual(raw.GoldenCritCount, goldenKills, "황금 치명타가 황금 처치보다 많다");
            Assert.LessOrEqual(raw.TimeBonusCount, nonPickupKills, "시간 추가 성공이 수거형 아닌 처치보다 많다");
        }

        // 위 테스트들이 실제로 모든 경로를 지났는지: 시드 전체에서 각 경로가 한 번 이상 일어나야 한다.
        // 실패하면 시드를 늘리거나 BattleCheats로 해당 적을 깔아 두는 테스트를 따로 둔다.
        [Test]
        public void Seeds_CoverEveryRewardPath()
        {
            int goldenKills = 0, goldenCrits = 0, timeBonuses = 0, pickupKills = 0;

            foreach (int seed in Seeds)
            {
                BattleRawData raw = RunBattle(seed).Raw;
                goldenKills += raw.KillBuckets.Where(k => k.Bucket.Golden).Sum(k => k.Count);
                pickupKills += raw.KillBuckets.Where(k => k.Bucket.Enemy.IsPickup).Sum(k => k.Count);
                goldenCrits += raw.GoldenCritCount;
                timeBonuses += raw.TimeBonusCount;
            }

            Assert.Greater(goldenKills, 0, "황금 적 처치가 없다");
            Assert.Greater(goldenCrits, 0, "황금 치명타가 없다");
            Assert.Greater(timeBonuses, 0, "시간 추가 성공이 없다");
            Assert.Greater(pickupKills, 0, "혜성 수거가 없다");
        }

        // 칸 하나의 Gold/EXP: 이 판의 수치표에서 황금이면 황금 성질의 값, 아니면 성질 없음의 값.
        // 황금 아닌 성질이 성질 없음과 같다는 전제는 StatTable_GoldAndExp_DependOnlyOnBucket이 확인한다.
        private static EnemyStats StatsOf(EnemyStatTable table, KillBucket bucket)
        {
            EnemyTraitDefinition trait = null;

            if (bucket.Golden)
            {
                trait = table.CompositionOf(bucket.Enemy).Traits.FirstOrDefault(t => t.Effect is GoldenDefinition);
                Assert.IsNotNull(trait, $"{bucket.Enemy.Type}: 황금 칸인데 이 판 구성에 황금 성질이 없다");
            }

            return table.Of(bucket.Enemy, bucket.Tier, trait, bucket.Size);
        }

        private static (EnemyType, int, int, bool) LogKey(KillBucketCount kill) =>
            (kill.Bucket.Enemy.Type, kill.Bucket.Tier, kill.Bucket.Size, kill.Bucket.Golden);

        private static bool SameReward(EnemyStats a, EnemyStats b) => a.Gold == b.Gold && a.Exp == b.Exp;

        private static string Describe(EnemyStats s) => $"Gold {s.Gold}, EXP {s.Exp}";

        private static BattleRun RunBattle(int seed) =>
            throw new System.NotImplementedException("RunBattle: 판 준비 코드(콘텐츠·진행 상태·노드 수치·봇)를 채워야 한다.");
    }
}
