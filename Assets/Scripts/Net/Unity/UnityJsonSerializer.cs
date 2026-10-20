using System;
using UnityEngine;

namespace BlackHole.Net.Unity
{
    // IJsonSerializer의 JsonUtility 구현.
    public sealed class UnityJsonSerializer : IJsonSerializer
    {
        public string ToJson(object value) => JsonUtility.ToJson(value);

        // JsonUtility는 깨진 JSON에서 예외를 던진다. 계약대로 null로 바꾼다.
        public T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
