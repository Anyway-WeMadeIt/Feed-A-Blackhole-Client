using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine; // AsyncOperation을 await하는 확장
using UnityEngine.Networking;

namespace BlackHole.Net.Unity
{
    // IHttpTransport의 UnityWebRequest 구현(계약 2.4). 통계 전송(AnalyticsClient)과 같은 방식이다.
    // 예외를 던지지 않고, 서버에 닿지 않은 경우도 HttpResult(NetworkError, Timeout)로 돌려준다.
    public sealed class UnityHttpTransport : IHttpTransport
    {
        // UnityWebRequest는 시간 초과를 ConnectionError로 알리고 error에 "Request timeout"을 넣는다.
        private const string TimeoutWord = "timeout";

        private static readonly UnityJsonSerializer Json = new();

        private readonly string _baseUrl;

        // baseUrl 예: http://localhost:8080 (경로 없이)
        public UnityHttpTransport(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl이 필요하다.", nameof(baseUrl));

            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<HttpResult> SendAsync(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                using var webRequest = new UnityWebRequest(_baseUrl + request.Path, request.Method)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = request.TimeoutSeconds,
                };

                if (request.JsonBody != null)
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.JsonBody));
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                }

                if (!string.IsNullOrEmpty(request.BearerToken))
                    webRequest.SetRequestHeader("Authorization", "Bearer " + request.BearerToken);

                await webRequest.SendWebRequest();

                return ToResult(webRequest.result, webRequest.responseCode, webRequest.error, webRequest.downloadHandler.text);
            }
            catch (Exception e)
            {
                // 잘못된 URL 등. 계약상 밖으로 던지지 않는다.
                return HttpResult.NetworkError(e.Message);
            }
        }

        // UnityWebRequest의 결과를 HttpResult로 나눈다. 네트워크 없이 테스트하려고 떼어 뒀다.
        internal static HttpResult ToResult(UnityWebRequest.Result result, long statusCode, string error, string body)
        {
            // 4xx·5xx(ProtocolError)도 서버가 답한 것이다. 연결 실패·시간 초과만 닿지 않은 것이다.
            if (result == UnityWebRequest.Result.ConnectionError
                || result == UnityWebRequest.Result.DataProcessingError)
            {
                bool timedOut = error != null && error.IndexOf(TimeoutWord, StringComparison.OrdinalIgnoreCase) >= 0;
                return timedOut ? HttpResult.Timeout(error) : HttpResult.NetworkError(error);
            }

            return HttpResult.Response(statusCode, body, ParseError(body));
        }

        // 오류 형식이 아니면 null.
        internal static ApiError ParseError(string body)
        {
            ApiErrorResponseDto dto = Json.FromJson<ApiErrorResponseDto>(body);

            // JsonUtility는 본문에 없는 객체 필드도 빈 객체로 채운다. code가 있어야 오류 형식이다.
            if (string.IsNullOrEmpty(dto?.error?.code)) return null;

            return new ApiError(dto.error.code, dto.error.message);
        }
    }
}
