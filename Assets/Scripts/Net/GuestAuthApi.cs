using System;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 인증 요청 하나가 AuthSession에 돌려주는 세 갈래.
    // Rejected: 서버가 4xx로 거부했다. 흐름이 오류 코드를 보고 다음 단계를 정한다.
    // Offline: 흐름을 멈추고 이번 실행은 오프라인이다(서버에 닿지 않음, 형식이 맞지 않는 2xx).
    internal enum AuthResultKind { Success, Rejected, Offline }

    internal sealed class AuthResult
    {
        public AuthResultKind Kind { get; private set; }

        // Rejected일 때. 오류 본문이 아니면 ErrorCode는 null.
        public long StatusCode { get; private set; }
        public string ErrorCode { get; private set; }

        // Offline일 때의 까닭(Unreachable 또는 ProtocolError).
        public OfflineReason OfflineReason { get; private set; }

        // 등록 응답.
        public string GuestId { get; private set; }
        public string GuestSecret { get; private set; }

        // 로그인·갱신 응답.
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        public static AuthResult Registered(string guestId, string guestSecret) =>
            new() { Kind = AuthResultKind.Success, GuestId = guestId, GuestSecret = guestSecret };

        public static AuthResult Tokens(string accessToken, string refreshToken) =>
            new() { Kind = AuthResultKind.Success, AccessToken = accessToken, RefreshToken = refreshToken };

        public static AuthResult Rejected(long statusCode, string errorCode) =>
            new() { Kind = AuthResultKind.Rejected, StatusCode = statusCode, ErrorCode = errorCode };

        public static AuthResult Offline(OfflineReason reason) =>
            new() { Kind = AuthResultKind.Offline, OfflineReason = reason };
    }

    // API 명세 "인증"의 세 요청(등록, 로그인, 갱신). 토큰 없이 보낸다.
    internal sealed class GuestAuthApi
    {
        private readonly IHttpTransport _transport;
        private readonly IJsonSerializer _json;
        private readonly Action<string> _logError;

        public GuestAuthApi(IHttpTransport transport, IJsonSerializer json, Action<string> logError)
        {
            _transport = transport;
            _json = json;
            _logError = logError;
        }

        // 요청 본문은 없다(API 명세).
        public Task<AuthResult> RegisterAsync(int timeoutSeconds) =>
            SendAsync<GuestRegisterResponseDto>(ApiPaths.GuestRegister, null, timeoutSeconds,
                dto => Has(dto.guestId) && Has(dto.guestSecret) ? AuthResult.Registered(dto.guestId, dto.guestSecret) : null);

        public Task<AuthResult> LoginAsync(GuestCredentials credentials, int timeoutSeconds) =>
            SendAsync<TokenResponseDto>(ApiPaths.GuestLogin,
                _json.ToJson(new GuestLoginRequestDto { guestId = credentials.GuestId, guestSecret = credentials.GuestSecret }),
                timeoutSeconds, ReadTokens);

        public Task<AuthResult> RefreshAsync(string refreshToken, int timeoutSeconds) =>
            SendAsync<TokenResponseDto>(ApiPaths.Refresh,
                _json.ToJson(new RefreshTokenRequestDto { refreshToken = refreshToken }),
                timeoutSeconds, ReadTokens);

        private async Task<AuthResult> SendAsync<T>(string path, string jsonBody, int timeoutSeconds, Func<T, AuthResult> read)
            where T : class
        {
            HttpResult result = await _transport.SendAsync(new HttpRequest(HttpRequest.Post, path, jsonBody, null, timeoutSeconds));

            // 네트워크 오류, 타임아웃, 5xx. 계정 정보는 건드리지 않는다.
            if (!result.Reached)
                return AuthResult.Offline(OfflineReason.Unreachable);

            if (!result.IsSuccess)
                return AuthResult.Rejected(result.StatusCode, result.Error?.Code);

            T dto = _json.FromJson<T>(result.Body);
            AuthResult success = dto != null ? read(dto) : null;

            if (success != null)
                return success;

            // 본문에 토큰·시크릿이 있으므로 로그에 남기지 않는다.
            _logError($"[인증] {path} {result.StatusCode} 응답의 형식이 맞지 않는다.");
            return AuthResult.Offline(OfflineReason.ProtocolError);
        }

        private static AuthResult ReadTokens(TokenResponseDto dto) =>
            Has(dto.accessToken) && Has(dto.refreshToken) ? AuthResult.Tokens(dto.accessToken, dto.refreshToken) : null;

        private static bool Has(string value) => !string.IsNullOrEmpty(value);
    }
}
