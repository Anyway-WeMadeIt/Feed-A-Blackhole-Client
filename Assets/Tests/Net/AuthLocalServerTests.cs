using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BlackHole.Net.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlackHole.Net.Tests
{
    // 로컬 서버를 상대로 W2-2 완료 조건을 본다. 서버를 켜 두고 테스트 러너에서 골라 돌린다(Explicit).
    //   1) 첫 실행: 등록 → 로그인
    //   2) 재시작: 갱신만(새 refresh 토큰 저장)
    //   3) 저장 전 종료 흉내: 옛 refresh 토큰으로 되돌림 → 갱신 거부(재사용 탐지) → 아이디·시크릿 로그인으로 복구
    // "재시작"은 세션과 저장소를 새로 만들어 흉내 낸다. 메모리 상태 없이 파일만 다음 실행으로 이어진다.
    // 서버 주소는 환경 변수 BLACKHOLE_SERVER_URL, 없으면 http://localhost:8080.
    [Explicit("로컬 서버가 필요하다.")]
    [Category("LocalServer")]
    public sealed class AuthLocalServerTests
    {
        private const int RequestTimeout = 5;
        private const int LoginBudget = 8;

        private static string BaseUrl =>
            Environment.GetEnvironmentVariable("BLACKHOLE_SERVER_URL") is { Length: > 0 } url ? url : "http://localhost:8080";

        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "BlackHole.Net.LocalServer", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [UnityTest]
        public IEnumerator FirstRunRestartAndCrashBeforeSave()
        {
            // 1) 첫 실행
            Launch first = new(BaseUrl, _directory);
            yield return TestTasks.Wait(first.Session.StartAsync(), 15);
            first.AssertOnline();
            CollectionAssert.AreEqual(new[] { ApiPaths.GuestRegister, ApiPaths.GuestLogin }, first.Paths);
            GuestCredentials afterFirst = Store().Load();
            Assert.IsNotNull(afterFirst?.RefreshToken, "아이디·시크릿·refresh 토큰을 저장했다.");

            // 2) 재시작
            Launch restart = new(BaseUrl, _directory);
            yield return TestTasks.Wait(restart.Session.StartAsync(), 15);
            restart.AssertOnline();
            CollectionAssert.AreEqual(new[] { ApiPaths.Refresh }, restart.Paths);
            GuestCredentials afterRestart = Store().Load();
            Assert.AreEqual(afterFirst.GuestId, afterRestart.GuestId, "같은 계정이다.");
            Assert.AreNotEqual(afterFirst.RefreshToken, afterRestart.RefreshToken, "새 refresh 토큰을 저장했다.");

            // 3) 갱신 응답은 받았지만 새 토큰을 저장하기 전에 죽었다: 파일에는 서버가 이미 회전시킨 옛 토큰이 남는다.
            Store().Save(afterRestart.WithRefreshToken(afterFirst.RefreshToken));
            Launch crashed = new(BaseUrl, _directory);
            yield return TestTasks.Wait(crashed.Session.StartAsync(), 15);
            crashed.AssertOnline();
            CollectionAssert.AreEqual(new[] { ApiPaths.Refresh, ApiPaths.GuestLogin }, crashed.Paths);
            Assert.AreEqual(401, crashed.Http.Results[0].StatusCode);
            Assert.AreEqual(ApiErrorCodes.InvalidRefreshToken, crashed.Http.Results[0].Error?.Code);
            GuestCredentials recovered = Store().Load();
            Assert.AreEqual(afterFirst.GuestId, recovered.GuestId, "새 계정을 만들지 않고 복구했다.");
            Assert.AreNotEqual(afterFirst.RefreshToken, recovered.RefreshToken);
        }

        private FileCredentialStore Store() => new(_directory);

        // 앱 한 번의 실행: 전송·저장소·세션을 새로 만든다.
        private sealed class Launch
        {
            public readonly RecordingTransport Http;
            public readonly AuthSession Session;
            private readonly string _baseUrl;

            public Launch(string baseUrl, string directory)
            {
                _baseUrl = baseUrl;
                Http = new RecordingTransport(new UnityHttpTransport(baseUrl));
                Session = new AuthSession(Http, new UnityJsonSerializer(), new FileCredentialStore(directory),
                    RequestTimeout, LoginBudget, Debug.Log, Debug.LogError);
            }

            public string[] Paths => Http.Requests.Select(request => request.Path).ToArray();

            public void AssertOnline() =>
                Assert.AreEqual(ConnectionState.Online, Session.State,
                    $"{Session.OfflineReason}. {_baseUrl}에 서버가 켜져 있는지 본다. 요청: {string.Join(", ", Http.Describe())}");
        }

        // 실제 전송을 감싸 보낸 요청과 받은 결과를 남긴다.
        private sealed class RecordingTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;

            public RecordingTransport(IHttpTransport inner) => _inner = inner;

            public List<HttpRequest> Requests { get; } = new();
            public List<HttpResult> Results { get; } = new();

            public async Task<HttpResult> SendAsync(HttpRequest request)
            {
                Requests.Add(request);
                HttpResult result = await _inner.SendAsync(request);
                Results.Add(result);
                return result;
            }

            public string[] Describe() =>
                Requests.Select((request, i) => i < Results.Count
                    ? $"{request.Path} → {(Results[i].Outcome == HttpOutcome.Response ? Results[i].StatusCode.ToString() : Results[i].Outcome.ToString())}"
                    : $"{request.Path} → (응답 없음)").ToArray();
        }
    }
}
