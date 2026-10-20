using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // IAuthSession 구현(계약 2.4, 2.6). 앱 시작 시 갱신 → 로그인 → (계정 정보가 없으면) 등록 후 로그인.
    // 저장 순서: 등록에 성공하면 로그인하기 전에 아이디·시크릿을, 새 refresh 토큰은 다른 일을 하기 전에 저장한다.
    // 서버에 닿지 않으면(네트워크 오류, 타임아웃, 5xx) 이번 실행은 오프라인이고 계정 정보는 그대로 둔다.
    public sealed class AuthSession : IAuthSession
    {
        private readonly GuestAuthApi _api;
        private readonly ICredentialStore _store;
        private readonly int _requestTimeoutSeconds;
        private readonly TimeSpan _loginBudget;
        private readonly Action<string> _log;
        private readonly Action<string> _logError;
        private readonly Func<DateTime> _now;
        private readonly SynchronizationContext _mainThread;
        private readonly object _gate = new();

        // 저장소 내용의 메모리 사본. 저장에 실패해도 이번 실행은 이 값으로 이어 간다.
        private GuestCredentials _credentials;
        private string _accessToken;
        private bool _started;
        // 진행 중인 401 복구. 겹친 호출은 이것을 함께 기다린다.
        private Task<bool> _recovering;

        public ConnectionState State { get; private set; } = ConnectionState.Connecting;
        public OfflineReason OfflineReason { get; private set; } = OfflineReason.None;
        public string AccessToken => State == ConnectionState.Online ? _accessToken : null;
        public event Action<ConnectionState> StateChanged;

        // 메인 스레드에서 만든다(StateChanged를 메인 스레드로 보낸다). 시간은 ServerSettings의 값을 넘긴다.
        // now는 테스트가 시간 예산을 흉내 낼 때 넣는다.
        public AuthSession(
            IHttpTransport transport,
            IJsonSerializer json,
            ICredentialStore store,
            int requestTimeoutSeconds,
            int loginBudgetSeconds,
            Action<string> log = null,
            Action<string> logError = null,
            Func<DateTime> now = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (requestTimeoutSeconds < 1) throw new ArgumentOutOfRangeException(nameof(requestTimeoutSeconds), "1초 이상이어야 한다.");
            if (loginBudgetSeconds < 1) throw new ArgumentOutOfRangeException(nameof(loginBudgetSeconds), "1초 이상이어야 한다.");

            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log ?? (_ => { });
            _logError = logError ?? (_ => { });
            _api = new GuestAuthApi(transport, json, _logError);
            _requestTimeoutSeconds = requestTimeoutSeconds;
            _loginBudget = TimeSpan.FromSeconds(loginBudgetSeconds);
            _now = now ?? (() => DateTime.UtcNow);
            _mainThread = SynchronizationContext.Current;
        }

        public async Task StartAsync()
        {
            lock (_gate)
            {
                // 로그인은 앱 시작 시 한 번만 한다(계획서 D9).
                if (_started)
                    return;

                _started = true;
            }

            OfflineReason reason;
            try
            {
                _credentials = _store.Load();
                DateTime deadline = _now() + _loginBudget;
                reason = await SignInAsync(true, () => TimeoutWithin(deadline));
            }
            catch (Exception error)
            {
                // 예상하지 못한 예외는 버그다.
                _logError("[인증] 로그인 중 예외: " + error);
                reason = OfflineReason.ProtocolError;
            }

            if (reason == OfflineReason.None)
                SetOnline();
            else
                SetOffline(reason);
        }

        public Task<bool> RefreshAfterUnauthorizedAsync()
        {
            lock (_gate)
            {
                if (State != ConnectionState.Online)
                    return Task.FromResult(false);

                // 갱신 요청은 동시에 하나만 보낸다(겹치면 서버의 재사용 탐지에 걸린다). 끝난 복구는 다시 쓰지 않는다.
                if (_recovering == null || _recovering.IsCompleted)
                    _recovering = RecoverAsync();

                return _recovering;
            }
        }

        public void GoOffline(OfflineReason reason) => SetOffline(reason);

        // 갱신 한 번, 갱신이 거부되면 로그인 한 번. 등록은 하지 않는다. 시간 예산 없이 요청마다 요청 타임아웃만 건다.
        private async Task<bool> RecoverAsync()
        {
            OfflineReason reason;
            try
            {
                reason = await SignInAsync(false, () => _requestTimeoutSeconds);
            }
            catch (Exception error)
            {
                _logError("[인증] 401 복구 중 예외: " + error);
                reason = OfflineReason.ProtocolError;
            }

            if (reason != OfflineReason.None)
            {
                SetOffline(reason);
                return false;
            }

            // 기다리는 사이 GoOffline이 불렸으면 false.
            return State == ConnectionState.Online;
        }

        // 계약 2.6의 흐름. 성공이면 None, 아니면 오프라인 까닭. timeout은 다음 요청의 타임아웃(초)이고 null이면 보내지 않는다.
        private async Task<OfflineReason> SignInAsync(bool allowRegister, Func<int?> timeout)
        {
            if (_credentials?.RefreshToken != null)
            {
                string refreshToken = _credentials.RefreshToken;
                AuthResult refreshed = await SendAsync(seconds => _api.RefreshAsync(refreshToken, seconds), timeout);

                if (refreshed.Kind == AuthResultKind.Success)
                    return Accept(refreshed);

                if (refreshed.Kind == AuthResultKind.Offline)
                    return refreshed.OfflineReason;

                if (refreshed.ErrorCode != ApiErrorCodes.InvalidRefreshToken)
                    return Unexpected("갱신", refreshed);

                // 만료·폐기·재사용 탐지(저장 전 종료 포함). 토큰을 지우고 아이디·시크릿 로그인으로 넘어간다.
                _credentials = _credentials.WithRefreshToken(null);
                TrySave(_credentials);
            }

            if (_credentials == null)
            {
                // 온라인이었다면 계정 정보가 있다. 복구 중에 없으면 버그다.
                if (!allowRegister)
                    return OfflineReason.ProtocolError;

                AuthResult registered = await SendAsync(seconds => _api.RegisterAsync(seconds), timeout);

                if (registered.Kind == AuthResultKind.Offline)
                    return registered.OfflineReason;

                if (registered.Kind == AuthResultKind.Rejected)
                    return Unexpected("등록", registered);

                var credentials = new GuestCredentials(registered.GuestId, registered.GuestSecret);

                // 로그인하기 전에 저장한다. 저장하지 못한 계정으로는 진행하지 않는다(다음 실행에서 다시 등록한다).
                if (!TrySave(credentials))
                    return OfflineReason.Unreachable;

                _credentials = credentials;
            }

            GuestCredentials current = _credentials;
            AuthResult login = await SendAsync(seconds => _api.LoginAsync(current, seconds), timeout);

            if (login.Kind == AuthResultKind.Success)
                return Accept(login);

            if (login.Kind == AuthResultKind.Offline)
                return login.OfflineReason;

            if (login.ErrorCode == ApiErrorCodes.InvalidCredentials)
            {
                // 새 계정을 자동으로 만들지 않는다(계약 2.7). 로컬 진행도가 빈 서버 진행도로 덮어써지는 것을 막는다.
                _logError("[인증] 저장된 계정이 거부됐다. 새 계정을 만들지 않고 오프라인으로 둔다. 개발 중이면 테스트 도구의 '계정·진행 초기화'를 쓴다.");
                return OfflineReason.CredentialsRejected;
            }

            return Unexpected("로그인", login);
        }

        private async Task<AuthResult> SendAsync(Func<int, Task<AuthResult>> send, Func<int?> timeout)
        {
            int? seconds = timeout();

            if (seconds == null)
            {
                _log("[인증] 로그인 시간 예산을 다 써서 요청을 보내지 않는다.");
                return AuthResult.Offline(OfflineReason.Unreachable);
            }

            return await send(seconds.Value);
        }

        // 요청 하나의 타임아웃: min(요청 타임아웃, 남은 예산), 정수 초, 최소 1. 예산이 남지 않았으면 null.
        private int? TimeoutWithin(DateTime deadline)
        {
            double remaining = (deadline - _now()).TotalSeconds;

            if (remaining <= 0)
                return null;

            return Math.Max(1, Math.Min(_requestTimeoutSeconds, (int)Math.Floor(remaining)));
        }

        // 새 refresh 토큰은 다른 일을 하기 전에 저장한다. 저장에 실패해도 이번 실행은 이어 간다
        // (다음 실행에서 옛 토큰이 재사용 탐지에 걸리고 아이디·시크릿 로그인으로 복구된다).
        private OfflineReason Accept(AuthResult tokens)
        {
            _credentials = _credentials.WithRefreshToken(tokens.RefreshToken);
            TrySave(_credentials);
            _accessToken = tokens.AccessToken;
            return OfflineReason.None;
        }

        private OfflineReason Unexpected(string request, AuthResult result)
        {
            _logError($"[인증] {request} 요청이 예상하지 못한 응답을 받았다: {result.StatusCode} {result.ErrorCode ?? "(오류 본문 없음)"}");
            return OfflineReason.ProtocolError;
        }

        private bool TrySave(GuestCredentials credentials)
        {
            try
            {
                _store.Save(credentials);
                return true;
            }
            catch (Exception error)
            {
                _logError("[인증] 계정 정보를 저장하지 못했다: " + error.Message);
                return false;
            }
        }

        private void SetOnline()
        {
            lock (_gate)
            {
                // 오프라인은 이번 실행 동안 그대로다.
                if (State != ConnectionState.Connecting)
                    return;

                State = ConnectionState.Online;
            }

            Raise(ConnectionState.Online);
        }

        private void SetOffline(OfflineReason reason)
        {
            lock (_gate)
            {
                // 처음 사유를 유지한다.
                if (State == ConnectionState.Offline)
                    return;

                State = ConnectionState.Offline;
                OfflineReason = reason;
                _accessToken = null;
            }

            Raise(ConnectionState.Offline);
        }

        private void Raise(ConnectionState state)
        {
            if (_mainThread == null || SynchronizationContext.Current == _mainThread)
                StateChanged?.Invoke(state);
            else
                _mainThread.Post(_ => StateChanged?.Invoke(state), null);
        }
    }
}
