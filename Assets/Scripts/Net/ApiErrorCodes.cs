namespace BlackHole.Net
{
    // 서버 오류 코드(API 명세 "오류 응답"). 클라이언트가 분기에 쓰는 것만 둔다.
    public static class ApiErrorCodes
    {
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
        public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
