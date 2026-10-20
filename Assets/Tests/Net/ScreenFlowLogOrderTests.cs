using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BlackHole.Core;
using BlackHole.Sync;
using NUnit.Framework;
using UnityEngine;

namespace BlackHole.Net.Tests
{
    // W1-6 "남은 일 ②": 결산 로그가 구매 로그보다 먼저 만들어지는지(ScreenFlow의 결산 → 업그레이드 흐름).
    // 구매가 먼저 오면 서버는 결산 보상이 반영되기 전의 Gold로 구매를 검사한다 → 구매가 거부되고,
    // 거부 반영(계약 5.7)이 남은 로그(그 판의 결산)를 모두 지우므로 그 판의 보상이 알림 없이 사라진다.
    //
    // ScreenFlow는 UIManager·BattleSystem 등 Unity 객체가 있어야 만들 수 있어 Editor 테스트에서 직접 띄우지 않는다.
    // 대신 ScreenFlow가 로그를 남기는 세 지점을 같은 순서·같은 인자로 옮긴 PlayerFlow로 장부(SyncLedger)를 채운다.
    // ProgressStore.Commit은 장부에 Append한 뒤 저장할 뿐이라 순서는 장부에서 그대로 보인다.
    //   StartBattle            → ScreenFlow.Battle.StartBattle        (RUN_START)
    //   EndBattle              → ScreenFlow.Battle.EndBattleAsync     (CommitSettlement → ShowSettlement)
    //   Buy                    → ScreenFlow.NodeTreeView.HandleNodeTreeNodeClicked (NODE_PURCHASE)
    // ScreenFlow에서 이 순서가 바뀌면 PlayerFlow도 같이 고쳐야 한다(아래 주석의 근거 줄 참고).
    [TestFixture]
    public class ScreenFlowLogOrderTests
    {
        private const string NodeA = "timer-01";
        private const string NodeB = "breaker.radius-01";

        private static readonly DateTime Now = new(2026, 10, 15, 3, 0, 0, DateTimeKind.Utc);
        private static readonly EnemyDefinition Asteroid =
            new(EnemyType.Asteroid, 1f, 1f, 0f, new[] { new EnemyTier(1f, 1, 1) });


        // ---------- ScreenFlow의 로그 지점을 옮긴 흐름 ----------

        private sealed class PlayerFlow
        {
            private readonly SyncLogFactory _logs = new(new TestJson(), "0.4.0", "4d6c398ad1f5e49c", () => Now);
            private readonly Dictionary<string, int> _ranks = new(StringComparer.Ordinal);
            private string _runId;

            public SyncLedger Ledger { get; } = new();
            public long Gold { get; private set; }

            // 새 게임: ScreenFlow.ModeSelect → ProgressStore.StartNew(…Reset)
            public void StartNew() => Ledger.Append(seq => _logs.Reset(seq));

            // ScreenFlow.Battle.StartBattle: 시작 Gold는 판을 시작할 때의 진행도 값.
            public string StartBattle()
            {
                string runId = Guid.NewGuid().ToString();
                _runId = runId;
                long startGold = Gold;
                var start = new RunStartInfo(123456, 0, 0, 12f, new[] { new SupplyRequest(Asteroid, 20) });
                Ledger.Append(seq => _logs.RunStart(seq, runId, start, startGold));
                return runId;
            }

            // ScreenFlow.Battle.EndBattleAsync: TryEndAsync가 진행도에 보상을 더한 뒤
            // CommitSettlement(결산 로그) → ShowSettlement(업그레이드 버튼이 생기는 화면) 순서.
            public void EndBattle(long settledGold)
            {
                Gold += settledGold; // BattleSystem.TryEndAsync의 결산(진행도 반영)

                string runId = _runId;
                _runId = null;
                Assert.That(runId, Is.Not.Null, "판 시작 없이 끝낸 판은 테스트 세션이라 로그를 남기지 않는다.");
                Ledger.Append(seq => _logs.Settlement(seq, runId, Raw(settledGold)));

                SettlementShown = true; // 이 뒤에야 결산 화면의 업그레이드 버튼을 누를 수 있다.
            }

            public bool SettlementShown { get; private set; }

            // ScreenFlow.NodeTreeView.HandleNodeTreeNodeClicked: 비용은 사기 전에 읽고, rank는 산 뒤의 값.
            public bool Buy(string nodeId, long cost)
            {
                if (Gold < cost)
                    return false;

                Gold -= cost;
                int rank = (_ranks.TryGetValue(nodeId, out int r) ? r : 0) + 1;
                _ranks[nodeId] = rank;
                Ledger.Append(seq => _logs.NodePurchase(seq, nodeId, rank, cost));
                return true;
            }

            private static BattleRawData Raw(long settled) =>
                new(123456, 12f, Array.Empty<EnemyKillCount>(),
                    new[] { new KillBucketCount(new KillBucket(Asteroid, 0, 1, false), (int)Math.Max(1, settled)) },
                    0, 0, 0, settled, 0, settled, 0, 0, null, settled, null);
        }


        // ---------- 검사 도구 ----------

        private static string[] TypesOf(IEnumerable<PendingLog> logs) => logs.Select(l => l.Type).ToArray();

        private static T PayloadOf<T>(PendingLog log) where T : class
        {
            // payload는 봉투 안의 객체라 글자를 잘라 내 따로 읽는다(SyncLogFactoryTests와 같은 방법).
            const string key = "\"payload\":";
            int start = log.Json.IndexOf(key, StringComparison.Ordinal) + key.Length;
            return new TestJson().FromJson<T>(log.Json.Substring(start, log.Json.Length - start - 1));
        }

        // 판이 열려 있는 동안(RUN_START 뒤, 그 판의 SETTLEMENT 전)에 구매 로그가 있으면 그 문제를 돌려준다. 없으면 null.
        // = "구매 로그가 그 판의 결산 로그보다 먼저 왔다".
        // Assert를 안에서 부르지 않는다 — 뒤바뀐 순서를 잡아내는지 확인하는 테스트가 결과를 그대로 검사할 수 있게.
        private static string FindPurchaseInsideOpenRun(IReadOnlyList<PendingLog> logs)
        {
            string openRun = null;

            foreach (PendingLog log in logs)
            {
                switch (log.Type)
                {
                    case LogTypes.RunStart:
                        if (openRun != null)
                            return $"seq {log.Seq}: 이전 판의 결산 없이 다음 판이 시작됐다.";
                        openRun = PayloadOf<RunStartPayloadDto>(log).runId;
                        break;

                    case LogTypes.Settlement:
                        string runId = PayloadOf<SettlementPayloadDto>(log).runId;
                        if (runId != openRun)
                            return $"seq {log.Seq}: 결산의 runId({runId})가 열린 판({openRun ?? "없음"})과 다르다.";
                        openRun = null;
                        break;

                    case LogTypes.NodePurchase:
                        if (openRun != null)
                            return $"seq {log.Seq}: 판({openRun})의 결산 로그보다 구매 로그가 먼저 왔다. 서버에서 그 판의 보상이 사라진다.";
                        break;
                }
            }

            return null;
        }

        // 서버가 하듯 seq 순서로 Gold를 다시 계산한다. 구매 시점의 Gold가 비용보다 적으면 그 seq를 돌려준다(서버라면 거부).
        // 판 시작의 startGold도 그때까지의 Gold와 같아야 한다.
        private static long? FirstUnaffordablePurchase(IEnumerable<PendingLog> logs, long initialGold = 0)
        {
            long gold = initialGold;

            foreach (PendingLog log in logs.OrderBy(l => l.Seq))
            {
                switch (log.Type)
                {
                    case LogTypes.Reset:
                        gold = 0;
                        break;
                    case LogTypes.RunStart:
                        Assert.That(PayloadOf<RunStartPayloadDto>(log).startGold, Is.EqualTo(gold),
                            $"seq {log.Seq}: 판 시작 Gold가 앞선 로그로 계산한 Gold와 다르다.");
                        break;
                    case LogTypes.Settlement:
                        gold += PayloadOf<SettlementPayloadDto>(log).settledGold;
                        break;
                    case LogTypes.NodePurchase:
                        long cost = PayloadOf<NodePurchasePayloadDto>(log).cost;
                        if (gold < cost)
                            return log.Seq;
                        gold -= cost;
                        break;
                }
            }

            return null;
        }

        // ---------- 테스트 ----------

        [Test]
        public void 결산_뒤_업그레이드에서_산_노드는_결산_로그_다음_seq를_받는다()
        {
            var flow = new PlayerFlow();
            flow.StartNew();
            flow.StartBattle();
            flow.EndBattle(settledGold: 120);

            Assert.That(flow.SettlementShown, Is.True);
            Assert.That(flow.Buy(NodeA, cost: 100), Is.True, "결산 보상으로 살 수 있어야 한다.");

            IReadOnlyList<PendingLog> logs = flow.Ledger.Pending;
            Assert.That(TypesOf(logs), Is.EqualTo(new[]
            {
                LogTypes.Reset, LogTypes.RunStart, LogTypes.Settlement, LogTypes.NodePurchase,
            }));

            PendingLog settlement = logs.Single(l => l.Type == LogTypes.Settlement);
            PendingLog purchase = logs.Single(l => l.Type == LogTypes.NodePurchase);
            Assert.That(settlement.Seq, Is.LessThan(purchase.Seq));
            Assert.That(purchase.Seq, Is.EqualTo(settlement.Seq + 1));
        }

        [Test]
        public void 결산_보상으로만_살_수_있는_구매도_seq_순서로_재생하면_Gold가_모자라지_않는다()
        {
            var flow = new PlayerFlow();
            flow.StartNew();
            flow.StartBattle();
            flow.EndBattle(settledGold: 120);
            flow.Buy(NodeA, cost: 100); // 판 시작 때 Gold는 0 — 보상 없이는 살 수 없다.

            Assert.That(FirstUnaffordablePurchase(flow.Ledger.Pending), Is.Null);
            Assert.That(FindPurchaseInsideOpenRun(flow.Ledger.Pending), Is.Null);
        }

        [Test]
        public void 여러_판에_걸쳐_구매가_끼어도_모든_구매가_그_판의_결산_뒤에_온다()
        {
            var flow = new PlayerFlow();
            flow.StartNew();

            // 1판 → 업그레이드에서 구매 → 2판(시작 Gold = 남은 Gold) → 결산 화면에서 "계속"(구매 없음) → 3판 → 구매 두 번
            flow.StartBattle();
            flow.EndBattle(150);
            flow.Buy(NodeA, 100);

            flow.StartBattle();
            flow.EndBattle(80);

            flow.StartBattle();
            flow.EndBattle(200);
            flow.Buy(NodeA, 120);
            flow.Buy(NodeB, 150);

            IReadOnlyList<PendingLog> logs = flow.Ledger.Pending;
            Assert.That(FindPurchaseInsideOpenRun(logs), Is.Null);
            Assert.That(FirstUnaffordablePurchase(logs), Is.Null);
            Assert.That(logs.Select(l => l.Seq), Is.Ordered.Ascending);
        }

        [Test]
        public void 전송_배치에서도_결산이_구매보다_앞선다()
        {
            var flow = new PlayerFlow();
            flow.StartNew();
            flow.StartBattle();
            flow.EndBattle(120);
            flow.Buy(NodeA, 100);

            // 전송(SyncSender, W3-4)은 대기 로그를 seq 오름차순 그대로 보낸다.
            SynchronizationContext saved = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            var api = new FakeSyncApi();
            api.Enqueue(SyncResults.Applied(4));

            try
            {
                using var sender = new SyncSender(new FakeAuthSession(), api, new LedgerSyncStore(flow.Ledger), 10);
                TestTasks.Done(sender.SyncOnStartupAsync());
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saved);
            }

            Assert.That(api.Batches.Count, Is.EqualTo(1));
            string[] sent = TypesOf(api.Batches[0]);
            Assert.That(Array.IndexOf(sent, LogTypes.Settlement),
                Is.LessThan(Array.IndexOf(sent, LogTypes.NodePurchase)));
            Assert.That(FindPurchaseInsideOpenRun(api.Batches[0]), Is.Null);
        }

        // 검사 도구가 실제로 뒤바뀐 순서를 잡아내는지 확인한다(검사가 항상 통과하는 것을 막는다).
        [Test]
        public void 구매가_결산보다_먼저_오면_검사가_잡아낸다()
        {
            var flow = new PlayerFlow();
            flow.StartNew();
            flow.StartBattle();
            flow.EndBattle(120);
            flow.Buy(NodeA, 100);

            // 결산(seq 3)과 구매(seq 4)의 순서를 바꾼 장부: 구매가 seq 3, 결산이 seq 4.
            // 봉투 JSON 안의 seq는 원래 값 그대로지만 검사 도구는 PendingLog.Seq와 payload만 본다.
            PendingLog[] logs = flow.Ledger.Pending.ToArray();
            PendingLog settlement = logs[2];
            PendingLog purchase = logs[3];
            var swapped = new[]
            {
                logs[0],
                logs[1],
                new PendingLog(settlement.Seq, purchase.Type, purchase.Json),
                new PendingLog(purchase.Seq, settlement.Type, settlement.Json),
            };

            Assert.That(FirstUnaffordablePurchase(swapped), Is.EqualTo(3), "보상 전 Gold(0)로는 살 수 없어야 한다.");
            Assert.That(FindPurchaseInsideOpenRun(swapped), Does.Contain("seq 3"));
        }
    }
}
