using System;
using System.Collections.Generic;

namespace BlackHole.Net
{
    // 진행 저장과 함께 있는 로그 저장소(계약 5.6, 5.7, 5.10). 구현은 진행 저장(Assembly-CSharp, W3-3).
    // 메서드는 모두 메인 스레드에서 부른다.
    public interface ISyncStore
    {
        // 아직 서버가 반영하지 않은 로그(seq 오름차순).
        IReadOnlyList<PendingLog> Pending { get; }

        // 새 로그가 쌓였을 때. 온라인이면 전송(SyncSender)이 이것을 보고 보낸다.
        event Action LogAppended;

        // 응답 하나를 반영한다(한 번의 원자적 저장). 규칙은 계약 5.7:
        // - 거부 없음 + 남은 로그 있음: 반영된 로그만 지운다. 진행도는 덮어쓰지 않는다.
        // - 거부 없음 + 남은 로그 없음: 반영된 로그를 지우고, 서버 진행도가 다르면 서버 값으로 맞춘다(AppliedWithDrift).
        // - 거부 있음: 남은 로그를 모두 지우고 서버 진행도로 덮어쓴다. LastRejection에 기록한다.
        SyncApplyOutcome ApplyResponse(SyncResponseDto response);

        // 마지막으로 받은 거부. 없으면 null.
        RejectedLogDto LastRejection { get; }

        // LastRejection을 이미 사용자에게 보여 줬는가.
        bool LastRejectionShown { get; }

        void MarkLastRejectionShown();
    }

    public enum SyncApplyOutcome
    {
        Applied,          // 거부 없음
        AppliedWithDrift, // 거부 없음, 남은 로그가 없는데 서버 진행도가 로컬과 달라 서버 값으로 맞춤(규칙 이식 버그 신호)
        Rejected,         // 거부 있음. 남은 로그 폐기, 서버 진행도로 덮어씀
    }
}
