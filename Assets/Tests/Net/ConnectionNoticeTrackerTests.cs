using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 연결 상태 알림을 한 번만, 상태가 바뀌었을 때만 알리는 판단(계약 2.8). 플레이 중 연결 끊김도 여기서 가려진다.
    public sealed class ConnectionNoticeTrackerTests
    {
        [Test]
        public void FirstAskReturnsTheCurrentNotice()
        {
            var tracker = new ConnectionNoticeTracker();

            Assert.AreEqual(ConnectionNoticeKeys.Online, tracker.Next(ConnectionState.Online, OfflineReason.None));
        }

        [Test]
        public void AskingAgainWithTheSameStateReturnsNothing()
        {
            var tracker = new ConnectionNoticeTracker();
            tracker.Next(ConnectionState.Online, OfflineReason.None);

            Assert.IsNull(tracker.Next(ConnectionState.Online, OfflineReason.None), "타이틀로 돌아오거나 업그레이드에 들어와도 다시 알리지 않는다.");
            Assert.IsNull(tracker.Next(ConnectionState.Online, OfflineReason.None));
        }

        [Test]
        public void LosingTheConnectionDuringPlayIsNoticedOnce()
        {
            var tracker = new ConnectionNoticeTracker();
            tracker.Next(ConnectionState.Online, OfflineReason.None);

            Assert.AreEqual(ConnectionNoticeKeys.Unreachable, tracker.Next(ConnectionState.Offline, OfflineReason.Unreachable));
            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.Unreachable));
        }

        [Test]
        public void AnOfflineStartIsNoticedOnTheFirstAsk()
        {
            var tracker = new ConnectionNoticeTracker();

            Assert.AreEqual(ConnectionNoticeKeys.AccountUnverified, tracker.Next(ConnectionState.Offline, OfflineReason.ProtocolError));
            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.ProtocolError));
        }

        [Test]
        public void StatesWithoutANoticeStayQuiet()
        {
            var tracker = new ConnectionNoticeTracker();

            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.Disabled));
            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.Disabled));
        }

        [Test]
        public void ContentOutdatedAfterOnlineAddsNoConnectionNotice()
        {
            var tracker = new ConnectionNoticeTracker();
            tracker.Next(ConnectionState.Online, OfflineReason.None);

            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.ContentOutdated), "콘텐츠 버전 불일치는 거부 알림이 알린다.");
            Assert.IsNull(tracker.Next(ConnectionState.Offline, OfflineReason.ContentOutdated));
        }

        [Test]
        public void ConnectingIsRememberedAsQuietSoTheLaterOnlineStateIsNoticed()
        {
            var tracker = new ConnectionNoticeTracker();

            Assert.IsNull(tracker.Next(ConnectionState.Connecting, OfflineReason.None));
            Assert.AreEqual(ConnectionNoticeKeys.Online, tracker.Next(ConnectionState.Online, OfflineReason.None));
        }
    }
}
