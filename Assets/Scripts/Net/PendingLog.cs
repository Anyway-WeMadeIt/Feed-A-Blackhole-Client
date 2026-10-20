using System;

namespace BlackHole.Net
{
    // 아직 서버가 반영하지 않은 로그 하나. 만들 때 정한 JSON 글자를 바꾸지 않고 그대로 보낸다(계약 C12).
    public sealed class PendingLog
    {
        public long Seq { get; }

        // LogTypes.
        public string Type { get; }

        // SyncJson.ComposeLog의 결과.
        public string Json { get; }

        public PendingLog(long seq, string type, string json)
        {
            if (seq < 1)
                throw new ArgumentOutOfRangeException(nameof(seq), "seq는 1 이상이다.");

            Seq = seq;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }
    }
}
