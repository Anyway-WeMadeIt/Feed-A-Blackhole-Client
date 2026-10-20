using System.IO;
using System.Text;
using BlackHole.Net;
using UnityEngine;

namespace BlackHole.Net.Tests
{
    // 서버 저장소와 같은 동기화 샘플(계약 3.8). 서버의 src/test/resources/sync-samples/와 같은 파일이다.
    internal static class SyncSamples
    {
        private const string Directory = "Assets/Tests/Net/SyncSamples";

        public static string Read(string name) => File.ReadAllText(Path.Combine(Directory, name), Encoding.UTF8);

        // 문자열 밖의 공백을 지운 JSON(키 순서와 값은 그대로).
        public static string Minify(string json)
        {
            var builder = new StringBuilder(json.Length);
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    builder.Append(c);

                    if (c == '\\' && i + 1 < json.Length)
                        builder.Append(json[++i]);
                    else if (c == '"')
                        inString = false;
                }
                else if (c == '"')
                {
                    inString = true;
                    builder.Append(c);
                }
                else if (!char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }

    // 테스트용 IJsonSerializer(JsonUtility). 실제 구현은 BlackHole.Net.Unity에 둔다(W2-1).
    internal sealed class TestJson : IJsonSerializer
    {
        public string ToJson(object value) => JsonUtility.ToJson(value);

        public T FromJson<T>(string json) where T : class => JsonUtility.FromJson<T>(json);
    }
}
