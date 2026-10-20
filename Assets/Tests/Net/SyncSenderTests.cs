using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 전송 규칙(계약 5.8, 3.5)을 가짜 ISyncApi와 실제 장부(SyncLedger)로 본다.
    // 동기화 컨텍스트를 비워 두면 가짜 결과의 이어 실행이 곧바로 돌아서 전송이 한 호출 안에 끝난다.
    public sealed class SyncSenderTests
    {
        private const int StartupTimeout = 10;

        private FakeAuthSession _auth;
        private FakeSyncApi _api;
        private LedgerSyncStore _store;
        private List<string> _logs;
        private List<string> _errors;
        private SynchronizationContext _savedContext;

        [SetUp]
        public void SetUp()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _auth = new FakeAuthSession();
            _api = new FakeSyncApi();
            _store = new LedgerSyncStore();
            _logs = new List<string>();
            _errors = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_savedContext);
            Assert.AreEqual(0, _api.UnexpectedCalls, "넣어 둔 결과보다 요청이 많았다.");
        }

        // ── 앱 시작 ──────────────────────────────────────────────

        [Test]
        public void StartupWithoutLogsSendsOneEmptyRequest()
        {
            _api.Enqueue(SyncResults.Applied(0));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count);
            Assert.IsEmpty(_api.Batches[0]);
            Assert.AreEqual(1, _store.Applied.Count, "빈 요청의 응답(서버 진행도)도 반영한다.");
            Assert.IsFalse(sender.IsSending);
        }

        [Test]
        public void StartupSendsLogsQueuedBeforeIt()
        {
            PendingLog first = _store.Append();
            PendingLog second = _store.Append();
            SyncSender sender = Sender();
            Assert.IsEmpty(_api.Batches, "시작 동기화 전에 쌓인 로그는 시작 동기화가 보낸다.");

            _api.Enqueue(SyncResults.Applied(2));
            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count);
            CollectionAssert.AreEqual(new[] { first, second }, _api.Batches[0], "로그는 만들 때의 객체(글자)를 그대로 보낸다.");
            Assert.IsEmpty(_store.Pending);
        }

        [Test]
        public void StartupOfflineSendsNothing()
        {
            _auth = FakeAuthSession.Offline(OfflineReason.Unreachable);
            _store.Append();
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsEmpty(_api.Batches);
            Assert.AreEqual(1, _store.Pending.Count, "오프라인이면 로그를 그대로 둔다.");
        }

        [Test]
        public void StartupRunsOnlyOnce()
        {
            _api.Enqueue(SyncResults.Applied(0));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());
            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count);
        }

        [Test]
        public void StartupStopsWaitingAfterTimeoutAndKeepsTheRequest()
        {
            TaskCompletionSource<SyncCallResult> response = _api.EnqueuePending();
            SyncSender sender = Sender(startupTimeout: 1);

            Task startup = sender.SyncOnStartupAsync();

            Assert.IsTrue(startup.Wait(TimeSpan.FromSeconds(5)), "시작 대기 시간이 지나면 끝나야 한다.");
            Assert.IsTrue(sender.IsSending, "보내던 요청은 계속 기다린다.");
            Assert.IsTrue(_logs.Any(line => line.Contains("시간 안에")));

            response.SetResult(SyncResults.Applied(0));

            Assert.IsFalse(sender.IsSending);
            Assert.AreEqual(1, _store.Applied.Count, "늦게 온 응답도 반영한다.");
        }

        [Test]
        public void LogQueuedWhileTheEmptyStartupRequestIsPendingIsSentAfterIt()
        {
            // 시작 대기 시간이 지나 타이틀로 넘어간 뒤 빈 요청의 응답보다 먼저 새 게임(RESET)을 고른 경우.
            TaskCompletionSource<SyncCallResult> emptyResponse = _api.EnqueuePending();
            _api.Enqueue(SyncResults.Applied(1));
            SyncSender sender = Sender();

            Task startup = sender.SyncOnStartupAsync();
            PendingLog reset = _store.Append();

            Assert.AreEqual(1, _api.Batches.Count, "빈 요청의 응답을 기다리는 동안에는 보내지 않는다.");

            emptyResponse.SetResult(SyncResults.Applied(0));

            Assert.AreEqual(2, _api.Batches.Count, "빈 요청을 반영한 뒤 기다리는 동안 쌓인 로그를 이어서 보낸다.");
            CollectionAssert.AreEqual(new[] { reset }, _api.Batches[1]);
            Assert.IsEmpty(_store.Pending);
            Assert.IsTrue(startup.Wait(TimeSpan.FromSeconds(5)));
        }

        // ── 온라인 중 ────────────────────────────────────────────

        [Test]
        public void LogAppendedWhileOnlineIsSentImmediately()
        {
            SyncSender sender = Started();
            _api.Enqueue(SyncResults.Applied(1));

            PendingLog log = _store.Append();

            Assert.AreEqual(2, _api.Batches.Count);
            CollectionAssert.AreEqual(new[] { log }, _api.Batches[1]);
            Assert.IsEmpty(_store.Pending);
        }

        [Test]
        public void LogsAppendedWhileSendingFollowAfterTheResponse()
        {
            SyncSender sender = Started();
            TaskCompletionSource<SyncCallResult> firstResponse = _api.EnqueuePending();
            _api.Enqueue(SyncResults.Applied(3));

            _store.Append();
            _store.Append();
            _store.Append();

            Assert.AreEqual(2, _api.Batches.Count, "요청은 동시에 하나만 보낸다.");
            Assert.IsTrue(sender.IsSending);

            firstResponse.SetResult(SyncResults.Applied(1));

            Assert.AreEqual(3, _api.Batches.Count);
            CollectionAssert.AreEqual(new long[] { 2, 3 }, _api.Batches[2].Select(log => log.Seq), "응답을 반영한 뒤 남은 로그를 이어서 보낸다.");
            Assert.IsEmpty(_store.Pending);
            Assert.IsFalse(sender.IsSending);
        }

        [Test]
        public void DisposedSenderStopsSending()
        {
            SyncSender sender = Started();
            sender.Dispose();

            _store.Append();

            Assert.AreEqual(1, _api.Batches.Count);
        }

        // ── 나눠 보내기 ──────────────────────────────────────────

        [Test]
        public void SplitsAt500LogsAndSendsTheNextAfterApplying()
        {
            for (int i = 0; i < 501; i++)
                _store.Append();

            _api.Enqueue(SyncResults.Applied(500));
            _api.Enqueue(SyncResults.Applied(501));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            CollectionAssert.AreEqual(new[] { 500, 1 }, _api.Batches.Select(batch => batch.Count));
            Assert.AreEqual(501, _api.Batches[1][0].Seq);
            Assert.AreEqual(2, _store.Applied.Count);
        }

        [Test]
        public void SplitsSoThatTheBodyStaysWithin1MB()
        {
            for (int i = 0; i < 3; i++)
                _store.Append(Padded(400_000));

            _api.Enqueue(SyncResults.Applied(2));
            _api.Enqueue(SyncResults.Applied(3));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            CollectionAssert.AreEqual(new[] { 2, 1 }, _api.Batches.Select(batch => batch.Count));

            foreach (IReadOnlyList<PendingLog> batch in _api.Batches)
            {
                int bytes = SyncJson.Utf8Length(SyncJson.ComposeRequest(batch.Select(log => log.Json)));
                Assert.LessOrEqual(bytes, SyncLimits.MaxRequestBytes);
            }
        }

        [Test]
        public void FillsABatchUpToExactly1MB()
        {
            // 두 로그로 만든 본문 {"logs":[a,b]}가 정확히 1 MB가 되게 맞춘다.
            int empty = SyncJson.Utf8Length(SyncJson.ComposeRequest(Array.Empty<string>()));
            int frame = SyncJson.Utf8Length(Padded(0));
            const int firstPad = 500_000;
            _store.Append(Padded(firstPad));
            _store.Append(Padded(SyncLimits.MaxRequestBytes - empty - 1 - (frame + firstPad) - frame));

            Assert.AreEqual(SyncLimits.MaxRequestBytes,
                SyncJson.Utf8Length(SyncJson.ComposeRequest(_store.Pending.Select(log => log.Json))), "준비가 잘못되었다.");

            _api.Enqueue(SyncResults.Applied(2));
            TestTasks.Done(Sender().SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count, "한도와 같으면 한 요청에 담는다.");
            Assert.AreEqual(2, _api.Batches[0].Count);
        }

        [Test]
        public void ALogLargerThanTheLimitIsAProtocolError()
        {
            _store.Append(Padded(SyncLimits.MaxRequestBytes));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsEmpty(_api.Batches);
            CollectionAssert.AreEqual(new[] { OfflineReason.ProtocolError }, _auth.GoOfflineCalls);
            Assert.AreEqual(1, _store.Pending.Count);
            Assert.AreEqual(1, _errors.Count);
        }

        // ── 응답 ─────────────────────────────────────────────────

        [Test]
        public void RejectionStopsAndDiscardsTheRestButKeepsOnline()
        {
            for (int i = 0; i < 501; i++)
                _store.Append();

            _api.Enqueue(SyncResults.Rejected(1));
            SyncSender sender = Sender();
            var received = new List<RejectedLogDto>();
            sender.RejectionReceived += received.Add;

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count, "거부가 나오면 다음 요청을 보내지 않는다.");
            Assert.IsEmpty(_store.Pending, "남은 로그는 모두 버린다.");
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, received[0].code);
            Assert.IsEmpty(_auth.GoOfflineCalls, "거부는 오프라인 사유가 아니다.");
            Assert.IsFalse(sender.FailedThisRun);

            // 다음 로그는 거부된 seq를 다시 쓰고 곧바로 보낸다.
            _api.Enqueue(SyncResults.Applied(2));
            PendingLog next = _store.Append();

            Assert.AreEqual(2, next.Seq);
            Assert.AreEqual(2, _api.Batches.Count);
        }

        [Test]
        public void RejectionAlsoDiscardsLogsAppendedWhileSending()
        {
            SyncSender sender = Started();
            TaskCompletionSource<SyncCallResult> response = _api.EnqueuePending();

            _store.Append();
            _store.Append();
            Assert.AreEqual(2, _store.Pending.Count);

            response.SetResult(SyncResults.Rejected(0));

            Assert.AreEqual(2, _api.Batches.Count, "거부 뒤에는 기다리는 동안 쌓인 로그도 보내지 않는다.");
            Assert.IsEmpty(_store.Pending, "보낸 뒤 쌓인 로그까지 모두 버린다(계획서 D2).");
            Assert.IsFalse(sender.FailedThisRun);

            // 다음 로그는 서버가 반영한 seq 다음부터 다시 쓴다.
            _api.Enqueue(SyncResults.Applied(1));
            PendingLog next = _store.Append();

            Assert.AreEqual(1, next.Seq);
            Assert.AreEqual(3, _api.Batches.Count);
            Assert.IsEmpty(_store.Pending);
        }

        [Test]
        public void ContentVersionMismatchIsAppliedLikeOtherRejectionsThenGoesOfflineAsContentOutdated()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0, RejectCodes.ContentVersionMismatch));
            SyncSender sender = Sender();
            var received = new List<RejectedLogDto>();
            sender.RejectionReceived += received.Add;

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsEmpty(_store.Pending, "다른 거부처럼 남은 로그를 폐기한다.");
            Assert.AreEqual(RejectCodes.ContentVersionMismatch, _store.LastRejection.code);
            Assert.AreEqual(1, received.Count, "알림은 다른 거부처럼 한 번 온다.");
            CollectionAssert.AreEqual(new[] { OfflineReason.ContentOutdated }, _auth.GoOfflineCalls);
            Assert.AreEqual(OfflineReason.ContentOutdated, _auth.OfflineReason);
            Assert.IsTrue(sender.FailedThisRun);
        }

        [Test]
        public void AfterContentOutdatedNothingMoreIsSentThisRun()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0, RejectCodes.ContentVersionMismatch));
            SyncSender sender = Sender();
            TestTasks.Done(sender.SyncOnStartupAsync());
            int sent = _api.Batches.Count;

            // 오프라인 진행: 로그는 쌓이지만 보내지 않는다. 다음 실행(업데이트 뒤)이 보내면 다시 거부되어 진행도가 서버 값으로 돌아간다.
            _store.Append();

            Assert.AreEqual(sent, _api.Batches.Count);
            Assert.AreEqual(1, _store.Pending.Count);
        }

        [Test]
        public void RejectionHandlerExceptionDoesNotStopTheContentOutdatedSwitch()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0, RejectCodes.ContentVersionMismatch));
            SyncSender sender = Sender();
            sender.RejectionReceived += _ => throw new InvalidOperationException("알림 오류");

            TestTasks.Done(sender.SyncOnStartupAsync());

            CollectionAssert.AreEqual(new[] { OfflineReason.ContentOutdated }, _auth.GoOfflineCalls);
        }

        [Test]
        public void OtherRejectionsKeepTheRunOnline()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0, RejectCodes.NotEnoughGold));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsEmpty(_auth.GoOfflineCalls);
            Assert.IsFalse(sender.FailedThisRun);
        }

        [Test]
        public void RejectionAlreadyShownIsNotRaisedAgain()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0));
            SyncSender sender = Sender();
            int raised = 0;
            sender.RejectionReceived += _ => raised++;
            sender.RejectionReceived += _ => _store.MarkLastRejectionShown();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, raised);
            Assert.IsTrue(_store.LastRejectionShown);
        }

        [Test]
        public void ExceptionInARejectionHandlerDoesNotFailTheSync()
        {
            _store.Append();
            _api.Enqueue(SyncResults.Rejected(0));
            SyncSender sender = Sender();
            sender.RejectionReceived += _ => throw new InvalidOperationException("알림 오류");

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsFalse(sender.FailedThisRun);
            Assert.IsEmpty(_auth.GoOfflineCalls);
            Assert.AreEqual(1, _errors.Count);
        }

        [Test]
        public void DriftIsAppliedWithoutFailing()
        {
            _store.LocalMatchesServer = false;
            _api.Enqueue(SyncResults.Applied(0));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _store.Applied.Count);
            Assert.IsFalse(sender.FailedThisRun);
            Assert.IsEmpty(_auth.GoOfflineCalls);
        }

        [Test]
        public void ServerAheadOfTheLedgerIsAccepted()
        {
            // 저장 실패 등으로 로컬 장부가 서버보다 뒤처지면 서버는 이미 반영한 로그를 건너뛰고 더 큰 seq를 돌려준다.
            _store.Append();
            _store.Append();
            _api.Enqueue(SyncResults.Applied(10));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsFalse(sender.FailedThisRun);
            Assert.IsEmpty(_store.Pending);
            Assert.AreEqual(11, _store.Ledger.NextSeq);
        }

        [Test]
        public void ResponseThatAppliesNothingStopsInsteadOfRepeating()
        {
            _store.Append();
            _store.Append();
            _api.Enqueue(SyncResults.Applied(0));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(1, _api.Batches.Count);
            Assert.IsTrue(sender.FailedThisRun);
            CollectionAssert.AreEqual(new[] { OfflineReason.ProtocolError }, _auth.GoOfflineCalls);
            Assert.AreEqual(2, _store.Pending.Count);
        }

        // ── 실패 ─────────────────────────────────────────────────

        [TestCase(SyncCallOutcome.Unreachable, OfflineReason.Unreachable)]
        [TestCase(SyncCallOutcome.Unauthorized, OfflineReason.ProtocolError)]
        [TestCase(SyncCallOutcome.ProtocolError, OfflineReason.ProtocolError)]
        public void FailureKeepsTheLogsAndStopsForThisRun(SyncCallOutcome outcome, OfflineReason reason)
        {
            _store.Append();
            _api.Enqueue(SyncCallResult.Failed(outcome));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsTrue(sender.FailedThisRun);
            CollectionAssert.AreEqual(new[] { reason }, _auth.GoOfflineCalls);
            Assert.AreEqual(1, _store.Pending.Count, "실패하면 로그를 그대로 두어 다음 실행이 보낸다.");
            Assert.IsEmpty(_store.Applied);

            _store.Append();
            Assert.AreEqual(1, _api.Batches.Count, "이번 실행은 더 보내지 않는다(D9).");
            Assert.AreEqual(outcome == SyncCallOutcome.ProtocolError ? 1 : 0, _errors.Count, "ProtocolError만 오류 로그다.");
        }

        [Test]
        public void FailureAfterAPartialSendKeepsOnlyTheUnsentLogs()
        {
            for (int i = 0; i < 501; i++)
                _store.Append();

            _api.Enqueue(SyncResults.Applied(500));
            _api.Enqueue(SyncCallResult.Failed(SyncCallOutcome.Unreachable));
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.AreEqual(2, _api.Batches.Count);
            Assert.IsTrue(sender.FailedThisRun);
            CollectionAssert.AreEqual(new[] { OfflineReason.Unreachable }, _auth.GoOfflineCalls);
            CollectionAssert.AreEqual(new long[] { 501 }, _store.Pending.Select(log => log.Seq), "반영된 묶음만 지우고 나머지는 다음 실행에 보낸다.");
            Assert.AreEqual(502, _store.Ledger.NextSeq);
        }

        [Test]
        public void ApiExceptionIsAProtocolError()
        {
            _store.Append();
            _api.Throw = new InvalidOperationException("버그");
            SyncSender sender = Sender();

            TestTasks.Done(sender.SyncOnStartupAsync());

            Assert.IsTrue(sender.FailedThisRun);
            CollectionAssert.AreEqual(new[] { OfflineReason.ProtocolError }, _auth.GoOfflineCalls);
            Assert.AreEqual(1, _store.Pending.Count);
        }

        [Test]
        public void GoingOfflineElsewhereStopsSending()
        {
            SyncSender sender = Started();
            _auth.GoOffline(OfflineReason.Unreachable);

            _store.Append();

            Assert.AreEqual(1, _api.Batches.Count);
            Assert.IsFalse(sender.FailedThisRun);
        }

        // ── 도우미 ───────────────────────────────────────────────

        private SyncSender Sender(int startupTimeout = StartupTimeout) =>
            new(_auth, _api, _store, startupTimeout, _logs.Add, _errors.Add);

        // 시작 동기화(빈 요청)를 마친 전송기.
        private SyncSender Started()
        {
            _api.Enqueue(SyncResults.Applied(0));
            SyncSender sender = Sender();
            TestTasks.Done(sender.SyncOnStartupAsync());
            return sender;
        }

        // pad 글자 수만큼 늘인 로그 JSON. seq는 장부가 정하므로 글자에 넣지 않는다.
        private static string Padded(int pad) => "{\"pad\":\"" + new string('a', Math.Max(0, pad)) + "\"}";
    }
}
