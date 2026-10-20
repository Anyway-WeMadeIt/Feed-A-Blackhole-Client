using System;

namespace BlackHole.Net
{
    // 이 설치의 게스트 계정 정보. 파일 형식은 계약 2.5(account.json).
    public sealed class GuestCredentials
    {
        public string GuestId { get; }
        public string GuestSecret { get; }

        // 없으면 null.
        public string RefreshToken { get; }

        public GuestCredentials(string guestId, string guestSecret, string refreshToken = null)
        {
            if (string.IsNullOrEmpty(guestId))
                throw new ArgumentException("guestId가 필요하다.", nameof(guestId));

            if (string.IsNullOrEmpty(guestSecret))
                throw new ArgumentException("guestSecret이 필요하다.", nameof(guestSecret));

            GuestId = guestId;
            GuestSecret = guestSecret;
            RefreshToken = string.IsNullOrEmpty(refreshToken) ? null : refreshToken;
        }

        // refreshToken만 바꾼 새 값. null이면 refreshToken을 지운다.
        public GuestCredentials WithRefreshToken(string refreshToken) => new(GuestId, GuestSecret, refreshToken);
    }
}
