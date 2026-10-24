using System;
using BlackHole.Core;
using BlackHole.Sync;

namespace BlackHole.Unity
{
    // 진행 상태의 변화(새 게임, 판 시작·결산, 노드 구매)를 서버 동기화 로그로 남기고 저장한다.
    // 서버에 무엇을 어떤 순서로 알리는지가 여기에 모여 있다(계약 5.2, 5.5):
    // - 판 시작(RUN_START) → 그 판의 결산(SETTLEMENT) → 그 뒤의 구매(NODE_PURCHASE). 결산이 구매보다 먼저여야 서버가 보상 뒤의 Gold로 구매를 검사한다.
    // - 포기는 로그가 없다. 서버는 다음 판 시작에서 열린 판을 포기로 닫는다.
    // - 판 시작 로그 없이 끝난 판(테스트 도구로 시작)은 서버가 결산을 받지 않으므로(RUN_NOT_OPEN) 이번 실행을 테스트 세션으로 돌린다.
    // Unity에 의존하지 않는다. 화면 흐름(ScreenFlow)이 부른다.
    public sealed class ProgressRecorder
    {
        private readonly IProgressCommitter _store;
        private readonly SyncLogFactory _logs;
        // 시작 로그를 남긴 판의 runId(소문자 UUID). 결산 로그가 같은 값을 싣는다. 열린 판이 없으면 null.
        private string _runId;

        public ProgressRecorder(IProgressCommitter store, SyncLogFactory logs)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        }

        // 시작 로그를 남긴 판이 열려 있는가.
        public bool HasOpenRun => _runId != null;

        // 새 게임. 진행 상태를 처음으로 되돌리고 새 게임 로그와 함께 바로 저장한다.
        public void StartNewGame(ProgressState progress) =>
            _store.StartNew(progress, seq => _logs.Reset(seq));

        // 판을 시작했다. startGold는 이 판을 시작할 때의 진행도 Gold.
        public void StartRun(RunStartInfo start, long startGold)
        {
            string runId = Guid.NewGuid().ToString();
            _runId = runId;
            _store.Commit(seq => _logs.RunStart(seq, runId, start, startGold));
        }

        // 판을 결산했다(진행 상태에는 이미 반영됨). 열린 판이 없으면 테스트 세션으로 돌린다.
        public void Settle(BattleRawData raw)
        {
            string runId = _runId;
            _runId = null;

            if (runId == null)
            {
                _store.EnterTestSession();
                return;
            }

            _store.Commit(seq => _logs.Settlement(seq, runId, raw));
        }

        // 노드를 샀다. rank는 산 뒤의 Rank, cost는 사기 전에 읽은 비용.
        public void Purchase(string nodeId, int rank, long cost) =>
            _store.Commit(seq => _logs.NodePurchase(seq, nodeId, rank, cost));

        // 판을 포기했다. 로그는 없다.
        public void Abandon() => _runId = null;

        // 이번 실행은 더 저장하지 않는다. 테스트 도구가 진행 상태를 시나리오로 바꾸기 전에 부른다.
        public void StopSaving() => _store.EnterTestSession();
    }
}
