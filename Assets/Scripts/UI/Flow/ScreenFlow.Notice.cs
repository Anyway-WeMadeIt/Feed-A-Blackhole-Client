using BlackHole.Net;
using UnityEngine;

namespace BlackHole.Unity
{
    // 타이틀·업그레이드 화면의 알림 글자(FloatingNotice). 알림은 세로 목록으로 한꺼번에 보이고, 알림마다 제 수명을 가진다.
    //
    // 1. 서버 연결 결과(계약 2.8): 알릴 연결 상태가 바뀌었을 때 한 번(ConnectionNoticeTracker). 처음 확인할 때는 로그인과 시작 동기화가 끝난 뒤의 상태를 알린다.
    //    - 온라인: "서버에 연결되었습니다"(짧게). 같이 알릴 거부가 있으면 건너뛴다 — 연결은 됐지만 진행도가 서버 기준으로 바뀐 것이 알릴 일이다.
    //    - 오프라인: 까닭에 따라 문구가 다르다. 서버를 쓰지 않는 실행(Disabled)은 알리지 않는다.
    //    - 플레이 중 연결이 끊기면(IAuthSession.StateChanged) 타이틀·업그레이드 화면에 있을 때는 바로, 전투 등 다른 화면이면 다음에 두 화면에 들어갈 때 알린다.
    // 2. 서버 거부(계약 5.9): 마지막 동기화에 거부가 있고 아직 보여 주지 않았으면 한 번. 연결 결과 아래에 보인다.
    //    - 콘텐츠 버전 불일치(CONTENT_VERSION_MISMATCH)는 전용 문구다. 이 경우 모든 동기화가 거부되어 진행도가 서버 값으로 덮어써지므로
    //      업데이트하라고 알린다.
    //
    // 알림은 화면에 들어갈 때(ShowTitle, ShowUpgrade)와, 화면에 있는 동안 거부를 받거나(GameBootstrap이 SyncSender.RejectionReceived를 연결)
    // 연결 상태가 바뀌었을 때 정한다.
    // 업그레이드 화면에서 거부를 받으면 서버 값으로 덮어쓴 진행도를 노드 트리와 Gold에 다시 그린다. 전투·결산 중이면 다음에 두 화면에 들어갈 때 보인다.
    // 거부는 글자를 줄 세울 때 "보여 줬다"로 기록한다(눌러서 닫는 창이 아니므로).
    internal sealed partial class ScreenFlow
    {
        private const float ConnectedHoldSeconds = 2.5f;
        private const float WarningHoldSeconds = 5f;
        private const float ContentOutdatedHoldSeconds = 9f;

        private FloatingNotice _notice;
        private readonly ConnectionNoticeTracker _connectionNotices = new();

        // GameBootstrap이 SyncSender.RejectionReceived에 연결한다. 메인 스레드에서 불린다.
        public void HandleSyncRejection()
        {
            if (_ui.CurrentRoot is UpgradeScreen)
                RefreshUpgrade();

            ShowNoticesIfDue();
        }

        // IAuthSession.StateChanged. 플레이 중 연결이 끊긴 경우도 여기로 온다(메인 스레드). 알릴 화면이 아니면 다음 진입 때 알린다.
        private void HandleConnectionStateChanged(ConnectionState state) => ShowNoticesIfDue();

        // 타이틀·업그레이드 화면이 열려 있으면, 새로 알릴 연결 상태와 거부를 함께 보인다.
        private void ShowNoticesIfDue()
        {
            if (!(_ui.CurrentRoot is TitleScreen) && !(_ui.CurrentRoot is UpgradeScreen))
                return;

            RejectedLogDto rejection = _progressStore.LastRejection;
            bool rejectionDue = rejection != null && !_progressStore.LastRejectionShown;

            ShowConnectionNotice(rejectionDue);

            if (rejectionDue)
                ShowRejectionNotice(rejection);
        }

        // 연결 상태가 이미 알린 것과 달라졌을 때만 알린다. 알렸든 건너뛰든 이 상태는 알린 것으로 친다.
        private void ShowConnectionNotice(bool rejectionFollows)
        {
            if (_auth == null)
                return;

            string key = _connectionNotices.Next(_auth.State, _auth.OfflineReason);

            if (key == null)
                return;

            bool online = _auth.State == ConnectionState.Online;

            if (online && rejectionFollows)
                return;

            EnsureNotice().Show(_localizer.Get(key), online ? ConnectedHoldSeconds : WarningHoldSeconds);
        }

        private void ShowRejectionNotice(RejectedLogDto rejection)
        {
            bool outdated = RejectionKinds.IsContentOutdated(rejection.code);

            string text = outdated
                ? _localizer.Get(RejectionKinds.ContentOutdatedKey)
                : _localizer.Format("sync.rejection", _localizer.Get(RejectionKinds.TextKeyOf(rejection.type)), rejection.code);

            EnsureNotice().Show(text, outdated ? ContentOutdatedHoldSeconds : WarningHoldSeconds);
            _progressStore.MarkLastRejectionShown();
        }

        private FloatingNotice EnsureNotice()
        {
            if (_notice != null)
                return _notice;

            var style = new UIStyle(_settings.TextScale, _fonts, _settings.LanguageLocale.ToId(), null, null, null);
            Transform canvas = _transition.transform.parent;

            // 엔딩 화면과 같이 화면 전환 덮개 바로 아래에 둔다.
            _notice = FloatingNotice.Create(
                canvas,
                _transition.transform.GetSiblingIndex(),
                style.FontFor(UITextRole.Body),
                style.Scale);

            return _notice;
        }

        private void DisposeNotice()
        {
            if (_notice != null)
                Object.Destroy(_notice.gameObject);

            _notice = null;
        }
    }
}
