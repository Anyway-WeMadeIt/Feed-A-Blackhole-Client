using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // ISyncApi 구현(계약 5.6). POST /me/sync 하나를 보내고 결과를 네 갈래로 돌려준다.
    // 401이면 IAuthSession으로 갱신한 뒤 같은 본문을 한 번 다시 보낸다.
    // 오프라인 전환(GoOffline)은 하지 않는다. 결과를 받은 SyncSender가 한 곳에서 한다(계약 5.8).
    public sealed class SyncApi : ISyncApi
    {
        private readonly IHttpTransport _transport;
        private readonly IAuthSession _auth;
        private readonly IJsonSerializer _json;
        private readonly int _timeoutSeconds;
        private readonly Action<string> _log;
        private readonly Action<string> _logError;

        // timeoutSeconds는 동기화 요청 타임아웃(ServerSettings.SyncTimeoutSeconds, 계약 C11)이다.
        public SyncApi(
            IHttpTransport transport,
            IAuthSession auth,
            IJsonSerializer json,
            int timeoutSeconds,
            Action<string> log = null,
            Action<string> logError = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _json = json ?? throw new ArgumentNullException(nameof(json));

            if (timeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "1초 이상이어야 한다.");

            _timeoutSeconds = timeoutSeconds;
            _log = log ?? (_ => { });
            _logError = logError ?? (_ => { });
        }

        public async Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs)
        {
            try
            {
                // 로그는 만들 때의 JSON 글자를 그대로 이어 붙인다(계약 C12).
                string body = SyncJson.ComposeRequest(logs.Select(log => log.Json));
                HttpResult result = await SendRequestAsync(body);

                if (result.Outcome == HttpOutcome.Response && result.StatusCode == 401)
                {
                    // 갱신에 실패하면 IAuthSession이 이미 오프라인으로 바꿨다.
                    if (!await _auth.RefreshAfterUnauthorizedAsync())
                        return SyncCallResult.Failed(SyncCallOutcome.Unauthorized);

                    result = await SendRequestAsync(body);
                }

                return Read(result);
            }
            catch (Exception error)
            {
                // 예외를 던지지 않는다(ISyncApi). 여기까지 오면 버그다.
                _logError("[동기화] 요청 중 예외: " + error);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }
        }

        private Task<HttpResult> SendRequestAsync(string body) =>
            _transport.SendAsync(new HttpRequest(HttpRequest.Post, ApiPaths.Sync, body, _auth.AccessToken, _timeoutSeconds));

        private SyncCallResult Read(HttpResult result)
        {
            // 401 응답이면 토큰을 갱신한 뒤 재시도한다. 여기에 걸렸다는 건 재시도 후에도 또 401을 받았다는 것. 실패 처리한다.
            if (result.Outcome == HttpOutcome.Response && result.StatusCode == 401)
            {
                _logError("[동기화] 토큰을 갱신한 뒤에도 401이다.");
                return SyncCallResult.Failed(SyncCallOutcome.Unauthorized);
            }

            // 네트워크 오류, 타임아웃, 5xx.
            if (!result.Reached)
            {
                _log($"[동기화] 서버에 닿지 않았다: {result.Outcome} {result.StatusCode} {result.TransportMessage}");
                return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
            }

            // 400, 413 등은 클라이언트 버그다(계약 3.4). 대기 로그는 지우지 않는다.
            if (result.StatusCode != 200)
            {
                _logError($"[동기화] 요청이 거절되었다: {result.StatusCode} {result.Error?.Code} {result.Error?.Message}");
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            SyncResponseDto response = _json.FromJson<SyncResponseDto>(result.Body);

            // rejected는 없을 수 있다(HasRejection이 null을 거부 없음으로 본다). progress는 항상 있다(계약 3.7).
            if (response == null || response.appliedThroughSeq < 0 || response.progress == null || response.progress.nodes == null)
            {
                _logError("[동기화] 응답의 형식이 맞지 않는다: " + result.Body);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            return SyncCallResult.Of(response);
        }
    }
}
