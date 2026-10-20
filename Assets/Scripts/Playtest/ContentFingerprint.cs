#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 수치 지문: 이 판이 어떤 수치로 만들어졌는지를 짧게(16진 8자리) 나타낸다.
    // 콘텐츠 저작 데이터(ContentData)와 노드 시트 데이터(NodeContentData)를 공개 필드 이름 순으로 풀어 쓴 글의 SHA-1 앞부분이다.
    // - 값만 본다: CSV 줄 번호(Row)는 넣지 않는다. 같은 값이면 어느 PC·어느 실행에서도 같은 지문이다.
    // - 에셋 YAML이나 JsonUtility를 쓰지 않는다: 객체 참조(instanceID)가 실행마다 달라져 같은 값에서도 지문이 바뀐다.
    // - 목록은 순서대로 쓴다. CSV 행 순서를 바꾸면 지문도 바뀐다(값이 같아도).
    // 메모(M3)와 변경 기록(M4)이 이 지문으로 "어떤 수치였나"를 가리킨다.
    internal static class ContentFingerprint
    {
        public const int Length = 8;
        private const string RowField = "Row";

        public static string Of(ContentData content, NodeContentData nodes)
        {
            var text = new StringBuilder(64 * 1024);
            Write(text, "content", content);
            Write(text, "nodes", nodes);

            using SHA1 sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));

            var hex = new StringBuilder(Length);
            for (int i = 0; i < Length / 2; i++)
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

            return hex.ToString();
        }

        private static void Write(StringBuilder text, string name, object value)
        {
            switch (value)
            {
                case null:
                    text.Append(name).Append("=~\n");
                    return;
                case string s:
                    text.Append(name).Append("=\"").Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")).Append("\"\n");
                    return;
                case float f:
                    text.Append(name).Append('=').Append(f.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                    return;
                case double d:
                    text.Append(name).Append('=').Append(d.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                    return;
                case bool b:
                    text.Append(name).Append('=').Append(b ? "true" : "false").Append('\n');
                    return;
                case Enum e:
                    text.Append(name).Append('=').Append(e.ToString()).Append('\n');
                    return;
                case IFormattable number when value.GetType().IsPrimitive:
                    text.Append(name).Append('=').Append(number.ToString(null, CultureInfo.InvariantCulture)).Append('\n');
                    return;
                case IList list:
                    text.Append(name).Append(".count=").Append(list.Count).Append('\n');
                    for (int i = 0; i < list.Count; i++)
                        Write(text, $"{name}[{i}]", list[i]);
                    return;
            }

            FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            foreach (FieldInfo field in fields)
            {
                if (field.Name == RowField)
                    continue;

                Write(text, $"{name}.{field.Name}", field.GetValue(value));
            }
        }
    }
}
#endif
