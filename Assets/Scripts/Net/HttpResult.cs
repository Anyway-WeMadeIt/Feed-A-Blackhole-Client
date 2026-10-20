namespace BlackHole.Net
{
    // 요청 하나의 결과. 전송(IHttpTransport)이 만든다.
    // 서버의 오류 본문({"error":{"code","message"}})은 전송이 파싱해 Error에 넣는다.
    public sealed class HttpResult
    {
        public HttpOutcome Outcome { get; }

        // Outcome이 Response일 때만 의미 있다.
        public long StatusCode { get; }

        // Outcome이 Response일 때의 본문. 없으면 "".
        public string Body { get; }

        // Outcome이 Response이고 본문이 오류 형식이면 그 내용, 아니면 null.
        public ApiError Error { get; }

        // Outcome이 Response가 아닐 때 원인(디버깅용). 없으면 "".
        public string TransportMessage { get; }

        // 서버가 2xx로 답했다.
        public bool IsSuccess => Outcome == HttpOutcome.Response && StatusCode >= 200 && StatusCode < 300;

        // 서버가 요청을 처리하고 답했다(5xx가 아닌 응답). 4xx도 여기에 든다 — 서버가 거부한 것이다.
        // false면 서버에 닿지 않은 것으로 본다(네트워크 오류, 타임아웃, 5xx).
        public bool Reached => Outcome == HttpOutcome.Response && StatusCode < 500;

        private HttpResult(HttpOutcome outcome, long statusCode, string body, ApiError error, string transportMessage)
        {
            Outcome = outcome;
            StatusCode = statusCode;
            Body = body ?? "";
            Error = error;
            TransportMessage = transportMessage ?? "";
        }

        public static HttpResult Response(long statusCode, string body, ApiError error = null) =>
            new(HttpOutcome.Response, statusCode, body, error, null);

        public static HttpResult NetworkError(string message) =>
            new(HttpOutcome.NetworkError, 0, null, null, message);

        public static HttpResult Timeout(string message = null) =>
            new(HttpOutcome.Timeout, 0, null, null, message);
    }
}
