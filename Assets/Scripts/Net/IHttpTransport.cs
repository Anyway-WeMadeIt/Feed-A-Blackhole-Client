using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 요청 하나를 보내고 결과를 돌려준다. 실제 구현은 BlackHole.Net.Unity(UnityWebRequest), 테스트는 가짜 구현을 쓴다.
    public interface IHttpTransport
    {
        // 예외를 던지지 않는다. 서버에 닿지 않은 경우도 HttpResult(NetworkError, Timeout)로 돌려준다.
        // 서버의 오류 본문은 파싱해 HttpResult.Error에 넣는다.
        Task<HttpResult> SendAsync(HttpRequest request);
    }
}
