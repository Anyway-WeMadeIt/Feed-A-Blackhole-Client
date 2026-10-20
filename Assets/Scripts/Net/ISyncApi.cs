using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 동기화 요청 하나(계약 5.6). 구현은 W3-4.
    // 토큰은 IAuthSession에서 얻고, 401 UNAUTHORIZED면 RefreshAfterUnauthorizedAsync 후 같은 요청을 한 번 다시 보낸다.
    // 나눠 보내기(500개, 1 MB)는 호출하는 쪽(SyncSender)이 한다. 여기에는 한도 안의 로그만 넘긴다.
    public interface ISyncApi
    {
        // 예외를 던지지 않는다.
        Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs);
    }

    public enum SyncCallOutcome
    {
        Response,      // 200, 응답 본문을 읽었다
        Unreachable,   // 네트워크 오류, 타임아웃, 5xx → 이번 실행은 오프라인(Unreachable)
        Unauthorized,  // 갱신 후에도 401 → 오프라인(IAuthSession이 이미 바꿈)
        ProtocolError, // 400, 413, 형식이 맞지 않는 200 → 오프라인(ProtocolError), 오류 로그
    }

    public sealed class SyncCallResult
    {
        public SyncCallOutcome Outcome { get; }

        // Outcome이 Response일 때만. 아니면 null.
        public SyncResponseDto Response { get; }

        private SyncCallResult(SyncCallOutcome outcome, SyncResponseDto response)
        {
            Outcome = outcome;
            Response = response;
        }

        public static SyncCallResult Of(SyncResponseDto response) => new(SyncCallOutcome.Response, response);

        public static SyncCallResult Failed(SyncCallOutcome outcome) => new(outcome, null);
    }
}
