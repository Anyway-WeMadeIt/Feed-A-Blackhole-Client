namespace BlackHole.Core
{
    // 화면에 보이는 노드 상태. Rank를 일부만 산 노드는 다음 Rank를 살 수 있는지에 따라 Purchasable이나 Revealed다.
    // 몇 Rank까지 샀는지는 ProgressState.RankOf로 본다.
    public enum NodeState
    {
        Hidden,      // 시작 노드가 아니고, Rank를 산 이웃도 없음.
        Revealed,    // 보이지만 지금은 다음 Rank를 살 수 없다(Gold 부족).
        Purchasable, // 다음 Rank를 살 수 있다.
        Owned,       // 마지막 Rank까지 샀다.
    }
}
