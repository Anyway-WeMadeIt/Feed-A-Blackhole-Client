namespace BlackHole.Net
{
    // 서버 연결 상태를 알릴 때, 이미 알린 상태를 다시 알리지 않게 기억한다(계약 2.8). Unity에 의존하지 않는다.
    // - 처음 물으면 지금 상태의 알림을 돌려준다(없으면 null).
    // - 그다음부터는 알림 글자 키가 바뀌었을 때만 돌려준다. 그래서 타이틀로 돌아오거나 업그레이드 화면에 들어올 때마다 다시 알리지 않고,
    //   플레이 중에 온라인에서 오프라인으로 바뀌면(연결 끊김) 다음 확인 때 한 번 알린다.
    // - 알릴 것이 없는 상태(null 키)로 바뀌어도 그 상태를 기억한다. 예: 온라인을 알린 뒤 콘텐츠 버전 불일치(ContentOutdated)로 바뀌면 연결 알림은 없다.
    //   그 사유는 거부 알림이 알린다.
    public sealed class ConnectionNoticeTracker
    {
        private bool _handled;
        private string _lastKey;

        // 지금 상태에서 새로 알릴 글자 키. 알릴 것이 없거나 이미 알렸으면 null.
        public string Next(ConnectionState state, OfflineReason reason)
        {
            string key = ConnectionNoticeKeys.TextKeyOf(state, reason);

            if (_handled && key == _lastKey)
                return null;

            _handled = true;
            _lastKey = key;
            return key;
        }
    }
}
