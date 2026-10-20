namespace BlackHole.Net
{
    // 거부된 로그의 type을 알림 문구의 "종류" 글자 키로 바꾼다(계약 5.9). 글자 자체는 UI 글자 표(UIText.csv)에 있다.
    // 모르는 type(서버가 새 type을 더한 경우)은 "기록"이다.
    public static class RejectionKinds
    {
        public const string OtherKey = "sync.rejection.kind.other";

        // 콘텐츠 버전이 서버와 맞지 않을 때의 전용 문구 키. 이 경우 모든 동기화가 거부되고 진행도가 서버 값으로 덮어써지므로
        // 종류·코드 대신 업데이트 안내를 보인다(계약 3.6).
        public const string ContentOutdatedKey = "sync.rejection.contentOutdated";

        public static bool IsContentOutdated(string code) => code == RejectCodes.ContentVersionMismatch;

        public static string TextKeyOf(string logType)
        {
            switch (logType)
            {
                case LogTypes.RunStart: return "sync.rejection.kind.runStart";
                case LogTypes.Settlement: return "sync.rejection.kind.settlement";
                case LogTypes.NodePurchase: return "sync.rejection.kind.nodePurchase";
                case LogTypes.Reset: return "sync.rejection.kind.reset";
                default: return OtherKey;
            }
        }
    }
}
