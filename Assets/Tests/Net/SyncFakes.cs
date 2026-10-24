using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackHole.Sync;

namespace BlackHole.Net.Tests
{
    // 동기화 테스트(SyncSenderTests, SyncApiTests)가 함께 쓰는 가짜들.

    // 넣어 둔 결과를 순서대로 돌려주고, 받은 로그 묶음을 그때의 사본으로 기록한다.
    internal sealed class FakeSyncApi : ISyncApi
    {
        private readonly Queue<Task<SyncCallResult>> _results = new();
        private readonly List<IReadOnlyList<PendingLog>> _batches = new();

        public IReadOnlyList<IReadOnlyList<PendingLog>> Batches => _batches;

        // 넣어 둔 결과보다 많이 받은 요청 수.
        public int UnexpectedCalls { get; private set; }

        // 요청을 받을 때마다 부른다. 보내는 중에 로그가 쌓이는 경우를 흉내 낸다.
        public Action<IReadOnlyList<PendingLog>> Sent { get; set; }

        // 받을 때 던질 예외(ISyncApi 계약 위반을 흉내 낸다).
        public Exception Throw { get; set; }

        public void Enqueue(SyncCallResult result) => _results.Enqueue(Task.FromResult(result));

        // 테스트가 직접 끝내는 결과. 응답을 기다리는 동안의 동작을 본다.
        public TaskCompletionSource<SyncCallResult> EnqueuePending()
        {
            var pending = new TaskCompletionSource<SyncCallResult>();
            _results.Enqueue(pending.Task);
            return pending;
        }

        public Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs)
        {
            PendingLog[] batch = logs.ToArray();
            _batches.Add(batch);
            Sent?.Invoke(batch);

            if (Throw != null)
                throw Throw;

            if (_results.Count > 0)
                return _results.Dequeue();

            UnexpectedCalls++;
            return Task.FromResult(SyncCallResult.Failed(SyncCallOutcome.Unreachable));
        }
    }

    // 로그인을 마친 세션. GoOffline은 실제 구현처럼 처음 사유를 유지하고, 부른 사유를 모두 기록한다.
    internal sealed class FakeAuthSession : IAuthSession
    {
        private string _token = "access-1";

        public ConnectionState State { get; private set; } = ConnectionState.Online;
        public OfflineReason OfflineReason { get; private set; } = OfflineReason.None;
        public string AccessToken => State == ConnectionState.Online ? _token : null;
        public event Action<ConnectionState> StateChanged;

        public List<OfflineReason> GoOfflineCalls { get; } = new();
        public int RefreshCalls { get; private set; }

        // 401 복구의 결과. 성공이면 새 토큰을 갖고, 실패면 실제 구현처럼 오프라인(Unreachable)이 된다.
        public bool RefreshSucceeds { get; set; } = true;
        public string RefreshedToken { get; set; } = "access-2";

        public static FakeAuthSession Offline(OfflineReason reason)
        {
            var session = new FakeAuthSession();
            session.SetOffline(reason);
            return session;
        }

        public Task StartAsync() => Task.CompletedTask;

        public Task<bool> RefreshAfterUnauthorizedAsync()
        {
            RefreshCalls++;

            if (State != ConnectionState.Online)
                return Task.FromResult(false);

            if (RefreshSucceeds)
            {
                _token = RefreshedToken;
                return Task.FromResult(true);
            }

            SetOffline(OfflineReason.Unreachable);
            return Task.FromResult(false);
        }

        public void GoOffline(OfflineReason reason)
        {
            GoOfflineCalls.Add(reason);
            SetOffline(reason);
        }

        private void SetOffline(OfflineReason reason)
        {
            if (State == ConnectionState.Offline)
                return;

            State = ConnectionState.Offline;
            OfflineReason = reason;
            StateChanged?.Invoke(State);
        }
    }

    // 실제 장부(SyncLedger, 계약 5.7)를 쓰는 메모리 저장소. 진행도 대신 "로컬이 서버와 같은가"만 정해 둔다.
    internal sealed class LedgerSyncStore : ISyncStore
    {
        private static readonly DateTime Now = new(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc);

        public SyncLedger Ledger { get; }

        // ledger를 주면 이미 로그가 쌓인 장부를 그대로 쓴다.
        public LedgerSyncStore(SyncLedger ledger = null) => Ledger = ledger ?? new SyncLedger();

        public IReadOnlyList<PendingLog> Pending => Ledger.Pending;
        public RejectedLogDto LastRejection => Ledger.LastRejection;
        public bool LastRejectionShown => Ledger.LastRejectionShown;
        public event Action LogAppended;

        // 남은 로그가 없을 때 로컬 진행도가 서버와 같은가. false면 AppliedWithDrift가 된다.
        public bool LocalMatchesServer { get; set; } = true;

        public List<SyncResponseDto> Applied { get; } = new();

        // 로그 하나를 쌓고 LogAppended를 올린다(ProgressStore.Commit과 같은 순서). json을 비우면 seq만 담은 글자를 쓴다.
        public PendingLog Append(string json = null)
        {
            PendingLog log = Ledger.Append(seq => new PendingLog(seq, LogTypes.NodePurchase, json ?? "{\"seq\":" + seq + "}"));
            LogAppended?.Invoke();
            return log;
        }

        public SyncApplyOutcome ApplyResponse(SyncResponseDto response)
        {
            Applied.Add(response);
            return Ledger.Apply(response, LocalMatchesServer, Now).Outcome;
        }

        public void MarkLastRejectionShown() => Ledger.MarkLastRejectionShown();
    }

    internal static class SyncResults
    {
        public static SyncCallResult Applied(long through) =>
            SyncCallResult.Of(new SyncResponseDto
            {
                appliedThroughSeq = through,
                progress = new ProgressDto { lastAppliedSeq = through },
            });

        public static SyncCallResult Rejected(long through, string code = RejectCodes.SettlementGoldMismatch) =>
            SyncCallResult.Of(new SyncResponseDto
            {
                appliedThroughSeq = through,
                rejected = new RejectedLogDto { seq = through + 1, type = LogTypes.Settlement, code = code, message = "m" },
                progress = new ProgressDto { lastAppliedSeq = through },
            });
    }
}
