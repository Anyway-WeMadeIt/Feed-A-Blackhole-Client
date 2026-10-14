using System;
using System.Collections.Generic;
using BlackHole.Core;
using BlackHole.Sync;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 판 시작·결산 로그가 서버 샘플(계약 3.8)과 같은 모양인지, 입력 값을 빠짐없이 싣는지(W3-2).
    public sealed class SyncLogFactoryTests
    {
        private const string RunId = "6f1c2b0a-3d4e-4f5a-8b6c-7d8e9f0a1b2c";

        private static readonly EnemyDefinition Asteroid = Enemy(EnemyType.Asteroid);
        private static readonly EnemyDefinition Planet = Enemy(EnemyType.Planet);

        private static EnemyDefinition Enemy(EnemyType type) =>
            new(type, 1f, 1f, 0f, new[] { new EnemyTier(1f, 1, 1) });

        private static SyncLogFactory Factory(DateTime now) =>
            new(new TestJson(), "0.4.0", "4d6c398ad1f5e49c", () => now);

        private static BattleRawData Raw(float played, long earned, int level, long exp, int stage, int nextStage,
            long settled, params KillBucketCount[] kills) =>
            new(123456, played, Array.Empty<EnemyKillCount>(), earned, level, exp, stage, nextStage, null, settled, null)
            {
                KillBuckets = kills,
            };

        [Test]
        public void RunStartLogMatchesTheSample()
        {
            var start = new RunStartInfo(123456, 0, 0, 12f, new[] { new SupplyRequest(Asteroid, 20) });

            PendingLog log = Factory(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)).RunStart(1, RunId, start, 0);

            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("log-run-start.json")), log.Json);
            Assert.AreEqual(1, log.Seq);
            Assert.AreEqual(LogTypes.RunStart, log.Type);
        }

        [Test]
        public void SettlementLogMatchesTheSample()
        {
            BattleRawData raw = Raw(12f, 20, 0, 20, 0, 0, 20,
                new KillBucketCount(new KillBucket(Asteroid, 0, 1, false), 20));

            PendingLog log = Factory(new DateTime(2026, 10, 12, 10, 0, 15, DateTimeKind.Utc)).Settlement(2, RunId, raw);

            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("log-settlement.json")), log.Json);
            Assert.AreEqual(LogTypes.Settlement, log.Type);
        }

        [Test]
        public void RunStartCarriesTheStartGoldAndEverySupplyInOrder()
        {
            var start = new RunStartInfo(-7, 2, 10, 15.5f,
                new[] { new SupplyRequest(Planet, 3), new SupplyRequest(Asteroid, 12) });

            var payload = PayloadOf<RunStartPayloadDto>(
                Factory(DateTime.UtcNow).RunStart(5, RunId, start, 52000));

            Assert.AreEqual(-7, payload.seed);
            Assert.AreEqual(2, payload.startGrowthStage);
            Assert.AreEqual(52000, payload.startGold);
            Assert.AreEqual(10, payload.startLevel);
            Assert.AreEqual(15.5f, payload.timeLimitSeconds);
            Assert.AreEqual(2, payload.startSupply.Count);
            Assert.AreEqual("planet", payload.startSupply[0].enemyId);
            Assert.AreEqual(3, payload.startSupply[0].count);
            Assert.AreEqual("asteroid", payload.startSupply[1].enemyId);
        }

        [Test]
        public void SettlementCarriesBucketsAndTotals()
        {
            BattleRawData raw = Raw(31.4f, 48210, 14, 91234, 1, 2, 690000,
                new KillBucketCount(new KillBucket(Asteroid, 0, 1, false), 140),
                new KillBucketCount(new KillBucket(Asteroid, 2, 2, true), 3));
            raw.GoldenCritCount = 1;
            raw.GoldenCritBonusGold = 900;
            raw.TimeBonusCount = 22;

            var payload = PayloadOf<SettlementPayloadDto>(Factory(DateTime.UtcNow).Settlement(9, RunId, raw));

            Assert.AreEqual(RunId, payload.runId);
            Assert.AreEqual(31.4f, payload.playedSeconds);
            Assert.AreEqual(48210, payload.earnedGold);
            Assert.AreEqual(91234, payload.exp);
            Assert.AreEqual(14, payload.reachedLevel);
            Assert.AreEqual(1, payload.goldenCritCount);
            Assert.AreEqual(900, payload.goldenCritBonusGold);
            Assert.AreEqual(22, payload.timeBonusCount);
            Assert.AreEqual(690000, payload.settledGold);
            Assert.AreEqual(2, payload.nextGrowthStage);
            Assert.AreEqual(2, payload.kills.Count);
            Assert.AreEqual(2, payload.kills[1].tier);
            Assert.AreEqual(2, payload.kills[1].size);
            Assert.IsTrue(payload.kills[1].golden);
            Assert.AreEqual(3, payload.kills[1].count);
        }

        [Test]
        public void SettlementWithoutKillsHasAnEmptyArray()
        {
            PendingLog log = Factory(DateTime.UtcNow).Settlement(2, RunId, Raw(1f, 0, 0, 0, 0, 0, 0));

            StringAssert.Contains("\"kills\":[]", log.Json);
        }

        private static T PayloadOf<T>(PendingLog log) where T : class
        {
            // payload는 봉투 안의 객체라 JsonUtility로 따로 읽기 위해 글자를 잘라 낸다.
            int start = log.Json.IndexOf("\"payload\":", StringComparison.Ordinal) + "\"payload\":".Length;
            string payload = log.Json.Substring(start, log.Json.Length - start - 1);
            return UnityEngine.JsonUtility.FromJson<T>(payload);
        }
    }
}
