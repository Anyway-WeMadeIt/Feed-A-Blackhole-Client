namespace BlackHole.Net
{
    // 서버 공통 오류 형식 {"error":{"code","message"}}의 내용.
    public sealed class ApiError
    {
        // ApiErrorCodes 상수와 비교한다.
        public string Code { get; }

        // 디버깅용. 화면에 보이지 않는다.
        public string Message { get; }

        public ApiError(string code, string message)
        {
            Code = code ?? "";
            Message = message ?? "";
        }
    }
}
