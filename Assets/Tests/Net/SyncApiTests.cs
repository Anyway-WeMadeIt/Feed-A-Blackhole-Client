using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 동기화 요청 하나(계약 5.6)를 가짜 전송으로 본다: 요청 모양, 401 재시도, 응답별 결과.
    // 오프라인 전환은 SyncSender가 하므로 SyncApi는 GoOffline을 부르지 않아야 한다.
    public sealed class SyncApiTests
    {
        private const int Timeout = 10;

        private FakeHttpTransport _http;
        private FakeAuthSession _auth;
        private List<string> _logs;
        private List<string> _errors;
        private SynchronizationContext _savedContext;

        [SetUp]
        public void SetUp()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _http = new FakeHttpTransport();
            _auth = new FakeAuthSession();
            _logs = new List<string>();
            _errors = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_savedContext);
            Assert.AreEqual(0, _http.UnexpectedRequests, "넣어 둔 응답보다 요청이 많았다.");
            Assert.IsEmpty(_auth.GoOfflineCalls, "SyncApi는 오프라인으로 바꾸지 않는다.");
        }

        // ── 요청 ─────────────────────────────────────────────────

        [Test]
        public void SendsTheLogsVerbatimWithTokenAndSyncTimeout()
        {
            PendingLog[] logs =
            {
                new(1, LogTypes.RunStart, SyncSamples.Minify(SyncSamples.Read("log-run-start.json"))),
                new(2, LogTypes.Settlement, SyncSamples.Minify(SyncSamples.Read("log-settlement.json"))),
            };
            _http.Enqueue(FakeHttpTransport.Respond(200, SyncSamples.Read("sync-response-applied.json")));

            Send(logs);

            HttpRequest request = _http.Requests.Single();
            Assert.AreEqual(HttpRequest.Post, request.Method);
            Assert.AreEqual(ApiPaths.Sync, request.Path);
            Assert.AreEqual("{\"logs\":[" + logs[0].Json + "," + logs[1].Json + "]}", request.JsonBody);
            Assert.AreEqual("access-1", request.BearerToken);
            Assert.AreEqual(Timeout, request.TimeoutSeconds, "동기화 요청 타임아웃을 쓴다(계약 C11).");
        }

        [Test]
        public void EmptyRequestSendsAnEmptyArray()
        {
            _http.Enqueue(FakeHttpTransport.Respond(200, SyncSamples.Read("sync-response-applied.json")));

            Send();

            Assert.AreEqual(SyncSamples.Minify(SyncSamples.Read("sync-request-empty.json")), _http.Requests.Single().JsonBody);
        }

        // ── 응답 ─────────────────────────────────────────────────

        [Test]
        public void AppliedResponseIsRead()
        {
            _http.Enqueue(FakeHttpTransport.Respond(200, SyncSamples.Read("sync-response-applied.json")));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.Response, result.Outcome);
            Assert.AreEqual(6, result.Response.appliedThroughSeq);
            Assert.IsFalse(result.Response.HasRejection, "rejected: null은 거부 없음이다.");
            Assert.AreEqual(6, result.Response.progress.lastAppliedSeq);
        }

        [Test]
        public void RejectedResponseIsAResponseNotAFailure()
        {
            _http.Enqueue(FakeHttpTransport.Respond(200, SyncSamples.Read("sync-response-rejected.json")));

            SyncCallResult result = Send(Log(1), Log(2));

            Assert.AreEqual(SyncCallOutcome.Response, result.Outcome);
            Assert.IsTrue(result.Response.HasRejection);
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, result.Response.rejected.code);
            Assert.AreEqual(1, result.Response.appliedThroughSeq);
        }

        [TestCase("not json")]
        [TestCase("{\"appliedThroughSeq\":-1,\"progress\":{\"nodes\":[]}}")]
        public void MalformedSuccessBodyIsAProtocolError(string body)
        {
            _http.Enqueue(FakeHttpTransport.Respond(200, body));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.ProtocolError, result.Outcome);
            Assert.AreEqual(1, _errors.Count);
        }

        [TestCase(400, ApiErrorCodes.InvalidRequest)]
        [TestCase(413, ApiErrorCodes.PayloadTooLarge)]
        [TestCase(403, "FORBIDDEN")]
        public void ClientErrorIsAProtocolErrorWithTheCodeLogged(int status, string code)
        {
            _http.Enqueue(FakeHttpTransport.Error(status, code, "설명"));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.ProtocolError, result.Outcome);
            Assert.AreEqual(1, _errors.Count);
            StringAssert.Contains(status.ToString(), _errors[0]);
            StringAssert.Contains(code, _errors[0]);
        }

        [Test]
        public void ServerErrorIsUnreachable()
        {
            _http.Enqueue(FakeHttpTransport.Error(503, ApiErrorCodes.InternalError));

            Assert.AreEqual(SyncCallOutcome.Unreachable, Send(Log(1)).Outcome);
            Assert.IsEmpty(_errors, "서버에 닿지 않은 것은 버그가 아니다.");
        }

        [Test]
        public void NetworkErrorIsUnreachable()
        {
            _http.Enqueue(HttpResult.NetworkError("연결 거부"));

            Assert.AreEqual(SyncCallOutcome.Unreachable, Send(Log(1)).Outcome);
        }

        [Test]
        public void TimeoutIsUnreachable()
        {
            _http.Enqueue(HttpResult.Timeout());

            Assert.AreEqual(SyncCallOutcome.Unreachable, Send(Log(1)).Outcome);
        }

        [Test]
        public void TransportExceptionIsAProtocolError()
        {
            var api = new SyncApi(new ThrowingTransport(), _auth, new TestJson(), Timeout, _logs.Add, _errors.Add);

            SyncCallResult result = TestTasks.Done(api.SendAsync(new[] { Log(1) }));

            Assert.AreEqual(SyncCallOutcome.ProtocolError, result.Outcome);
            Assert.AreEqual(1, _errors.Count);
        }

        // ── 401 ──────────────────────────────────────────────────

        [Test]
        public void UnauthorizedRefreshesAndResendsTheSameBodyOnce()
        {
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.Unauthorized));
            _http.Enqueue(FakeHttpTransport.Respond(200, SyncSamples.Read("sync-response-applied.json")));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.Response, result.Outcome);
            Assert.AreEqual(1, _auth.RefreshCalls);
            Assert.AreEqual(2, _http.Requests.Count);
            Assert.AreEqual(_http.Requests[0].JsonBody, _http.Requests[1].JsonBody, "같은 글자를 다시 보낸다.");
            Assert.AreEqual("access-2", _http.Requests[1].BearerToken, "새 토큰으로 보낸다.");
        }

        [Test]
        public void UnauthorizedWithFailedRefreshIsUnauthorized()
        {
            _auth.RefreshSucceeds = false;
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.Unauthorized));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.Unauthorized, result.Outcome);
            Assert.AreEqual(1, _http.Requests.Count, "갱신에 실패하면 다시 보내지 않는다.");
        }

        [Test]
        public void UnauthorizedAgainAfterRefreshIsUnauthorized()
        {
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.Unauthorized));
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.Unauthorized));

            SyncCallResult result = Send(Log(1));

            Assert.AreEqual(SyncCallOutcome.Unauthorized, result.Outcome);
            Assert.AreEqual(1, _auth.RefreshCalls, "갱신은 한 번만 한다.");
            Assert.AreEqual(2, _http.Requests.Count);
            Assert.AreEqual(1, _errors.Count);
        }

        [Test]
        public void RetryAfterRefreshCanStillBeUnreachable()
        {
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.Unauthorized));
            _http.Enqueue(HttpResult.Timeout());

            Assert.AreEqual(SyncCallOutcome.Unreachable, Send(Log(1)).Outcome);
        }

        // ── 도우미 ───────────────────────────────────────────────

        private SyncCallResult Send(params PendingLog[] logs)
        {
            var api = new SyncApi(_http, _auth, new TestJson(), Timeout, _logs.Add, _errors.Add);
            return TestTasks.Done(api.SendAsync(logs));
        }

        private static PendingLog Log(long seq) => new(seq, LogTypes.NodePurchase, "{\"seq\":" + seq + "}");

        private sealed class ThrowingTransport : IHttpTransport
        {
            public System.Threading.Tasks.Task<HttpResult> SendAsync(HttpRequest request) =>
                throw new InvalidOperationException("전송 버그");
        }
    }
}
