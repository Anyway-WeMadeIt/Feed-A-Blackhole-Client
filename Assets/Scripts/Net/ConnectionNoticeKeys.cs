namespace BlackHole.Net
{
    // 타이틀에서 보여 줄 서버 연결 결과 알림의 글자 키(계약 2.8). 글자 자체는 UI 글자 표(UIText.csv)에 있다.
    public static class ConnectionNoticeKeys
    {
        public const string Online = "sync.connection.online";
        public const string Unreachable = "sync.connection.unreachable";
        public const string AccountUnverified = "sync.connection.account";

        // 이 상태에서 알릴 글자 키. 알릴 것이 없으면 null.
        // - Connecting: 타이틀은 로그인이 끝난 뒤에 열리므로 알리지 않는다.
        // - Offline(Disabled): 서버를 쓰지 않는 실행이라 표시 없음.
        // - Offline(ContentOutdated): 콘텐츠 버전 불일치 거부 알림(RejectionKinds.ContentOutdatedKey)이 같은 내용을 알리므로 여기서는 알리지 않는다.
        public static string TextKeyOf(ConnectionState state, OfflineReason reason)
        {
            switch (state)
            {
                case ConnectionState.Online:
                    return Online;
                case ConnectionState.Offline:
                    switch (reason)
                    {
                        case OfflineReason.Unreachable:
                            return Unreachable;
                        case OfflineReason.CredentialsRejected:
                        case OfflineReason.ProtocolError:
                            return AccountUnverified;
                        default:
                            return null;
                    }
                default:
                    return null;
            }
        }

        // 연결 문제로 알리는 상태인가(성공이 아니라 오프라인 알림).
        public static bool IsProblem(ConnectionState state, OfflineReason reason) =>
            state == ConnectionState.Offline && TextKeyOf(state, reason) != null;
    }
}
