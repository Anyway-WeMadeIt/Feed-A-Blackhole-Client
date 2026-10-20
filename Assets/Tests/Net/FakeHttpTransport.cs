using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Net.Unity;

namespace BlackHole.Net.Tests
{
    // 테스트용 전송. 넣어 둔 응답을 순서대로 돌려주고, 받은 요청을 기록한다.
    internal sealed class FakeHttpTransport : IHttpTransport
    {
        private readonly Queue<Task<HttpResult>> _responses = new();
        private readonly List<HttpRequest> _requests = new();

        public IReadOnlyList<HttpRequest> Requests => _requests;

        // 넣어 둔 응답보다 많이 받은 요청 수. 호출하는 쪽이 예외를 삼킬 수 있어서 던지지 않고 센다.
        public int UnexpectedRequests { get; private set; }

        // 요청을 받을 때마다 부른다. 요청 사이에 시계를 움직일 때 쓴다.
        public Action<HttpRequest> Sent { get; set; }

        public void Enqueue(HttpResult result) => _responses.Enqueue(Task.FromResult(result));

        // 테스트가 직접 끝내는 응답. 요청이 겹치는 경우(갱신 단일 비행 등)를 흉내 낸다.
        public TaskCompletionSource<HttpResult> EnqueuePending()
        {
            var pending = new TaskCompletionSource<HttpResult>();
            _responses.Enqueue(pending.Task);
            return pending;
        }

        public Task<HttpResult> SendAsync(HttpRequest request)
        {
            _requests.Add(request);
            Sent?.Invoke(request);

            if (_responses.Count > 0) return _responses.Dequeue();

            UnexpectedRequests++;
            return Task.FromResult(HttpResult.NetworkError("넣어 둔 응답이 없다."));
        }

        // 서버가 답한 응답. Error는 실제 전송과 같은 방식으로 본문에서 읽는다.
        public static HttpResult Respond(long statusCode, string body) =>
            HttpResult.Response(statusCode, body, UnityHttpTransport.ParseError(body));

        // 서버 공통 오류 본문으로 답한 응답.
        public static HttpResult Error(long statusCode, string code, string message = "") =>
            Respond(statusCode, ErrorBody(code, message));

        public static string ErrorBody(string code, string message = "") =>
            new UnityJsonSerializer().ToJson(new ApiErrorResponseDto
            {
                error = new ApiErrorDto { code = code, message = message },
            });
    }
}
