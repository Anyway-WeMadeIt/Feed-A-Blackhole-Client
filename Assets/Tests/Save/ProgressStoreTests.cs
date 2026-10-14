using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackHole.Core;
using BlackHole.Net;
using BlackHole.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlackHole.Save.Tests
{
    // 진행 저장 포맷 2(진행도 + 로그 장부)와 ProgressStore: 같은 원자적 쓰기, 포맷 1 이전, 서버 응답 반영(덮어쓰기 경로).
    public sealed class ProgressStoreTests
    {
        private const string NodeA = "timer-01";
        private const string NodeB = "breaker.radius-01";
        private const string FormatOneFile =
            "{\"FormatVersion\":1,\"Gold\":100,\"GrowthStage\":1,\"Nodes\":[{\"NodeId\":\"timer-01\",\"Rank\":1}],\"SavedAtUtc\":\"2026-10-12T00:00:00Z\"}";

        private static readonly DateTime Now = new(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc);

        private static NodeContent _nodes;
        private static HqGrowthDefinition _growth;

        private string _directory;

        private string FilePath => Path.Combine(_directory, "progress.json");

        [OneTimeSetUp]
        public void LoadContent()
        {
            string folder = Path.Combine(Application.dataPath, "Data", "NodeTable");
            string Read(string name) => File.ReadAllText(Path.Combine(folder, name + ".csv"), Encoding.UTF8);

            NodeContentLoadResult result = NodeContentCsv.Load(
                Read(NodeContentCsv.StatsTab), Read(NodeContentCsv.NodesTab), Read(NodeContentCsv.CostTab), Read(NodeContentCsv.EffectsTab));
            Assert.IsNotNull(result.Content, string.Join("\n", result.Diagnostics));
            _nodes = result.Content;

            _growth = new HqGrowthDefinition(
                new long[] { 10, 25, 45 },
                new[] { new HqMilestone(2, 100, 1f), new HqMilestone(3, 200, 1.2f) });
        }

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "blackhole-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void DeleteDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private ProgressStore Load(out ProgressState progress)
        {
            progress = new ProgressState();
            return ProgressStore.Load(_directory, _nodes, _growth, progress, () => Now);
        }

        private static PendingLog Log(long seq) => new(seq, LogTypes.NodePurchase, "{\"seq\":" + seq + "}");

        private ProgressSaveData ReadFile() => JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(FilePath, Encoding.UTF8));

        private void WriteFormatOne() => File.WriteAllText(FilePath, FormatOneFile, new UTF8Encoding(false));

        private static SyncResponseDto Response(long through, long gold, int stage, params (string NodeId, int Rank)[] ranks)
        {
            var response = new SyncResponseDto
            {
                appliedThroughSeq = through,
                progress = new ProgressDto { lastAppliedSeq = through, gold = gold, growthStage = stage },
            };

            foreach ((string nodeId, int rank) in ranks)
                response.progress.nodes.Add(new ProgressNodeDto { nodeId = nodeId, rank = rank });

            return response;
        }

        private static SyncResponseDto Rejection(long through, long gold, int stage, params (string NodeId, int Rank)[] ranks)
        {
            SyncResponseDto response = Response(through, gold, stage, ranks);
            response.rejected = new RejectedLogDto { seq = through + 1, type = LogTypes.Settlement, code = RejectCodes.SettlementGoldMismatch, message = "m" };
            return response;
        }

        // ---- 저장 ----

        [Test]
        public void StartNewSavesTheResetLogWithTheProgress()
        {
            ProgressStore store = Load(out ProgressState progress);

            store.StartNew(progress, seq => new PendingLog(seq, LogTypes.Reset, "{\"seq\":" + seq + "}"));

            ProgressSaveData file = ReadFile();
            Assert.AreEqual(ProgressSaveData.CurrentFormatVersion, file.FormatVersion);
            Assert.AreEqual(0, file.Gold);
            Assert.AreEqual(2, file.Sync.NextSeq);
            Assert.AreEqual(1, file.Sync.Pending.Count);
            Assert.AreEqual(LogTypes.Reset, file.Sync.Pending[0].Type);
        }

        [Test]
        public void CommitSavesTheProgressAndTheLogInOneFile()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));

            progress.EarnGold(30);
            store.Commit(seq => Log(seq));

            ProgressStore reloaded = Load(out _);
            Assert.AreEqual(30, reloaded.Saved.Gold);
            Assert.AreEqual(2, reloaded.Pending.Count);
            Assert.AreEqual(2, reloaded.Pending[1].Seq);
            Assert.AreEqual("{\"seq\":2}", reloaded.Pending[1].Json);
        }

        [Test]
        public void LogAppendedIsRaisedAfterTheSave()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));
            int saved = -1;
            store.LogAppended += () => saved = ReadFile().Sync.Pending.Count;

            store.Commit(seq => Log(seq));

            Assert.AreEqual(2, saved);
        }

        [Test]
        public void CommitBeforeChoosingDoesNothing()
        {
            ProgressStore store = Load(out _);
            bool called = false;

            store.Commit(seq => { called = true; return Log(seq); });

            Assert.IsFalse(called);
            Assert.AreEqual(0, store.Pending.Count);
            Assert.IsFalse(File.Exists(FilePath));
        }

        [Test]
        public void TestSessionMakesNoLogsAndSavesNothing()
        {
            ProgressStore store = Load(out _);
            store.EnterTestSession();
            bool called = false;

            store.Commit(seq => { called = true; return Log(seq); });

            Assert.IsFalse(called);
            Assert.IsFalse(File.Exists(FilePath));
        }

        [Test]
        public void FormatOneFileLoadsWithAnInitialLedgerAndBecomesFormatTwo()
        {
            WriteFormatOne();
            ProgressStore store = Load(out ProgressState progress);

            Assert.IsNotNull(store.Saved);
            Assert.AreEqual(100, store.Saved.Gold);
            Assert.AreEqual(0, store.Pending.Count);

            store.Continue(progress);
            store.Commit(seq => Log(seq));

            ProgressSaveData file = ReadFile();
            Assert.AreEqual(2, file.FormatVersion);
            Assert.AreEqual(100, file.Gold);
            Assert.AreEqual(1, file.Nodes.Count);
            Assert.AreEqual(1, file.Sync.Pending.Count);
            Assert.AreEqual(1, file.Sync.Pending[0].Seq);
            Assert.AreEqual(2, file.Sync.NextSeq);
        }

        [Test]
        public void FailedWriteKeepsTheOldFileAndTheLogForTheNextSave()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));
            Directory.CreateDirectory(FilePath + ".tmp");   // 임시 파일을 쓸 수 없게 막는다(쓰기 도중 실패).

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[저장\] 저장하지 못했다"));
            progress.EarnGold(5);
            store.Commit(seq => Log(seq));

            Assert.AreEqual(2, store.Pending.Count, "메모리의 장부는 유지한다.");
            ProgressSaveData file = ReadFile();
            Assert.AreEqual(0, file.Gold, "파일은 이전 저장 그대로다.");
            Assert.AreEqual(1, file.Sync.Pending.Count);

            Directory.Delete(FilePath + ".tmp");
            store.Save(progress);

            file = ReadFile();
            Assert.AreEqual(5, file.Gold);
            Assert.AreEqual(2, file.Sync.Pending.Count, "진행도와 로그가 함께 저장된다.");
        }

        [Test]
        public void BrokenSyncStateFallsBackToThePreviousSave()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));
            store.Commit(seq => Log(seq));   // 본 파일(로그 2개), 직전 저장(로그 1개)

            ProgressSaveData broken = ReadFile();
            broken.Sync.NextSeq = 99;   // 마지막 대기 로그 다음 seq가 아니다.
            File.WriteAllText(FilePath, JsonUtility.ToJson(broken), new UTF8Encoding(false));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[저장\] 본 저장이 깨졌다"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"읽지 못한 저장 파일을 보관했다"));
            ProgressStore reloaded = Load(out _);

            Assert.AreEqual(1, reloaded.Pending.Count, "직전 저장의 진행도와 장부가 함께 돌아온다.");
        }

        // ---- 서버 응답 ----

        [Test]
        public void AppliedResponseClearsTheLogsAndWritesTheFile()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));
            store.Commit(seq => Log(seq));

            SyncApplyOutcome outcome = store.ApplyResponse(Response(2, 0, 0));

            Assert.AreEqual(SyncApplyOutcome.Applied, outcome);
            Assert.AreEqual(0, store.Pending.Count);
            ProgressSaveData file = ReadFile();
            Assert.AreEqual(0, file.Sync.Pending.Count);
            Assert.AreEqual(3, file.Sync.NextSeq);
        }

        [Test]
        public void ResponseWithRemainingLogsDoesNotOverwriteTheProgress()
        {
            ProgressStore store = Load(out ProgressState progress);
            store.StartNew(progress, seq => Log(seq));
            progress.EarnGold(40);
            store.Commit(seq => Log(seq));   // 로그 2: 응답을 기다리는 동안 쌓인 로그

            SyncApplyOutcome outcome = store.ApplyResponse(Response(1, 0, 0));

            Assert.AreEqual(SyncApplyOutcome.Applied, outcome);
            Assert.AreEqual(40, progress.Gold, "서버 진행도(0)로 덮어쓰지 않는다.");
            Assert.AreEqual(1, store.Pending.Count);
            Assert.AreEqual(40, ReadFile().Gold);
        }

        [Test]
        public void RejectionOverwritesTheProgressWithoutKeepingTheLargerValues()
        {
            WriteFormatOne();   // Gold 100, 성장도 1, timer-01
            ProgressStore store = Load(out ProgressState progress);
            store.Continue(progress);
            store.Commit(seq => Log(seq));
            store.Commit(seq => Log(seq));

            SyncApplyOutcome outcome = store.ApplyResponse(Rejection(0, 10, 0, (NodeB, 1)));

            Assert.AreEqual(SyncApplyOutcome.Rejected, outcome);
            Assert.AreEqual(10, progress.Gold);
            Assert.AreEqual(0, progress.GrowthStage);
            Assert.AreEqual(0, progress.RankOf(NodeA));
            Assert.AreEqual(1, progress.RankOf(NodeB));

            // 저장의 "큰 값 유지"를 거치지 않는다: 파일에도 서버 값이 그대로 남는다.
            ProgressSaveData file = ReadFile();
            Assert.AreEqual(10, file.Gold);
            Assert.AreEqual(0, file.GrowthStage);
            Assert.AreEqual(1, file.Nodes.Count);
            Assert.AreEqual(NodeB, file.Nodes[0].NodeId);
            Assert.AreEqual(0, file.Sync.Pending.Count);
            Assert.AreEqual(1, file.Sync.NextSeq);
            Assert.AreEqual(RejectCodes.SettlementGoldMismatch, file.Sync.LastRejection.Code);
            Assert.IsFalse(store.LastRejectionShown);

            // 이후 저장에서도 옛 값이 되살아나지 않는다.
            progress.EarnGold(1);
            store.Save(progress);
            Assert.AreEqual(11, ReadFile().Gold);
            Assert.AreEqual(0, ReadFile().GrowthStage);
        }

        [Test]
        public void MarkingTheRejectionShownIsSaved()
        {
            WriteFormatOne();
            ProgressStore store = Load(out ProgressState progress);
            store.Continue(progress);
            store.Commit(seq => Log(seq));
            store.ApplyResponse(Rejection(0, 0, 0));

            store.MarkLastRejectionShown();

            Assert.IsTrue(ReadFile().Sync.LastRejection.Shown);
            ProgressStore reloaded = Load(out _);
            Assert.IsNotNull(reloaded.LastRejection);
            Assert.IsTrue(reloaded.LastRejectionShown);
        }

        [Test]
        public void ResponseAtTheTitleIsWrittenAndBecomesTheContinueSave()
        {
            WriteFormatOne();   // Gold 100
            ProgressStore store = Load(out ProgressState progress);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[동기화\] 거부가 없는데"));
            SyncApplyOutcome outcome = store.ApplyResponse(Response(0, 40, 0));

            Assert.AreEqual(SyncApplyOutcome.AppliedWithDrift, outcome);
            Assert.AreEqual(40, store.Saved.Gold);
            Assert.AreEqual(40, ReadFile().Gold, "고르기 전에도 응답은 파일에 쓴다.");

            store.Continue(progress);
            Assert.AreEqual(40, progress.Gold);
        }

        [Test]
        public void TitleResponseWithoutOverwriteKeepsTheSavedProgressAndStoresTheLedger()
        {
            WriteFormatOne();
            ProgressStore store = Load(out _);

            store.ApplyResponse(Response(0, 100, 1, (NodeA, 1)));   // 같은 진행도

            ProgressSaveData file = ReadFile();
            Assert.AreEqual(100, file.Gold);
            Assert.AreEqual(2, file.FormatVersion);
            Assert.AreEqual(1, file.Sync.NextSeq);
        }

        [Test]
        public void EmptyServerWithoutALocalSaveCreatesNoSave()
        {
            ProgressStore store = Load(out _);

            store.ApplyResponse(Response(0, 0, 0));
            store.ApplyResponse(Rejection(0, 0, 0));

            Assert.IsNull(store.Saved);
            Assert.IsFalse(store.CanContinue);
            Assert.IsFalse(File.Exists(FilePath));
        }

        [Test]
        public void ServerWithHistoryWithoutALocalSaveCreatesOne()
        {
            ProgressStore store = Load(out ProgressState progress);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[동기화\] 거부가 없는데"));
            store.ApplyResponse(Response(7, 5, 0));

            Assert.IsNotNull(store.Saved);
            Assert.IsTrue(store.CanContinue);
            Assert.AreEqual(5, ReadFile().Gold);
            Assert.AreEqual(8, ReadFile().Sync.NextSeq, "다음 seq는 서버가 반영한 마지막 seq + 1이다.");

            store.Continue(progress);
            Assert.AreEqual(5, progress.Gold);
        }

        [Test]
        public void ServerProgressThatCannotBeUsedKeepsTheLocalProgress()
        {
            WriteFormatOne();
            ProgressStore store = Load(out ProgressState progress);
            store.Continue(progress);

            // 성장도가 음수인 진행도는 저장 검사를 통과하지 못한다(서버가 그런 값을 줄 일은 없다). 덮어쓰지 않고 오류를 남긴다.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[동기화\] 거부가 없는데"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"서버 진행도를 로컬에 쓰지 못했다"));
            store.ApplyResponse(Response(0, 9, -1));

            Assert.AreEqual(100, progress.Gold);
            Assert.AreEqual(1, progress.GrowthStage);
        }

        // ---- 형식 검사 ----

        [Test]
        public void SyncStateOutOfOrderIsABrokenSave()
        {
            var data = new ProgressSaveData { FormatVersion = ProgressSaveData.CurrentFormatVersion };
            data.Sync.NextSeq = 5;
            data.Sync.Pending.Add(new PendingLogSaveData { Seq = 1, Type = LogTypes.Reset, Json = "{}" });
            data.Sync.Pending.Add(new PendingLogSaveData { Seq = 3, Type = LogTypes.Reset, Json = "{}" });

            Assert.IsFalse(ProgressSave.Load(data, _nodes, _growth).Succeeded);

            data.Sync.Pending[1].Seq = 2;
            data.Sync.NextSeq = 3;
            Assert.IsTrue(ProgressSave.Load(data, _nodes, _growth).Succeeded);

            data.Sync.Pending[1].Json = "";
            Assert.IsFalse(ProgressSave.Load(data, _nodes, _growth).Succeeded);
        }

        [Test]
        public void UnknownFormatVersionsAreRejected()
        {
            Assert.IsFalse(ProgressSave.Load(new ProgressSaveData { FormatVersion = 0 }, _nodes, _growth).Succeeded);
            Assert.IsFalse(ProgressSave.Load(new ProgressSaveData { FormatVersion = 3 }, _nodes, _growth).Succeeded);
            Assert.IsTrue(ProgressSave.Load(new ProgressSaveData { FormatVersion = 1 }, _nodes, _growth).Succeeded);
        }
    }
}
