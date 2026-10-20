using System;

namespace BlackHole.Net
{
    // 서버 공통 오류 본문 {"error":{"code","message"}}(API 명세 "오류 응답"). 필드 이름이 JSON 키다.
    // 전송이 이걸로 읽어 HttpResult.Error(ApiError)에 넣는다.
    [Serializable]
    public sealed class ApiErrorResponseDto
    {
        public ApiErrorDto error;
    }

    [Serializable]
    public sealed class ApiErrorDto
    {
        public string code;
        public string message;
    }
}
