namespace BlackHole.Net
{
    // 이번 실행의 서버 연결 상태. 앱 시작 시 Connecting에서 Online 또는 Offline으로 한 번 정해지고,
    // Online에서 Offline으로만 바뀐다(이번 실행 중에는 다시 연결하지 않는다, 계획서 D9).
    public enum ConnectionState
    {
        Connecting, // 앱 시작 직후, 로그인 중
        Online,
        Offline,
    }

    // Offline인 까닭. 타이틀 표시 문구가 이것으로 갈린다(계약 2.8).
    public enum OfflineReason
    {
        None,                // Offline이 아니다
        Disabled,            // 설정이 꺼져 있음, 밸런스 프로필을 적용한 실행
        Unreachable,         // 네트워크 오류, 타임아웃, 5xx, 로그인 시간 예산 초과
        CredentialsRejected, // 저장된 guestId·guestSecret이 거부됨(새 계정을 만들지 않는다, 계약 2.7)
        ProtocolError,       // 예상하지 못한 응답(그 밖의 4xx, 형식이 맞지 않는 본문). 버그
        ContentOutdated,     // 동기화가 CONTENT_VERSION_MISMATCH로 거부됨(계약 3.6). 클라이언트 업데이트가 필요하다. 이번 실행은 더 보내지 않는다
    }
}
