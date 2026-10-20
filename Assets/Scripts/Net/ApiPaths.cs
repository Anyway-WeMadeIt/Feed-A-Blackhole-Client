namespace BlackHole.Net
{
    // 서버 API 경로(API 명세). baseUrl 뒤에 붙는다.
    public static class ApiPaths
    {
        public const string GuestRegister = "/api/v1/auth/guest/register";
        public const string GuestLogin = "/api/v1/auth/guest/login";
        public const string Refresh = "/api/v1/auth/refresh";
        public const string Sync = "/api/v1/me/sync";
    }
}
