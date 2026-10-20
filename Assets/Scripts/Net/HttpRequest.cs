using System;

namespace BlackHole.Net
{
    // 서버로 보낼 요청 하나. 전송(IHttpTransport)이 baseUrl 뒤에 Path를 붙여 보낸다.
    public sealed class HttpRequest
    {
        public const string Get = "GET";
        public const string Post = "POST";

        public string Method { get; }

        // baseUrl 뒤에 붙는 경로. 예: /api/v1/auth/guest/login (ApiPaths).
        public string Path { get; }

        // JSON 본문. 없으면 null(Content-Type도 붙이지 않는다).
        public string JsonBody { get; }

        // 액세스 토큰. 있으면 Authorization: Bearer <값>을 붙인다. 없으면 null.
        public string BearerToken { get; }

        // 1 이상.
        public int TimeoutSeconds { get; }

        public HttpRequest(string method, string path, string jsonBody, string bearerToken, int timeoutSeconds)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Path = path ?? throw new ArgumentNullException(nameof(path));

            if (timeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "1초 이상이어야 한다.");

            JsonBody = jsonBody;
            BearerToken = bearerToken;
            TimeoutSeconds = timeoutSeconds;
        }
    }
}
