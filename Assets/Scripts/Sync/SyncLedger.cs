using System;
using System.Collections.Generic;
using BlackHole.Core;
using BlackHole.Net;

namespace BlackHole.Sync
{
    // 로그 장부: 다음 seq, 서버가 아직 반영하지 않은 로그(대기 로그), 마지막 거부.
    // 응답을 반영하는 규칙(계약 5.7)을 가진다. 저장(파일)과 진행도 덮어쓰기는 부르는 쪽(ProgressStore)이 한다.
    // Unity에 의존하지 않아 Editor 테스트가 규칙을 그대로 확인한다. 메인 스레드에서만 부른다.
    public sealed class SyncLedger
    {
        private readonly List<PendingLog> _pending = new();
        private RejectionSaveData _rejection = new();

        // 다음에 만들 로그의 seq.
        public long NextSeq { get; private set; } = 1;

        // 아직 서버가 반영하지 않은 로그(seq 오름차순).
        public IReadOnlyList<PendingLog> Pending => _pending;

        // 마지막으로 받은 거부. 없으면 null.
        public RejectedLogDto LastRejection => string.IsNullOrEmpty(_rejection.Code)
            ? null
            : new RejectedLogDto { seq = _rejection.Seq, type = _rejection.Type, code = _rejection.Code, message = _rejection.Message };

        // 마지막 거부를 사용자에게 이미 알렸는가. 거부가 없으면 true.
        public bool LastRejectionShown => string.IsNullOrEmpty(_rejection.Code) || _rejection.Shown;

        // 처음 상태인가(저장할 동기화 내용이 없다).
        public bool IsInitial => NextSeq == 1 && _pending.Count == 0 && string.IsNullOrEmpty(_rejection.Code);

        public SyncLedger()
        {
        }

        // 저장에서 불러온다. 값은 ProgressSave가 이미 검사했다.
        public SyncLedger(SyncSaveData saved)
        {
            if (saved == null)
                return;

            NextSeq = saved.NextSeq;

            foreach (PendingLogSaveData log in saved.Pending)
                _pending.Add(new PendingLog(log.Seq, log.Type, log.Json));

            _rejection = saved.LastRejection ?? new RejectionSaveData();
        }

        // 새 로그를 만들어 장부에 더한다. make는 이 로그의 seq를 받아 로그를 만든다. 만든 로그의 seq가 다르면 버그다.
        public PendingLog Append(Func<long, PendingLog> make)
        {
            if (make == null)
                throw new ArgumentNullException(nameof(make));

            long seq = NextSeq;
            PendingLog log = make(seq);

            if (log == null || log.Seq != seq)
                throw new InvalidOperationException($"만든 로그의 seq가 장부의 다음 seq({seq})와 다르다: {log?.Seq}.");

            _pending.Add(log);
            NextSeq = seq + 1;
            return log;
        }

        // 서버 응답 하나를 반영한다(계약 5.7). localMatchesServer: 로컬 진행도가 응답의 진행도와 같은가.
        // 거부가 있으면 남은 로그를 모두 지우고 서버 진행도로 덮어쓰라고 알린다. 거부가 없을 때는 남은 로그가 없고 서로 다를 때만 덮어쓴다.
        public SyncApplication Apply(SyncResponseDto response, bool localMatchesServer, DateTime utcNow)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            long applied = response.appliedThroughSeq;
            _pending.RemoveAll(log => log.Seq <= applied);

            if (response.HasRejection)
            {
                RejectedLogDto rejected = response.rejected;
                _pending.Clear();
                NextSeq = applied + 1;
                _rejection = new RejectionSaveData
                {
                    Seq = rejected.seq,
                    Type = rejected.type ?? "",
                    Code = rejected.code,
                    Message = rejected.message ?? "",
                    AtUtc = SyncJson.FormatUtc(utcNow),
                    Shown = false,
                };
                return new SyncApplication(SyncApplyOutcome.Rejected, overwriteProgress: true);
            }

            if (_pending.Count > 0)
            {
                // 로컬 = 서버 + 남은 로그. 덮어쓰면 남은 로그의 효과를 잃는다.
                NextSeq = _pending[_pending.Count - 1].Seq + 1;
                return new SyncApplication(SyncApplyOutcome.Applied, overwriteProgress: false);
            }

            NextSeq = applied + 1;

            return localMatchesServer
                ? new SyncApplication(SyncApplyOutcome.Applied, overwriteProgress: false)
                : new SyncApplication(SyncApplyOutcome.AppliedWithDrift, overwriteProgress: true);
        }

        public void MarkLastRejectionShown() => _rejection.Shown = true;

        // 저장 형식으로. 대기 로그의 글자는 그대로다.
        public SyncSaveData ToSave()
        {
            var save = new SyncSaveData
            {
                NextSeq = NextSeq,
                LastRejection = new RejectionSaveData
                {
                    Seq = _rejection.Seq,
                    Type = _rejection.Type,
                    Code = _rejection.Code,
                    Message = _rejection.Message,
                    AtUtc = _rejection.AtUtc,
                    Shown = _rejection.Shown,
                },
            };

            foreach (PendingLog log in _pending)
                save.Pending.Add(new PendingLogSaveData { Seq = log.Seq, Type = log.Type, Json = log.Json });

            return save;
        }
    }

    // 응답 반영의 결과: 거부 여부와 진행도 덮어쓰기 필요 여부.
    public readonly struct SyncApplication
    {
        public SyncApplyOutcome Outcome { get; }

        // 로컬 진행도를 서버 진행도로 덮어써야 하는가.
        public bool OverwriteProgress { get; }

        public SyncApplication(SyncApplyOutcome outcome, bool overwriteProgress)
        {
            Outcome = outcome;
            OverwriteProgress = overwriteProgress;
        }
    }
}
