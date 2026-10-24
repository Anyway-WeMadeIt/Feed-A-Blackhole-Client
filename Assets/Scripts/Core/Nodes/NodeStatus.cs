namespace BlackHole.Core
{
    // 노드 하나의 지금 상태를 한 번에: 보이는 상태, 산 Rank, 다음 Rank의 비용(마지막 Rank까지 샀으면 null).
    // NodePurchase.StatusOf·StatusesOf가 만든다. 화면이 노드를 칠하고 비용을 보일 때 쓴다.
    public readonly struct NodeStatus
    {
        public string Id { get; }
        public NodeState State { get; }
        public int Rank { get; }
        public long? NextCost { get; }

        public NodeStatus(string id, NodeState state, int rank, long? nextCost)
        {
            Id = id;
            State = state;
            Rank = rank;
            NextCost = nextCost;
        }
    }
}
