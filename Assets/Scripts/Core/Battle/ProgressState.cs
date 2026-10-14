using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 게임의 진행 상태.
    // Gold, 구매한 노드 Rank, 블랙홀 성장도를 보관하며 전투 사이와 앱 재실행 뒤에도 이어진다.
    // 노드 구매와 전투 결산 때만 바뀌고 ProgressSave로 저장·복원한다. 새 진행은 Gold 0, 성장도 0, 산 노드 없음에서 시작한다.
    public sealed class ProgressState
    {
        private readonly List<string> _ownedNodes = new List<string>();
        private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>(StringComparer.Ordinal);

        // 원작의 금액은 T(조) 단위까지 오르므로 int(약 21억)가 아니라 long이다.
        public long Gold { get; private set; }
        // Rank를 하나라도 산 노드의 ID(처음 산 순서). ID로 기록하므로 트리를 다시 불러와도 이어진다.
        public IReadOnlyList<string> OwnedNodes { get; }
        // 블랙홀의 성장도: 도달한 이정표의 수. 새 진행은 0이다. 결산 때만, 한 판에 최대 1 오른다. 줄지 않는다.
        // 판의 Level·EXP는 저장하지 않는다 — 매 판 성장도의 시작 Level(마지막 이정표의 Level)에서 시작한다(HqGrowthDefinition).
        public int GrowthStage { get; private set; } = HqGrowthDefinition.StartStage;

        public ProgressState()
        {
            OwnedNodes = _ownedNodes.AsReadOnly();
        }

        // 이 노드를 몇 Rank까지 샀는가. 사지 않았으면 0이다.
        public int RankOf(string nodeId) =>
            nodeId != null && _ranks.TryGetValue(nodeId, out int rank) ? rank : 0;

        // Rank를 하나라도 샀는가. 선으로 이어진 노드가 드러나는 기준이다(NodeGraph.IsRevealed).
        public bool Owns(string nodeId) => RankOf(nodeId) > 0;

        // Gold를 더한다. 전투 중에는 부르지 않는다 — 판이 끝난 뒤 결산(GameSession.Settle)이 그 판이 번 Gold로 한 번 부른다.
        // 그래서 진행 상태는 전투 밖에서만 바뀌고, 저장 시점도 전투 밖이다.
        public void EarnGold(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "0 이상이어야 한다.");

            Gold = checked(Gold + amount);
        }

        // 결산(GameSession.Settle)이 판의 결산 뒤 성장도(Hq.NextStage)를 반영한다. 그대로이거나 1 오른다.
        internal void KeepGrowthStage(int stage)
        {
            if (stage < GrowthStage || stage > GrowthStage + 1)
                throw new ArgumentOutOfRangeException(nameof(stage), $"결산은 성장도를 그대로 두거나 1 올린다. 지금 {GrowthStage}, 받은 값 {stage}.");

            GrowthStage = stage;
        }

        // 구매 규칙(NodePurchase.TryPurchase)이 최대 Rank와 Gold를 확인한 뒤에만 부른다. 노드의 Rank를 하나 올린다.
        internal void BuyRank(string nodeId, long cost)
        {
            Gold -= cost;
            int rank = RankOf(nodeId) + 1;
            _ranks[nodeId] = rank;

            if (rank == 1)
                _ownedNodes.Add(nodeId);
        }

        // 저장(ProgressSave)이 검사를 마친 값으로 덮어쓴다. 지금 값은 모두 버린다. ranks는 처음 산 순서다.
        internal void Restore(long gold, int growthStage, IReadOnlyList<(string NodeId, int Rank)> ranks)
        {
            Gold = gold;
            GrowthStage = growthStage;
            _ranks.Clear();
            _ownedNodes.Clear();

            foreach ((string nodeId, int rank) in ranks)
            {
                _ranks.Add(nodeId, rank);
                _ownedNodes.Add(nodeId);
            }
        }
    }
}
