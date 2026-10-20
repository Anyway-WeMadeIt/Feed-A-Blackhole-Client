using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 서버가 거부한 것(4xx)과 서버에 닿지 않은 것(네트워크 오류, 타임아웃, 5xx)의 구분(계약 2.1).
    public sealed class HttpResultTests
    {
        [Test]
        public void TwoHundredIsSuccessAndReached()
        {
            HttpResult result = HttpResult.Response(200, "{}");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Reached);
        }

        [Test]
        public void FourHundredIsReachedButNotSuccess()
        {
            HttpResult result = HttpResult.Response(401, "{}", new ApiError(ApiErrorCodes.InvalidRefreshToken, "만료"));

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Reached);
            Assert.AreEqual(ApiErrorCodes.InvalidRefreshToken, result.Error.Code);
        }

        [Test]
        public void FiveHundredIsNotReached()
        {
            Assert.IsFalse(HttpResult.Response(503, "").Reached);
        }

        [Test]
        public void NetworkErrorAndTimeoutAreNotReached()
        {
            Assert.IsFalse(HttpResult.NetworkError("연결 실패").Reached);
            Assert.IsFalse(HttpResult.Timeout().Reached);
            Assert.AreEqual("", HttpResult.Timeout().Body);
        }

        [Test]
        public void CredentialsWithoutRefreshTokenKeepNull()
        {
            var credentials = new GuestCredentials("id", "secret", "");

            Assert.IsNull(credentials.RefreshToken);
            Assert.AreEqual("token", credentials.WithRefreshToken("token").RefreshToken);
            Assert.IsNull(credentials.WithRefreshToken("token").WithRefreshToken(null).RefreshToken);
        }
    }
}
