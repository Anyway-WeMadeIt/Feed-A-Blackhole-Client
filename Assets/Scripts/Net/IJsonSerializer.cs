namespace BlackHole.Net
{
    // JSON 변환. 이 어셈블리는 Unity에 의존하지 않으므로 구현(JsonUtility)은 BlackHole.Net.Unity에 있다.
    // DTO는 [Serializable] 클래스의 public 필드로 만든다(JsonUtility 규칙).
    public interface IJsonSerializer
    {
        string ToJson(object value);

        // 형식이 맞지 않으면 null.
        T FromJson<T>(string json) where T : class;
    }
}
