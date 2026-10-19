using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlackHole.ContentExport
{
    // 키 순서를 지키는 JSON 객체.
    public sealed class JsonObject : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _entries = new();

        public int Count => _entries.Count;

        public void Add(string key, object value) => _entries.Add(new KeyValuePair<string, object>(key, value));

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // 이 값은 여러 줄 모드 안에서도 한 줄로 쓴다(숫자 배열처럼 줄이 길어지는 값에 쓴다). 한 줄 쓰기는 WriteInline과 같다.
    public sealed class JsonInline
    {
        public object Value { get; }

        public JsonInline(object value) => Value = value;
    }

    // 이미 JSON인 글자를 그대로 쓴다(다시 직렬화하면 float 서식이 바뀔 수 있는 값에 쓴다). 글자가 올바른 JSON 값인지는 부르는 쪽이 보장한다.
    public sealed class JsonRaw
    {
        public string Text { get; }

        public JsonRaw(string text) => Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    // 서버 묶음의 JSON 쓰기. Python json.dumps(indent=2, ensure_ascii=False)와 같은 글자를 낸다
    // (서버 저장소의 샘플 묶음과 이전 이관 스크립트가 이 형식이다). 같은 값이면 언제나 같은 바이트다 — 콘텐츠 버전이 바이트로 계산되기 때문이다.
    //
    // 값: JsonObject, IList(배열), string, bool, int, long, float(가장 짧은 왕복 표기), null, JsonInline, JsonRaw.
    public static class PyJson
    {
        public static string Write(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, 0);
            return builder.ToString();
        }

        // 한 줄짜리(들여쓰기 없음, 구분자 ", "와 ": "). layout.json의 노드 한 줄에 쓴다(json.dumps(separators=(", ", ": "))).
        public static string WriteInline(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, -1);
            return builder.ToString();
        }

        // float의 가장 짧은 왕복 표기. 정수면 정수로 쓴다(12 → "12", 0.3f → "0.3").
        public static string Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "JSON에 쓸 수 없는 값이다.");

            if (Math.Abs(value) < 1e15f && value == (float)Math.Truncate(value))
                return ((long)value).ToString(CultureInfo.InvariantCulture);

            for (int digits = 1; digits <= 9; digits++)
            {
                string text = value.ToString("G" + digits, CultureInfo.InvariantCulture);

                if (float.Parse(text, CultureInfo.InvariantCulture) == value)
                    return text.Replace("E", "e");
            }

            return value.ToString("G9", CultureInfo.InvariantCulture).Replace("E", "e");
        }

        // indent가 음수면 한 줄로 쓴다.
        private static void WriteValue(StringBuilder builder, object value, int indent)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case int number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case long number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case float number:
                    builder.Append(Float(number));
                    break;
                case JsonRaw raw:
                    builder.Append(raw.Text);
                    break;
                case JsonInline inline:
                    WriteValue(builder, inline.Value, -1);
                    break;
                case JsonObject obj:
                    WriteObject(builder, obj, indent);
                    break;
                case IList list:
                    WriteArray(builder, list, indent);
                    break;
                default:
                    throw new ArgumentException($"JSON에 쓸 수 없는 타입이다: {value.GetType()}.", nameof(value));
            }
        }

        private static void WriteObject(StringBuilder builder, JsonObject obj, int indent)
        {
            if (obj.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append('{');
            bool first = true;

            foreach (KeyValuePair<string, object> entry in obj)
            {
                if (!first)
                    builder.Append(indent < 0 ? ", " : ",");

                if (indent >= 0)
                    NewLine(builder, indent + 1);

                WriteString(builder, entry.Key);
                builder.Append(": ");
                WriteValue(builder, entry.Value, indent < 0 ? -1 : indent + 1);
                first = false;
            }

            if (indent >= 0)
                NewLine(builder, indent);

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IList list, int indent)
        {
            if (list.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.Append('[');

            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                    builder.Append(indent < 0 ? ", " : ",");

                if (indent >= 0)
                    NewLine(builder, indent + 1);

                WriteValue(builder, list[i], indent < 0 ? -1 : indent + 1);
            }

            if (indent >= 0)
                NewLine(builder, indent);

            builder.Append(']');
        }

        // 줄을 바꾸고 들여쓴다(2칸). 여러 줄 모드에서만 부른다.
        private static void NewLine(StringBuilder builder, int indent)
        {
            builder.Append('\n').Append(' ', indent * 2);
        }

        // ensure_ascii=False: 따옴표, 역슬래시, 제어 문자만 이스케이프하고 나머지(한글 등)는 그대로 쓴다.
        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
