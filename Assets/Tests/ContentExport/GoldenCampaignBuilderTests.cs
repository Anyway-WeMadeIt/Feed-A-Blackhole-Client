using System;
using System.Collections.Generic;
using BlackHole.Net;
using NUnit.Framework;

namespace BlackHole.ContentExport.Tests
{
    // 골든 캠페인 로그 파일(계약 3.8a)의 글자 형식과 입력 검사.
    // 판을 돌려 로그를 만드는 일은 에디터 도구(CampaignLogExport)가 하고 에디터 전용이라 여기서 확인하지 않는다.
    public sealed class GoldenCampaignBuilderTests
    {
        // 생성기가 만든 글자처럼 float이 긴 소수로 적힌 로그. 다시 직렬화하면 달라질 수 있는 글자다.
        private const string ResetJson = "{\"seq\":1,\"type\":\"RESET\",\"occurredAtUtc\":\"2026-01-01T00:00:00Z\",\"buildVersion\":\"golden\",\"contentVersion\":\"abc\",\"payload\":{}}";
        private const string SettlementJson = "{\"seq\":2,\"type\":\"SETTLEMENT\",\"occurredAtUtc\":\"2026-01-01T00:00:01Z\",\"buildVersion\":\"golden\",\"contentVersion\":\"abc\",\"payload\":{\"runId\":\"x\",\"playedSeconds\":12.3000002}}";

        private static List<GoldenCampaignEntry> Entries() => new()
        {
            new GoldenCampaignEntry(new PendingLog(1, "RESET", ResetJson), 0, 0),
            new GoldenCampaignEntry(new PendingLog(2, "SETTLEMENT", SettlementJson), 5, 1),
        };

        private static GoldenCampaignMeta Meta(CampaignStopReason reason = CampaignStopReason.Stalled) =>
            new(battles: 1, maxGrowthStage: 2, reachedGrowthStage: 1, postClearBattles: 0, stopReason: reason);

        private static GoldenFinalState Final() => new(5, 1, new[] { ("a", 2) });

        [Test]
        public void WritesTheFileInTheContractedShape()
        {
            string text = GoldenCampaignBuilder.Build("abc", Meta(), Entries(), Final(), new GoldenTimeSlack(2, 0.5));

            string expected = string.Join("\n", new[]
            {
                "{",
                "  \"formatVersion\": 1,",
                "  \"contentVersion\": \"abc\",",
                "  \"campaign\": {",
                "    \"battles\": 1,",
                "    \"maxGrowthStage\": 2,",
                "    \"reachedGrowthStage\": 1,",
                "    \"cleared\": false,",
                "    \"postClearBattles\": 0,",
                "    \"stopReason\": \"stalled\"",
                "  },",
                "  \"logs\": [",
                "    " + ResetJson + ",",
                "    " + SettlementJson,
                "  ],",
                "  \"checkpoints\": [",
                "    [1, 0, 0],",
                "    [2, 5, 1]",
                "  ],",
                "  \"final\": {",
                "    \"gold\": 5,",
                "    \"growthStage\": 1,",
                "    \"nodes\": [",
                "      {\"nodeId\": \"a\", \"rank\": 2}",
                "    ]",
                "  },",
                "  \"margins\": {",
                "    \"minTimeSlack\": {\"seq\": 2, \"seconds\": 0.5}",
                "  }",
                "}",
                "",
            });

            Assert.AreEqual(expected, text);
        }

        [Test]
        public void LogsAreEmbeddedVerbatim()
        {
            string text = GoldenCampaignBuilder.Build("abc", Meta(), Entries(), Final(), null);

            StringAssert.Contains("\"playedSeconds\":12.3000002", text);
            StringAssert.Contains(SettlementJson, text);
        }

        [Test]
        public void WritesNullSlackWhenThereIsNoSettlement()
        {
            string text = GoldenCampaignBuilder.Build("abc", Meta(), Entries(), Final(), null);

            StringAssert.Contains("\"minTimeSlack\": null", text);
        }

        [Test]
        public void SameInputGivesTheSameText()
        {
            string first = GoldenCampaignBuilder.Build("abc", Meta(), Entries(), Final(), new GoldenTimeSlack(2, 0.5));
            string second = GoldenCampaignBuilder.Build("abc", Meta(), Entries(), Final(), new GoldenTimeSlack(2, 0.5));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void ClearedFollowsTheReachedStage()
        {
            var cleared = new GoldenCampaignMeta(5, 2, 2, 3, CampaignStopReason.Cleared);

            Assert.IsTrue(cleared.Cleared);
            Assert.IsFalse(Meta().Cleared);
            StringAssert.Contains("\"cleared\": true", GoldenCampaignBuilder.Build("abc", cleared, Entries(), Final(), null));
        }

        [TestCase(CampaignStopReason.Cleared, "cleared")]
        [TestCase(CampaignStopReason.Stalled, "stalled")]
        [TestCase(CampaignStopReason.BattleLimit, "battleLimit")]
        [TestCase(CampaignStopReason.Overflow, "overflow")]
        [TestCase(CampaignStopReason.Capped, "capped")]
        public void StopReasonText(CampaignStopReason reason, string expected)
        {
            Assert.AreEqual(expected, GoldenCampaignBuilder.StopReasonText(reason));
            StringAssert.Contains($"\"stopReason\": \"{expected}\"", GoldenCampaignBuilder.Build("abc", Meta(reason), Entries(), Final(), null));
        }

        [Test]
        public void RejectsSeqThatDoesNotStartAtOneAndGrowByOne()
        {
            var skipped = new List<GoldenCampaignEntry>
            {
                new(new PendingLog(1, "RESET", ResetJson), 0, 0),
                new(new PendingLog(3, "SETTLEMENT", SettlementJson), 5, 1),
            };

            Assert.Throws<ArgumentException>(() => GoldenCampaignBuilder.Build("abc", Meta(), skipped, Final(), null));

            var late = new List<GoldenCampaignEntry> { new(new PendingLog(2, "RESET", ResetJson), 0, 0) };
            Assert.Throws<ArgumentException>(() => GoldenCampaignBuilder.Build("abc", Meta(), late, Final(), null));
        }

        [Test]
        public void RejectsEmptyLogsAndMissingContentVersion()
        {
            Assert.Throws<ArgumentException>(() => GoldenCampaignBuilder.Build("abc", Meta(), new List<GoldenCampaignEntry>(), Final(), null));
            Assert.Throws<ArgumentException>(() => GoldenCampaignBuilder.Build("", Meta(), Entries(), Final(), null));
        }

        [Test]
        public void RejectsReachedStageBeyondTheLastStage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoldenCampaignMeta(1, 2, 3, 0, CampaignStopReason.Stalled));
        }
    }
}
