using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackHole.Net.Unity;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 로그인 흐름(계약 2.6 표)을 가짜 전송·메모리 저장소로 본다.
    // 동기화 컨텍스트를 비워 두면 가짜 응답의 이어 실행이 곧바로 돌아서 흐름이 한 호출 안에 끝난다.
    public sealed class AuthSessionTests
    {
        private const string Id = "guest-1";
        private const string Secret = "secret-1";
        private const int RequestTimeout = 5;
        private const int LoginBudget = 8;

        private FakeHttpTransport _http;
        private MemoryCredentialStore _store;
        private List<string> _errors;
        private DateTime _time;
        private SynchronizationContext _savedContext;

        [SetUp]
        public void SetUp()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _http = new FakeHttpTransport();
            _store = new MemoryCredentialStore(() => _http.Requests.Count);
            _errors = new List<string>();
            _time = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_savedContext);
            Assert.AreEqual(0, _http.UnexpectedRequests, "넣어 둔 응답보다 요청이 많았다.");
        }

        // ── 앱 시작 ──────────────────────────────────────────────

        [Test]
        public void FirstRunRegistersAndSavesBeforeLogin()
        {
            _http.Enqueue(Registered());
            _http.Enqueue(Tokens("a1", "r1"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            Assert.AreEqual("a1", session.AccessToken);
            CollectionAssert.AreEqual(new[] { ApiPaths.GuestRegister, ApiPaths.GuestLogin }, Paths());
            Assert.IsNull(_http.Requests[0].JsonBody, "등록 요청은 본문이 없다.");
            Assert.AreEqual("{\"guestId\":\"guest-1\",\"guestSecret\":\"secret-1\"}", _http.Requests[1].JsonBody);
            Assert.IsTrue(_http.Requests.All(request => request.BearerToken == null), "인증 요청은 토큰 없이 보낸다.");

            // 등록 응답(요청 1) 뒤, 로그인(요청 2) 전에 아이디·시크릿을 저장했다.
            Assert.AreEqual(2, _store.History.Count);
            AssertSaved(_store.History[0], 1, null);
            AssertSaved(_store.History[1], 2, "r1");
        }

        [Test]
        public void RestartRefreshesAndSavesTheNewToken()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(Tokens("a2", "r2"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            Assert.AreEqual("a2", session.AccessToken);
            CollectionAssert.AreEqual(new[] { ApiPaths.Refresh }, Paths());
            Assert.AreEqual("{\"refreshToken\":\"r1\"}", _http.Requests[0].JsonBody);
            AssertSaved(_store.History.Single(), 1, "r2");
        }

        [Test]
        public void SavedCredentialsWithoutTokenLogIn()
        {
            _store.Preset(new GuestCredentials(Id, Secret));
            _http.Enqueue(Tokens("a1", "r1"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            CollectionAssert.AreEqual(new[] { ApiPaths.GuestLogin }, Paths());
            Assert.AreEqual("r1", _store.Stored.RefreshToken);
        }

        [Test]
        public void RejectedRefreshClearsTheTokenThenLogsIn()
        {
            // 저장 전 종료로 서버가 이미 회전시킨 옛 토큰이 남은 경우 등.
            _store.Preset(new GuestCredentials(Id, Secret, "r-old"));
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidRefreshToken));
            _http.Enqueue(Tokens("a1", "r-new"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            CollectionAssert.AreEqual(new[] { ApiPaths.Refresh, ApiPaths.GuestLogin }, Paths());
            AssertSaved(_store.History[0], 1, null);
            AssertSaved(_store.History[1], 2, "r-new");
            Assert.AreEqual(Id, _store.Stored.GuestId, "같은 계정이다.");
        }

        [Test]
        public void RejectedCredentialsStayOfflineWithoutANewAccount()
        {
            _store.Preset(new GuestCredentials(Id, Secret));
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidCredentials));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Offline, session.State);
            Assert.AreEqual(OfflineReason.CredentialsRejected, session.OfflineReason);
            Assert.IsNull(session.AccessToken);
            CollectionAssert.AreEqual(new[] { ApiPaths.GuestLogin }, Paths(), "등록하지 않는다.");
            Assert.AreEqual(0, _store.History.Count, "계정 정보는 그대로다.");
        }

        [TestCase(0, TestName = "UnreachableKeepsCredentials(NetworkError)")]
        [TestCase(1, TestName = "UnreachableKeepsCredentials(Timeout)")]
        [TestCase(2, TestName = "UnreachableKeepsCredentials(503)")]
        public void UnreachableKeepsCredentials(int kind)
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(kind switch
            {
                0 => HttpResult.NetworkError("연결 실패"),
                1 => HttpResult.Timeout(),
                _ => FakeHttpTransport.Error(503, ApiErrorCodes.InternalError),
            });

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.Unreachable, session.OfflineReason);
            Assert.AreEqual(1, _http.Requests.Count, "로그인으로 넘어가지 않는다.");
            Assert.AreEqual(0, _store.History.Count);
            Assert.AreEqual("r1", _store.Stored.RefreshToken);
        }

        [Test]
        public void UnexpectedRefreshRejectionIsProtocolError()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(FakeHttpTransport.Error(400, ApiErrorCodes.InvalidRequest));

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.ProtocolError, session.OfflineReason);
            Assert.AreEqual(1, _http.Requests.Count);
            Assert.AreEqual(0, _store.History.Count);
            Assert.IsNotEmpty(_errors, "오류 로그를 남긴다.");
        }

        [Test]
        public void MalformedRegisterResponseSavesNothing()
        {
            // 서버와 다른 필드 이름(secret)이면 형식이 맞지 않는 2xx다.
            _http.Enqueue(FakeHttpTransport.Respond(201, "{\"guestId\":\"guest-1\",\"secret\":\"secret-1\"}"));

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.ProtocolError, session.OfflineReason);
            Assert.AreEqual(0, _store.History.Count);
            Assert.IsNotEmpty(_errors);
        }

        [Test]
        public void MalformedTokenResponseIsProtocolError()
        {
            _store.Preset(new GuestCredentials(Id, Secret));
            _http.Enqueue(FakeHttpTransport.Respond(200, "{\"accessToken\":\"a1\"}"));

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.ProtocolError, session.OfflineReason);
            Assert.AreEqual(0, _store.History.Count);
        }

        [Test]
        public void UnsavedRegistrationDoesNotLogIn()
        {
            _store.FailSave = true;
            _http.Enqueue(Registered());

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.Unreachable, session.OfflineReason);
            CollectionAssert.AreEqual(new[] { ApiPaths.GuestRegister }, Paths());
        }

        [Test]
        public void UnsavedTokenStillGoesOnline()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _store.FailSave = true;
            _http.Enqueue(Tokens("a2", "r2"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            Assert.IsNotEmpty(_errors);
        }

        [Test]
        public void RequestTimeoutIsCappedByTheRemainingBudget()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r-old"));
            _http.Sent = _ => _time += TimeSpan.FromSeconds(6.5);
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidRefreshToken));
            _http.Enqueue(Tokens("a1", "r1"));

            AuthSession session = Started();

            Assert.AreEqual(ConnectionState.Online, session.State);
            Assert.AreEqual(RequestTimeout, _http.Requests[0].TimeoutSeconds, "min(요청 5초, 남은 8초)");
            Assert.AreEqual(1, _http.Requests[1].TimeoutSeconds, "min(요청 5초, 남은 1.5초)");
        }

        [Test]
        public void ExhaustedBudgetGoesOfflineWithoutSending()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r-old"));
            _http.Sent = _ => _time += TimeSpan.FromSeconds(LoginBudget);
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidRefreshToken));

            AuthSession session = Started();

            Assert.AreEqual(OfflineReason.Unreachable, session.OfflineReason);
            Assert.AreEqual(1, _http.Requests.Count, "로그인 요청은 보내지 않는다.");
        }

        [Test]
        public void StartsOnlyOnce()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(Tokens("a1", "r2"));
            AuthSession session = Started();

            TestTasks.Done(session.StartAsync());

            Assert.AreEqual(1, _http.Requests.Count);
        }

        [Test]
        public void StateChangesAreRaisedOnce()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(Tokens("a1", "r2"));
            AuthSession session = Session();
            var raised = new List<ConnectionState>();
            session.StateChanged += raised.Add;

            TestTasks.Done(session.StartAsync());
            session.GoOffline(OfflineReason.Unreachable);
            session.GoOffline(OfflineReason.ProtocolError);

            CollectionAssert.AreEqual(new[] { ConnectionState.Online, ConnectionState.Offline }, raised);
            Assert.AreEqual(OfflineReason.Unreachable, session.OfflineReason, "처음 까닭을 유지한다.");
            Assert.IsNull(session.AccessToken);
        }

        // ── 실행 중 401 ──────────────────────────────────────────────

        [Test]
        public void ConcurrentUnauthorizedCallsShareOneRefresh()
        {
            AuthSession session = OnlineWithToken("r1");
            int before = _http.Requests.Count;
            TaskCompletionSource<HttpResult> pending = _http.EnqueuePending();

            Task<bool> first = session.RefreshAfterUnauthorizedAsync();
            Task<bool> second = session.RefreshAfterUnauthorizedAsync();

            Assert.AreEqual(before + 1, _http.Requests.Count, "갱신 요청은 하나만 나간다.");
            Assert.AreSame(first, second, "같은 복구를 함께 기다린다.");
            Assert.IsFalse(first.IsCompleted);

            pending.SetResult(Tokens("a2", "r2"));

            Assert.IsTrue(TestTasks.Done(first));
            Assert.IsTrue(TestTasks.Done(second));
            Assert.AreEqual("a2", session.AccessToken);
            Assert.AreEqual("r2", _store.Stored.RefreshToken);
        }

        [Test]
        public void LaterUnauthorizedStartsANewRefresh()
        {
            AuthSession session = OnlineWithToken("r1");
            _http.Enqueue(Tokens("a2", "r2"));
            _http.Enqueue(Tokens("a3", "r3"));

            Assert.IsTrue(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));
            Assert.IsTrue(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));

            Assert.AreEqual("{\"refreshToken\":\"r2\"}", _http.Requests[2].JsonBody, "앞선 복구가 받은 새 토큰으로 갱신한다.");
            Assert.AreEqual("a3", session.AccessToken);
        }

        [Test]
        public void RecoveryLogsInOnceAfterRejectedRefresh()
        {
            AuthSession session = OnlineWithToken("r1");
            // 복구에는 시간 예산이 없다. 로그인 예산이 한참 지난 뒤에도 요청 타임아웃만 건다.
            _time += TimeSpan.FromHours(1);
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidRefreshToken));
            _http.Enqueue(Tokens("a2", "r2"));

            Assert.IsTrue(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));

            CollectionAssert.AreEqual(new[] { ApiPaths.Refresh, ApiPaths.GuestLogin }, Paths().Skip(1));
            Assert.IsTrue(_http.Requests.Skip(1).All(request => request.TimeoutSeconds == RequestTimeout));
            Assert.AreEqual("a2", session.AccessToken);
        }

        [Test]
        public void FailedRecoveryGoesOffline()
        {
            AuthSession session = OnlineWithToken("r1");
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidRefreshToken));
            _http.Enqueue(FakeHttpTransport.Error(401, ApiErrorCodes.InvalidCredentials));

            Assert.IsFalse(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));

            Assert.AreEqual(ConnectionState.Offline, session.State);
            Assert.AreEqual(OfflineReason.CredentialsRejected, session.OfflineReason);
            Assert.AreEqual(3, _http.Requests.Count, "등록하지 않는다.");
        }

        [Test]
        public void UnreachableRecoveryGoesOffline()
        {
            AuthSession session = OnlineWithToken("r1");
            _http.Enqueue(HttpResult.Timeout());

            Assert.IsFalse(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));

            Assert.AreEqual(OfflineReason.Unreachable, session.OfflineReason);
            Assert.AreEqual("r1", _store.Stored.RefreshToken);
        }

        [Test]
        public void OfflineSessionDoesNotRecover()
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r1"));
            _http.Enqueue(HttpResult.NetworkError("연결 실패"));
            AuthSession session = Started();

            Assert.IsFalse(TestTasks.Done(session.RefreshAfterUnauthorizedAsync()));
            Assert.AreEqual(1, _http.Requests.Count);
        }

        // ── 도우미 ──────────────────────────────────────────────

        private AuthSession Session() =>
            new(_http, new UnityJsonSerializer(), _store, RequestTimeout, LoginBudget, null, _errors.Add, () => _time);

        private AuthSession Started()
        {
            AuthSession session = Session();
            TestTasks.Done(session.StartAsync());
            return session;
        }

        // 갱신으로 온라인이 된 세션. 기록은 그 갱신 요청 하나에서 시작한다.
        private AuthSession OnlineWithToken(string refreshToken)
        {
            _store.Preset(new GuestCredentials(Id, Secret, "r0"));
            _http.Enqueue(Tokens("a1", refreshToken));
            AuthSession session = Started();
            Assert.AreEqual(ConnectionState.Online, session.State);
            return session;
        }

        private string[] Paths() => _http.Requests.Select(request => request.Path).ToArray();

        private static HttpResult Registered() =>
            FakeHttpTransport.Respond(201, $"{{\"guestId\":\"{Id}\",\"guestSecret\":\"{Secret}\"}}");

        private static HttpResult Tokens(string accessToken, string refreshToken) =>
            FakeHttpTransport.Respond(200, $"{{\"accessToken\":\"{accessToken}\",\"expiresIn\":900,\"refreshToken\":\"{refreshToken}\"}}");

        private static void AssertSaved(MemoryCredentialStore.Saved saved, int afterRequests, string refreshToken)
        {
            Assert.AreEqual(Id, saved.Credentials.GuestId);
            Assert.AreEqual(Secret, saved.Credentials.GuestSecret);
            Assert.AreEqual(refreshToken, saved.Credentials.RefreshToken);
            Assert.AreEqual(afterRequests, saved.AfterRequests, "저장한 시점(그때까지 보낸 요청 수)");
        }

        // 저장할 때마다 그때까지 보낸 요청 수를 함께 남긴다(저장 순서 확인).
        private sealed class MemoryCredentialStore : ICredentialStore
        {
            public readonly struct Saved
            {
                public readonly GuestCredentials Credentials;
                public readonly int AfterRequests;

                public Saved(GuestCredentials credentials, int afterRequests)
                {
                    Credentials = credentials;
                    AfterRequests = afterRequests;
                }
            }

            private readonly Func<int> _requestCount;

            public MemoryCredentialStore(Func<int> requestCount) => _requestCount = requestCount;

            public GuestCredentials Stored { get; private set; }
            public List<Saved> History { get; } = new();
            public bool FailSave { get; set; }

            public void Preset(GuestCredentials credentials) => Stored = credentials;

            public GuestCredentials Load() => Stored;

            public void Save(GuestCredentials credentials)
            {
                if (FailSave)
                    throw new System.IO.IOException("저장 공간 부족");

                Stored = credentials;
                History.Add(new Saved(credentials, _requestCount()));
            }
        }
    }
}
