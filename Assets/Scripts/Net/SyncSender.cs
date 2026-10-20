using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // 대기 로그를 서버로 보내고 응답을 저장소에 반영한다(계약 5.8, W3-4). Unity에 의존하지 않는다.
    // - 앱 시작: 로그인이 끝나면 쌓인 로그를 전부 보낸다(없으면 빈 배열 하나). 끝나거나 syncTimeoutSeconds가 지나면 시작을 푼다.
    // - 온라인 중: LogAppended마다 보낸다. 요청은 동시에 하나만, 보내는 중에 쌓인 로그는 응답을 반영한 뒤 이어서 보낸다.
    // - 나눠 보내기: 500개, 1 MB 이하. 앞 응답을 반영한 뒤 다음을 보내고, 거부가 나오면 멈춘다.
    // - 실패: 로그를 그대로 두고 이번 실행은 더 보내지 않는다(계획서 D9). 오프라인 전환은 여기서만 한다.
    // 메서드와 이벤트는 모두 메인 스레드에서 부른다.
    public sealed class SyncSender : IDisposable
    {
        // 빈 요청 본문 {"logs":[]}의 UTF-8 바이트 수. 본문은 SyncApi가 SyncJson.ComposeRequest로 만든다.
        private static readonly int EmptyRequestBytes = SyncJson.Utf8Length(SyncJson.ComposeRequest(Array.Empty<string>()));

        private readonly IAuthSession _auth;
        private readonly ISyncApi _api;
        private readonly ISyncStore _store;
        private readonly int _startupTimeoutSeconds;
        private readonly Action<string> _log;
        private readonly Action<string> _logError;

        private Task _pump;
        private bool _startupAttempted;
        private bool _failedThisRun;
        private bool _sendRequested;
        private bool _sendEmptyOnNextPump;
        private bool _disposed;

        public bool IsSending => _pump != null && !_pump.IsCompleted;
        public bool FailedThisRun => _failedThisRun;

        // 거부를 반영했을 때. 거부 기록은 ISyncStore.LastRejection에도 남는다(알림은 W3-5).
        public event Action<RejectedLogDto> RejectionReceived;

        // startupTimeoutSeconds는 시작 동기화를 기다리는 최대 시간(ServerSettings.SyncTimeoutSeconds, 계약 2.8)이다.
        public SyncSender(
            IAuthSession auth,
            ISyncApi api,
            ISyncStore store,
            int startupTimeoutSeconds,
            Action<string> log = null,
            Action<string> logError = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _store = store ?? throw new ArgumentNullException(nameof(store));

            if (startupTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(startupTimeoutSeconds), "1초 이상이어야 한다.");

            _startupTimeoutSeconds = startupTimeoutSeconds;
            _log = log ?? (_ => { });
            _logError = logError ?? (_ => { });

            _store.LogAppended += OnLogAppended;
        }

        // 로그인(IAuthSession.StartAsync)이 끝난 뒤 한 번 부른다. 오프라인이면 보내지 않고 바로 끝난다.
        // 동기화가 끝나거나 시작 대기 시간이 지나면 끝난다. 시간이 지나도 보내던 요청은 계속 처리한다.
        public Task SyncOnStartupAsync()
        {
            ThrowIfDisposed();

            if (_startupAttempted)
                return Task.CompletedTask;

            _startupAttempted = true;
            _sendRequested = true;
            _sendEmptyOnNextPump = true;

            return WaitForStartupAsync(EnsurePump());
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _store.LogAppended -= OnLogAppended;
            RejectionReceived = null;
        }

        private async Task WaitForStartupAsync(Task pump)
        {
            Task timeout = Task.Delay(TimeSpan.FromSeconds(_startupTimeoutSeconds));

            if (await Task.WhenAny(pump, timeout) == timeout)
                _log("[동기화] 시작 동기화가 시간 안에 끝나지 않아 먼저 시작한다. 보내던 요청은 계속 처리한다.");
        }

        // 시작 동기화 전에 쌓인 로그는 시작 동기화가 함께 보낸다.
        private void OnLogAppended()
        {
            if (_disposed || _failedThisRun || !_startupAttempted || _auth.State != ConnectionState.Online)
                return;

            _sendRequested = true;
            EnsurePump();
        }

        // 보내는 중이면 그 흐름에 합친다(동시에 하나만, 계약 C26).
        private Task EnsurePump()
        {
            if (_disposed || _failedThisRun)
                return Task.CompletedTask;

            if (_pump == null || _pump.IsCompleted)
                _pump = PumpAsync();

            return _pump;
        }

        private async Task PumpAsync()
        {
            try
            {
                while (!_disposed && !_failedThisRun && _sendRequested)
                {
                    _sendRequested = false;
                    bool sendEmpty = _sendEmptyOnNextPump;
                    _sendEmptyOnNextPump = false;

                    if (_auth.State != ConnectionState.Online)
                        return;

                    // 시작 동기화는 로그가 없어도 빈 배열을 보내 서버 진행도를 받는다.
                    if (_store.Pending.Count == 0)
                    {
                        if (sendEmpty && !await SendAndApplyAsync(Array.Empty<PendingLog>()))
                            return;

                        continue;
                    }

                    while (!_disposed && !_failedThisRun && _auth.State == ConnectionState.Online && _store.Pending.Count > 0)
                    {
                        List<PendingLog> batch = CreateBatch(_store.Pending);

                        if (batch.Count == 0)
                        {
                            Fail(SyncCallOutcome.ProtocolError, $"로그 하나(seq {_store.Pending[0].Seq})가 요청 한도(1 MB)를 넘는다.");
                            return;
                        }

                        // 거부가 나오면 남은 로그는 ApplyResponse가 이미 지웠다. 멈춘다.
                        if (!await SendAndApplyAsync(batch))
                            return;

                        // 거부 없이 반영했는데 첫 로그가 그대로면 같은 요청을 되풀이하게 된다.
                        if (_store.Pending.Count > 0 && _store.Pending[0].Seq <= batch[0].Seq)
                        {
                            Fail(SyncCallOutcome.ProtocolError, $"응답을 반영한 뒤에도 seq {batch[0].Seq}가 남아 있다.");
                            return;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Fail(SyncCallOutcome.ProtocolError, "동기화 중 예외: " + error);
            }
        }

        // 요청 하나를 보내고 응답을 반영한다. 거부 없이 반영했으면 true(이어서 보내도 된다).
        private async Task<bool> SendAndApplyAsync(IReadOnlyList<PendingLog> batch)
        {
            SyncCallResult result = await _api.SendAsync(batch);

            if (result == null || result.Outcome != SyncCallOutcome.Response || result.Response == null)
            {
                SyncCallOutcome outcome = result == null || result.Outcome == SyncCallOutcome.Response
                    ? SyncCallOutcome.ProtocolError
                    : result.Outcome;
                Fail(outcome, $"요청 실패: {outcome}");
                return false;
            }

            // AppliedWithDrift의 경고 로그는 저장소가 남긴다.
            if (_store.ApplyResponse(result.Response) != SyncApplyOutcome.Rejected)
                return true;

            // CONTENT_VERSION_MISMATCH도 다른 거부와 똑같이 반영한다(대기 로그 폐기, 서버 진행도 채택). 아래에서 이번 실행을 오프라인으로 바꾼다.
            RejectedLogDto rejection = _store.LastRejection;
            _log($"[동기화] seq {rejection?.seq} {rejection?.type} 거부: {rejection?.code}. 남은 로그를 버리고 서버 진행도를 따른다.");

            if (rejection != null && !_store.LastRejectionShown)
            {
                // 알림 쪽의 예외가 반영을 마친 동기화를 실패로 만들지 않게 한다.
                try
                {
                    RejectionReceived?.Invoke(rejection);
                }
                catch (Exception error)
                {
                    _logError("[동기화] 거부 알림 처리 중 예외: " + error);
                }
            }

            // 콘텐츠 버전이 다르면 이후 모든 로그가 거부되어 진행도가 매번 서버 값으로 돌아간다. 이번 실행은 더 보내지 않고 오프라인으로 둔다
            // (계약 3.6). 알림을 이미 보였든 아니든 전환은 한다. 오프라인 진행의 로그는 쌓아 두지만 버전이 맞는 업데이트 뒤 보내면 다시 거부된다.
            if (rejection != null && RejectionKinds.IsContentOutdated(rejection.code))
            {
                _log("[동기화] 콘텐츠 버전이 서버와 달라 이번 실행은 오프라인으로 전환한다. 업데이트가 필요하다.");
                StopForThisRun(OfflineReason.ContentOutdated);
            }

            return false;
        }

        // seq 순서대로 500개, 본문 1 MB 이하(계약 3.5). 로그 글자는 그대로 이어 붙이므로 크기를 미리 셀 수 있다.
        private static List<PendingLog> CreateBatch(IReadOnlyList<PendingLog> pending)
        {
            var batch = new List<PendingLog>();
            int bytes = EmptyRequestBytes;

            for (int i = 0; i < pending.Count && batch.Count < SyncLimits.MaxLogsPerRequest; i++)
            {
                // 두 번째 로그부터 앞에 쉼표 1바이트가 붙는다.
                int added = SyncJson.Utf8Length(pending[i].Json) + (batch.Count > 0 ? 1 : 0);

                if (bytes + added > SyncLimits.MaxRequestBytes)
                    break;

                batch.Add(pending[i]);
                bytes += added;
            }

            return batch;
        }

        // 이번 실행은 더 보내지 않는다. 로그는 그대로 두어 다음 실행이 보낸다(계획서 D9).
        private void Fail(SyncCallOutcome outcome, string message)
        {
            if (_failedThisRun)
                return;

            if (outcome == SyncCallOutcome.ProtocolError)
                _logError("[동기화] " + message);
            else
                _log("[동기화] " + message);

            // 갱신 실패로 끝난 Unauthorized는 IAuthSession이 이미 오프라인이고 처음 사유를 유지한다.
            // 갱신한 뒤에도 401인 경우만 아직 온라인이며, 예상하지 못한 응답이므로 ProtocolError다.
            StopForThisRun(outcome == SyncCallOutcome.Unreachable ? OfflineReason.Unreachable : OfflineReason.ProtocolError);
        }

        // 이번 실행은 더 보내지 않고 오프라인으로 알린다.
        private void StopForThisRun(OfflineReason reason)
        {
            if (_failedThisRun)
                return;

            _failedThisRun = true;
            _sendRequested = false;
            _auth.GoOffline(reason);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SyncSender));
        }
    }
}
