using System;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 처음부터 오프라인인 IAuthSession(계약 2.3: 설정이 꺼져 있음, 밸런스 프로필 적용). 요청을 보내지 않는다.
    public sealed class OfflineAuthSession : IAuthSession
    {
        public OfflineAuthSession(OfflineReason reason) => OfflineReason = reason;

        public ConnectionState State => ConnectionState.Offline;
        public OfflineReason OfflineReason { get; }
        public string AccessToken => null;

        // 상태가 바뀌지 않는다.
        public event Action<ConnectionState> StateChanged { add { } remove { } }

        public Task StartAsync() => Task.CompletedTask;
        public Task<bool> RefreshAfterUnauthorizedAsync() => Task.FromResult(false);
        public void GoOffline(OfflineReason reason) { }
    }
}
