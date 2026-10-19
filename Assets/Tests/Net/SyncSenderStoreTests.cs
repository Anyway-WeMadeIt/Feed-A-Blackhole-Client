using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BlackHole.Core;
using BlackHole.Unity;
using NUnit.Framework;
using UnityEngine;

namespace BlackHole.Net.Tests
{
    // 전송기와 실제 진행 저장(ProgressStore, W3-3)을 함께 본다. 서버는 가짜 ISyncApi다.
    // 저장 파일이 실행 사이를 잇는 부분(로그를 저장한 뒤에 보내는가, 타이틀에서 받은 응답이 다음 실행에 남는가)을 확인한다.
    public sealed class SyncSenderStoreTests
    {
        private static NodeContent _nodes;
        private static HqGrowthDefinition _growth;

        private string _directory;
        private FakeSyncApi _api;
        private List<string> _errors;
        private SynchronizationContext _savedContext;

        [OneTimeSetUp]
        public void LoadContent()
        {
            string folder = Path.Combine(Application.dataPath, "Data", "NodeTable");
            string Read(string name) => File.ReadAllText(Path.Combine(folder, name + ".csv"), Encoding.UTF8);

            NodeContentLoadResult content = NodeContentCsv.Load(
                Read(NodeContentCsv.StatsTab), Read(NodeContentCsv.NodesTab),
                Read(NodeContentCsv.CostTab), Read(NodeContentCsv.EffectsTab));
            Assert.IsNotNull(content.Content, string.Join("\n", content.Diagnostics));
            _nodes = content.Content;

            _growth = new HqGrowthDefinition(
                new long[] { 10, 25, 45 },
                new[] { new HqMilestone(2, 100, 1f), new HqMilestone(3, 200, 1.2f) });
        }

        [SetUp]
        public void SetUp()
        {
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _directory = Path.Combine(Path.GetTempPath(), "blackhole-sender-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _api = new FakeSyncApi();
            _errors = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_savedContext);

            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);

            Assert.AreEqual(0, _api.UnexpectedCalls, "넣어 둔 결과보다 요청이 많았다.");
            Assert.IsEmpty(_errors);
        }

        [Test]
        public void OnlineLogIsSavedBeforeItIsSent()
        {
            ProgressStore store = Load(out ProgressState progress);
            _api.Enqueue(SyncCallResult.Of(Response(0, 0)));
            SyncSender sender = Sender(store);
            TestTasks.Done(sender.SyncOnStartupAsync());

            // 보내는 순간 로그가 이미 파일에 있어야, 응답 전에 꺼져도 다음 실행이 다시 보낸다.
            long[] savedWhenSent = null;
            _api.Sent = batch => savedWhenSent = Load(out _).Pending.Select(log => log.Seq).ToArray();
            _api.Enqueue(SyncCallResult.Of(Response(1, 0)));

            store.StartNew(progress, ResetLog);

            CollectionAssert.AreEqual(new long[] { 1 }, savedWhenSent);
            Assert.IsEmpty(store.Pending);
            Assert.IsEmpty(Load(out _).Pending, "반영 결과도 파일에 쓴다.");
        }

        [Test]
        public void StartupAtTheTitleSendsTheSavedLogsAndTheNextRunHasNone()
        {
            SaveTwoLogsOffline();

            ProgressStore store = Load(out ProgressState progress);
            Assert.AreEqual(2, store.Pending.Count, "준비가 잘못되었다.");
            _api.Enqueue(SyncCallResult.Of(Response(2, 30)));

            TestTasks.Done(Sender(store).SyncOnStartupAsync());

            CollectionAssert.AreEqual(new long[] { 1, 2 }, _api.Batches.Single().Select(log => log.Seq));
            Assert.IsEmpty(store.Pending);

            ProgressStore next = Load(out ProgressState nextProgress);
            Assert.IsEmpty(next.Pending, "다음 실행의 장부에 남지 않는다.");
            next.Continue(nextProgress);
            Assert.AreEqual(30, nextProgress.Gold);

            store.Continue(progress);
            Assert.AreEqual(30, progress.Gold);
        }

        [Test]
        public void RejectionAtTheTitleBecomesTheContinueSaveAndIsRemembered()
        {
            SaveTwoLogsOffline();

            ProgressStore store = Load(out ProgressState progress);
            SyncResponseDto rejected = Response(1, 0);
            rejected.rejected = new RejectedLogDto { seq = 2, type = LogTypes.Settlement, code = RejectCodes.SettlementGoldMismatch, message = "m" };
            _api.Enqueue(SyncCallResult.Of(rejected));

            TestTasks.Done(Sender(store).SyncOnStartupAsync());

            ProgressStore next = Load(out ProgressState nextProgress);
            Assert.IsEmpty(next.Pending, "거부되면 남은 로그를 버린다.");
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, next.LastRejection?.code, "거부 기록이 다음 실행에도 남아 알림(W3-5)이 읽는다.");
            Assert.IsFalse(next.LastRejectionShown);

            next.Continue(nextProgress);
            Assert.AreEqual(0, nextProgress.Gold, "계속하면 서버 진행도로 이어진다.");

            store.Continue(progress);
            Assert.AreEqual(0, progress.Gold);
        }

        // ---------- 도우미 ----------

        private ProgressStore Load(out ProgressState progress)
        {
            progress = new ProgressState();
            return ProgressStore.Load(_directory, _nodes, _growth, progress);
        }

        private SyncSender Sender(ProgressStore store) =>
            new(new FakeAuthSession(), _api, store, 10, null, _errors.Add);

        // 오프라인 실행: 새 게임(RESET, seq 1) → Gold 30을 번 뒤 로그 하나(seq 2). 전송기 없이 저장만 한다.
        private void SaveTwoLogsOffline()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, ResetLog);
            progress.EarnGold(30);
            store.Commit(seq => new PendingLog(seq, LogTypes.Settlement, "{\"seq\":" + seq + "}"));
        }

        private static PendingLog ResetLog(long seq) => new(seq, LogTypes.Reset, "{\"seq\":" + seq + "}");

        // 서버 진행도(Gold만 다르고 성장도는 시작 단계, 노드 없음).
        private static SyncResponseDto Response(long through, long gold) =>
            new()
            {
                appliedThroughSeq = through,
                progress = new ProgressDto { lastAppliedSeq = through, gold = gold, growthStage = HqGrowthDefinition.StartStage },
            };
    }
}
