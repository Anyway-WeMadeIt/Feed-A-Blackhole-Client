using BlackHole.Net.Unity;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // JsonUtility 구현이 IJsonSerializer 계약(형식이 맞지 않으면 null)을 지키는지.
    public sealed class UnityJsonSerializerTests
    {
        private readonly UnityJsonSerializer _json = new();

        [Test]
        public void BrokenOrEmptyJsonIsNull()
        {
            Assert.IsNull(_json.FromJson<TokenResponseDto>("{\"accessToken\":"));
            Assert.IsNull(_json.FromJson<TokenResponseDto>("Internal Server Error"));
            Assert.IsNull(_json.FromJson<TokenResponseDto>(""));
            Assert.IsNull(_json.FromJson<TokenResponseDto>(null));
        }

        [Test]
        public void DtoFieldNamesAreJsonKeys()
        {
            string json = _json.ToJson(new GuestLoginRequestDto { guestId = "id", guestSecret = "s" });

            Assert.AreEqual("{\"guestId\":\"id\",\"guestSecret\":\"s\"}", json);
        }

        [Test]
        public void ReadsTokenResponse()
        {
            TokenResponseDto dto = _json.FromJson<TokenResponseDto>("{\"accessToken\":\"a\",\"expiresIn\":900,\"refreshToken\":\"r\"}");

            Assert.AreEqual("a", dto.accessToken);
            Assert.AreEqual(900, dto.expiresIn);
            Assert.AreEqual("r", dto.refreshToken);
        }
    }
}
