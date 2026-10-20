using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlackHole.Net
{
    // 로그와 요청 본문을 글자로 합친다(계약 5.5).
    // JsonUtility는 타입마다 다른 payload를 쓰지 못하므로, 봉투는 여기서 쓰고 payload는 IJsonSerializer가 쓴 글자를 그대로 넣는다.
    public static class SyncJson
    {
        private static readonly UTF8Encoding _utf8NoBom = new(false);

        // 로그 하나: {"seq":…,"type":"…","occurredAtUtc":"…","buildVersion":"…","contentVersion":"…","payload":<payloadJson>}
        public static string ComposeLog(long seq, string type, string occurredAtUtc, string buildVersion,
            string contentVersion, string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson))
                throw new ArgumentException("payload JSON이 필요하다(필드가 없으면 \"{}\").", nameof(payloadJson));

            var builder = new StringBuilder(payloadJson.Length + 160);
            builder.Append("{\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"type\":");
            AppendString(builder, type);
            builder.Append(",\"occurredAtUtc\":");
            AppendString(builder, occurredAtUtc);
            builder.Append(",\"buildVersion\":");
            AppendString(builder, buildVersion);
            builder.Append(",\"contentVersion\":");
            AppendString(builder, contentVersion);
            builder.Append(",\"payload\":").Append(payloadJson).Append('}');
            return builder.ToString();
        }

        // 요청 본문: {"logs":[<log1>,<log2>,…]}. 로그 글자를 바꾸지 않고 이어 붙인다.
        public static string ComposeRequest(IEnumerable<string> logJsons)
        {
            var builder = new StringBuilder("{\"logs\":[");
            bool first = true;

            foreach (string log in logJsons)
            {
                if (!first)
                    builder.Append(',');

                builder.Append(log);
                first = false;
            }

            return builder.Append("]}").ToString();
        }

        // 요청 본문의 UTF-8 바이트 수(한도 계산용).
        public static int Utf8Length(string json) => _utf8NoBom.GetByteCount(json ?? "");

        // 로그의 occurredAtUtc 형식: yyyy-MM-ddTHH:mm:ssZ.
        public static string FormatUtc(DateTime utc) =>
            utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        // JSON 문자열(따옴표 포함). null은 ""로 쓴다.
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');

            foreach (char c in value ?? "")
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
