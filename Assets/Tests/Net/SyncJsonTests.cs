using System;
using BlackHole.Core;
using BlackHole.Sync;
using NUnit.Framework;
using UnityEngine;

namespace BlackHole.Net.Tests
{
    // 클라이언트가 만드는 로그·요청이 서버 샘플(계약 3.8)과 같은 모양인지, 응답 샘플을 DTO로 읽을 수 있는지(계약 5.4~5.5).
    public sealed class SyncJsonTests
    {
        private static readonly DateTime SampleTime = new(2026, 10, 12, 10, 0, 20, DateTimeKind.Utc);

        private static SyncLogFactory Factory(DateTime now) =>
            new(new TestJson(), "0.4.0", "4d6c398ad1f5e49c", () => now);

        [Test]
        public void NodePurchaseLogMatchesTheSample()
        {
            PendingLog log = Factory(SampleTime).NodePurchase(3, "timer-01", 1, 2);

            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("log-node-purchase.json")), log.Json);
            Assert.AreEqual(3, log.Seq);
            Assert.AreEqual(LogTypes.NodePurchase, log.Type);
        }

        [Test]
        public void ResetLogMatchesTheSample()
        {
            PendingLog log = Factory(new DateTime(2026, 10, 12, 10, 1, 0, DateTimeKind.Utc)).Reset(6);

            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("log-reset.json")), log.Json);
        }

        [Test]
        public void RequestJoinsLogsWithoutChangingThem()
        {
            string body = SyncJson.ComposeRequest(new[] { "{\"seq\":1}", "{\"seq\":2}" });

            Assert.AreEqual("{\"logs\":[{\"seq\":1},{\"seq\":2}]}", body);
            Assert.AreEqual("{\"logs\":[]}", SyncJson.ComposeRequest(Array.Empty<string>()));
            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("sync-request-empty.json")),
                SyncJson.ComposeRequest(Array.Empty<string>()));
        }

        [Test]
        public void EnvelopeStringsAreEscaped()
        {
            string json = SyncJson.ComposeLog(1, "RESET", "t", "a\"b\\c\n\u0001", "v", "{}");
            var parsed = JsonUtility.FromJson<Envelope>(json);

            Assert.AreEqual("a\"b\\c\n\u0001", parsed.buildVersion);
        }

        [Test]
        public void Utf8LengthCountsBytes()
        {
            Assert.AreEqual(3, SyncJson.Utf8Length("abc"));
            Assert.AreEqual(6, SyncJson.Utf8Length("한글"));
        }

        [Test]
        public void AppliedResponseHasNoRejection()
        {
            var response = JsonUtility.FromJson<SyncResponseDto>(SyncSamples.Read("sync-response-applied.json"));

            Assert.IsFalse(response.HasRejection);
            Assert.AreEqual(6, response.appliedThroughSeq);
            Assert.AreEqual(6, response.progress.lastAppliedSeq);
            Assert.AreEqual(0, response.progress.nodes.Count);
        }

        [Test]
        public void RejectedResponseCarriesTheCode()
        {
            var response = JsonUtility.FromJson<SyncResponseDto>(SyncSamples.Read("sync-response-rejected.json"));

            Assert.IsTrue(response.HasRejection);
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, response.rejected.code);
            Assert.AreEqual(2, response.rejected.seq);
            Assert.AreEqual(LogTypes.Settlement, response.rejected.type);
            Assert.AreEqual(1, response.appliedThroughSeq);
        }

        [Test]
        public void SettlementPayloadKeepsTheSampleShape()
        {
            // 샘플의 결산 payload를 DTO로 읽어 다시 쓰고, 다시 읽어도 값이 같은지(키와 타입이 DTO와 맞는지) 본다.
            string sample = SyncSamples.Read("log-settlement.json");
            var envelope = JsonUtility.FromJson<SettlementEnvelope>(sample);
            var again = JsonUtility.FromJson<SettlementPayloadDto>(JsonUtility.ToJson(envelope.payload));

            Assert.AreEqual("6f1c2b0a-3d4e-4f5a-8b6c-7d8e9f0a1b2c", again.runId);
            Assert.AreEqual(12f, again.playedSeconds);
            Assert.AreEqual(1, again.kills.Count);
            Assert.AreEqual("asteroid", again.kills[0].enemyId);
            Assert.AreEqual(20, again.kills[0].count);
            Assert.AreEqual(20, again.earnedGold);
            Assert.AreEqual(20, again.settledGold);
        }

        [Test]
        public void EnemyIdIsTheLowercaseTypeName()
        {
            Assert.AreEqual("asteroid", SyncLogFactory.EnemyIdOf(EnemyType.Asteroid));
            Assert.AreEqual("galaxy", SyncLogFactory.EnemyIdOf(EnemyType.Galaxy));
        }

        [Serializable]
        private sealed class Envelope
        {
            public long seq;
            public string type;
            public string buildVersion;
        }

        [Serializable]
        private sealed class SettlementEnvelope
        {
            public long seq;
            public SettlementPayloadDto payload;
        }
    }
}
