// 헤드리스 테스트 전용. Unity 직렬화 자체의 검증은 에디터에서 한다.
namespace UnityEngine
{
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }); }
            catch (System.Text.Json.JsonException e) { throw new System.ArgumentException(e.Message, e); }
        }
    }
}
