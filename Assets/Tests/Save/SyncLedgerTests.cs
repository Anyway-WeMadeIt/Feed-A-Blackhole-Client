using System;
using BlackHole.Core;
using BlackHole.Net;
using BlackHole.Sync;
using NUnit.Framework;

namespace BlackHole.Save.Tests
{
    // 로그 장부의 응답 반영 규칙(계약 5.7)과 서버 진행도 비교.
    public sealed class SyncLedgerTests
    {
        private static readonly DateTime Now = new(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc);

        private static PendingLog Log(long seq) => new(seq, LogTypes.NodePurchase, "{\"seq\":" + seq + "}");

        private static SyncLedger LedgerWithLogs(int count)
        {
            var ledger = new SyncLedger();

            for (int i = 0; i < count; i++)
                ledger.Append(Log);

            return ledger;
        }

        private static SyncResponseDto Applied(long through) =>
            new() { appliedThroughSeq = through, progress = new ProgressDto { lastAppliedSeq = through } };

        private static SyncResponseDto Rejected(long through, string code) =>
            new()
            {
                appliedThroughSeq = through,
                rejected = new RejectedLogDto { seq = through + 1, type = LogTypes.Settlement, code = code, message = "m" },
                progress = new ProgressDto { lastAppliedSeq = through },
            };

        [Test]
        public void AppendAssignsConsecutiveSeqs()
        {
            SyncLedger ledger = LedgerWithLogs(3);

            Assert.AreEqual(4, ledger.NextSeq);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, new[] { ledger.Pending[0].Seq, ledger.Pending[1].Seq, ledger.Pending[2].Seq });
        }

        [Test]
        public void AppendRejectsALogWithAnotherSeq()
        {
            var ledger = new SyncLedger();

            Assert.Throws<InvalidOperationException>(() => ledger.Append(_ => Log(5)));
            Assert.AreEqual(1, ledger.NextSeq);
            Assert.AreEqual(0, ledger.Pending.Count);
        }

        [Test]
        public void AppliedWithRemainingLogsKeepsThemAndDoesNotOverwrite()
        {
            SyncLedger ledger = LedgerWithLogs(4);

            SyncApplication result = ledger.Apply(Applied(2), localMatchesServer: false, Now);

            Assert.AreEqual(SyncApplyOutcome.Applied, result.Outcome);
            Assert.IsFalse(result.OverwriteProgress);
            CollectionAssert.AreEqual(new long[] { 3, 4 }, new[] { ledger.Pending[0].Seq, ledger.Pending[1].Seq });
            Assert.AreEqual(5, ledger.NextSeq);
        }

        [Test]
        public void AppliedEverythingAndMatchingJustClears()
        {
            SyncLedger ledger = LedgerWithLogs(2);

            SyncApplication result = ledger.Apply(Applied(2), localMatchesServer: true, Now);

            Assert.AreEqual(SyncApplyOutcome.Applied, result.Outcome);
            Assert.IsFalse(result.OverwriteProgress);
            Assert.AreEqual(0, ledger.Pending.Count);
            Assert.AreEqual(3, ledger.NextSeq);
        }

        [Test]
        public void AppliedEverythingButDifferentIsDrift()
        {
            SyncLedger ledger = LedgerWithLogs(2);

            SyncApplication result = ledger.Apply(Applied(2), localMatchesServer: false, Now);

            Assert.AreEqual(SyncApplyOutcome.AppliedWithDrift, result.Outcome);
            Assert.IsTrue(result.OverwriteProgress);
            Assert.AreEqual(3, ledger.NextSeq);
        }

        [Test]
        public void EmptyResponseSetsTheNextSeqFromTheServer()
        {
            // 서버가 이미 20까지 반영한 계정에서, 로컬 장부는 처음 상태로 시작했다.
            var ledger = new SyncLedger();

            ledger.Apply(Applied(20), localMatchesServer: true, Now);

            Assert.AreEqual(21, ledger.NextSeq);
        }

        [Test]
        public void RejectionDiscardsEveryRemainingLogAndOverwrites()
        {
            SyncLedger ledger = LedgerWithLogs(5);

            // 2까지 반영, 3이 거부됐다. 4와 5는 보낸 뒤 쌓인 것까지 모두 버린다.
            SyncApplication result = ledger.Apply(Rejected(2, RejectCodes.SettlementGoldMismatch), localMatchesServer: true, Now);

            Assert.AreEqual(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.IsTrue(result.OverwriteProgress);
            Assert.AreEqual(0, ledger.Pending.Count);
            Assert.AreEqual(3, ledger.NextSeq);
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, ledger.LastRejection.code);
            Assert.IsFalse(ledger.LastRejectionShown);
        }

        [Test]
        public void MarkingTheRejectionShownKeepsIt()
        {
            SyncLedger ledger = LedgerWithLogs(1);
            ledger.Apply(Rejected(0, RejectCodes.RunNotOpen), false, Now);

            ledger.MarkLastRejectionShown();

            Assert.IsTrue(ledger.LastRejectionShown);
            Assert.AreEqual(RejectCodes.RunNotOpen, ledger.LastRejection.code);
        }

        [Test]
        public void NoRejectionMeansNullAndShown()
        {
            var ledger = new SyncLedger();

            Assert.IsNull(ledger.LastRejection);
            Assert.IsTrue(ledger.LastRejectionShown);
            Assert.IsTrue(ledger.IsInitial);
        }

        [Test]
        public void SaveAndLoadKeepEverything()
        {
            SyncLedger ledger = LedgerWithLogs(3);
            ledger.Apply(Rejected(1, RejectCodes.CostMismatch), false, Now);
            ledger.Append(Log);

            var loaded = new SyncLedger(ledger.ToSave());

            Assert.AreEqual(ledger.NextSeq, loaded.NextSeq);
            Assert.AreEqual(1, loaded.Pending.Count);
            Assert.AreEqual(ledger.Pending[0].Json, loaded.Pending[0].Json);
            Assert.AreEqual(RejectCodes.CostMismatch, loaded.LastRejection.code);
            Assert.IsFalse(loaded.LastRejectionShown);
        }

        [Test]
        public void ServerProgressIsEmptyOnlyWithoutAnyHistory()
        {
            Assert.IsTrue(ServerProgress.IsEmpty(new ProgressDto()));
            Assert.IsFalse(ServerProgress.IsEmpty(new ProgressDto { lastAppliedSeq = 3 }));
            Assert.IsFalse(ServerProgress.IsEmpty(new ProgressDto { gold = 1 }));
            Assert.IsFalse(ServerProgress.IsEmpty(new ProgressDto { growthStage = 1 }));

            var withNode = new ProgressDto();
            withNode.nodes.Add(new ProgressNodeDto { nodeId = "timer-01", rank = 1 });
            Assert.IsFalse(ServerProgress.IsEmpty(withNode));
        }

        [Test]
        public void MatchesIgnoresNodeOrderButNotRanks()
        {
            var server = new ProgressDto { gold = 7, growthStage = 1 };
            server.nodes.Add(new ProgressNodeDto { nodeId = "a", rank = 2 });
            server.nodes.Add(new ProgressNodeDto { nodeId = "b", rank = 1 });

            Assert.IsTrue(ServerProgress.Matches(7, 1, new[] { ("b", 1), ("a", 2) }, server));
            Assert.IsFalse(ServerProgress.Matches(7, 1, new[] { ("b", 1), ("a", 1) }, server));
            Assert.IsFalse(ServerProgress.Matches(7, 1, new[] { ("a", 2) }, server));
            Assert.IsFalse(ServerProgress.Matches(8, 1, new[] { ("b", 1), ("a", 2) }, server));
            Assert.IsFalse(ServerProgress.Matches(7, 0, new[] { ("b", 1), ("a", 2) }, server));
        }
    }
}
