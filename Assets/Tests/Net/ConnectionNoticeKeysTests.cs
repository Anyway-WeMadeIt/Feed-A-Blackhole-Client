using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 타이틀의 서버 연결 결과 알림(계약 2.8)과 콘텐츠 버전 불일치 전용 문구의 키 선택.
    public sealed class ConnectionNoticeKeysTests
    {
        [Test]
        public void Online_HasTheConnectedNotice()
        {
            Assert.AreEqual(ConnectionNoticeKeys.Online, ConnectionNoticeKeys.TextKeyOf(ConnectionState.Online, OfflineReason.None));
        }

        [TestCase(OfflineReason.Unreachable, ConnectionNoticeKeys.Unreachable)]
        [TestCase(OfflineReason.CredentialsRejected, ConnectionNoticeKeys.AccountUnverified)]
        [TestCase(OfflineReason.ProtocolError, ConnectionNoticeKeys.AccountUnverified)]
        public void OfflineReasons_HaveTheirOwnNotice(OfflineReason reason, string key)
        {
            Assert.AreEqual(key, ConnectionNoticeKeys.TextKeyOf(ConnectionState.Offline, reason));
        }

        [Test]
        public void DisabledAndConnectingShowNothing()
        {
            Assert.IsNull(ConnectionNoticeKeys.TextKeyOf(ConnectionState.Offline, OfflineReason.Disabled));
            Assert.IsNull(ConnectionNoticeKeys.TextKeyOf(ConnectionState.Connecting, OfflineReason.None));
            Assert.IsNull(ConnectionNoticeKeys.TextKeyOf(ConnectionState.Offline, OfflineReason.ContentOutdated), "콘텐츠 버전 불일치는 거부 알림이 알린다.");
        }

        [Test]
        public void OnlyOfflineNoticesAreProblems()
        {
            Assert.IsFalse(ConnectionNoticeKeys.IsProblem(ConnectionState.Online, OfflineReason.None));
            Assert.IsFalse(ConnectionNoticeKeys.IsProblem(ConnectionState.Offline, OfflineReason.Disabled));
            Assert.IsTrue(ConnectionNoticeKeys.IsProblem(ConnectionState.Offline, OfflineReason.Unreachable));
        }

        [Test]
        public void ContentVersionMismatchIsTheOnlyOutdatedCode()
        {
            Assert.IsTrue(RejectionKinds.IsContentOutdated(RejectCodes.ContentVersionMismatch));
            Assert.IsFalse(RejectionKinds.IsContentOutdated(RejectCodes.NotEnoughGold));
            Assert.IsFalse(RejectionKinds.IsContentOutdated(null));
        }
    }
}
