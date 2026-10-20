using System;

namespace BlackHole.Net
{
    // 인증 API의 요청·응답 본문(API 명세 "인증"). 필드 이름이 JSON 키다.

    // POST /auth/guest/register 응답(201).
    [Serializable]
    public sealed class GuestRegisterResponseDto
    {
        public string guestId;
        public string guestSecret;
    }

    // POST /auth/guest/login 요청.
    [Serializable]
    public sealed class GuestLoginRequestDto
    {
        public string guestId;
        public string guestSecret;
    }

    // POST /auth/refresh 요청.
    [Serializable]
    public sealed class RefreshTokenRequestDto
    {
        public string refreshToken;
    }

    // 로그인·갱신 응답(200).
    [Serializable]
    public sealed class TokenResponseDto
    {
        public string accessToken;
        public long expiresIn;
        public string refreshToken;
    }
}
