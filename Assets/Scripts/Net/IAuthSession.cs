using System;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 이번 실행의 로그인 세션(계약 2.4, 2.6). 동기화와 타이틀 화면은 이것만 본다 — 토큰과 계정 정보를 직접 다루지 않는다.
    public interface IAuthSession
    {
        ConnectionState State { get; }

        // State가 Offline일 때의 까닭. 아니면 None.
        OfflineReason OfflineReason { get; }

        // State가 바뀔 때. 메인 스레드에서 불린다.
        event Action<ConnectionState> StateChanged;

        // State가 Online일 때 현재 액세스 토큰. 아니면 null.
        string AccessToken { get; }

        // 앱 시작 시 한 번 부른다. 갱신 → 로그인 → 등록 순서(계약 2.6)를 로그인 시간 예산 안에서 진행한다.
        // 끝나면 State가 Online 또는 Offline이다. 예외를 던지지 않는다.
        Task StartAsync();

        // 요청이 401 UNAUTHORIZED를 받았을 때 부른다. 갱신(안 되면 로그인)에 성공하면 true — 같은 요청을 한 번 다시 보낸다.
        // 실패하면 false이고 State는 Offline이 된다. 동시에 불러도 갱신 요청은 하나만 나간다.
        Task<bool> RefreshAfterUnauthorizedAsync();

        // 동기화 등이 서버에 닿지 않았을 때 부른다. 이번 실행은 오프라인이 된다.
        void GoOffline(OfflineReason reason);
    }
}
