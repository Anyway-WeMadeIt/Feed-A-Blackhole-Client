using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 서버 동기화 상태의 저장 형식(저장 포맷 2). 진행도와 같은 파일에 들어가 한 번의 원자적 쓰기로 저장된다.
    // 검사 전 값이다 — 불러올 때 ProgressSave가 검사한다. 포맷 1 파일에는 없고, 그때는 처음 상태(NextSeq 1, 로그 없음)다.
    [Serializable]
    public sealed class SyncSaveData
    {
        // 다음에 만들 로그의 seq. 서버가 반영한 마지막 seq + 1, 대기 로그가 있으면 마지막 대기 로그의 seq + 1이다.
        public long NextSeq = 1;

        // 아직 서버가 반영하지 않은 로그(seq 오름차순, 빈틈 없음).
        public List<PendingLogSaveData> Pending = new();

        // 마지막으로 받은 거부. Code가 ""이면 거부가 없다(JsonUtility는 null 객체를 쓰지 못한다).
        public RejectionSaveData LastRejection = new();
    }

    // 대기 로그 하나. Json은 만들 때 정한 글자 그대로다(재전송해도 같은 글자가 간다).
    [Serializable]
    public sealed class PendingLogSaveData
    {
        public long Seq;
        public string Type;
        public string Json;
    }

    [Serializable]
    public sealed class RejectionSaveData
    {
        public long Seq;
        public string Type = "";
        public string Code = "";
        public string Message = "";
        // 거부를 받은 시각(UTC, yyyy-MM-ddTHH:mm:ssZ).
        public string AtUtc = "";
        // 사용자에게 이미 알렸는가.
        public bool Shown = true;
    }
}
