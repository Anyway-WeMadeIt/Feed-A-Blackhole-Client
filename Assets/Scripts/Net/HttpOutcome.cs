namespace BlackHole.Net
{
    // 요청 하나가 어떻게 끝났는가. 서버에 닿지 않은 경우(NetworkError, Timeout)도 예외가 아니라 결과다.
    public enum HttpOutcome
    {
        Response,     // 서버가 답했다(2xx, 4xx, 5xx 모두). StatusCode와 Body를 본다.
        NetworkError, // 연결 실패, DNS 실패 등 서버에 닿지 않았다.
        Timeout,      // 시간 안에 답이 없었다.
    }
}
