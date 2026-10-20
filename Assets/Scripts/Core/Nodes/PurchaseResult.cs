namespace BlackHole.Core
{
    // 다음 Rank 하나를 사려 할 때의 결과.
    public enum PurchaseResult
    {
        Purchased,
        UnknownNode,    // 요청한 노드 ID가 트리에 없다(배치되지 않은 노드 포함).
        MaxRankReached, // 마지막 Rank까지 이미 샀다.
        Hidden,         // 아직 드러나지 않았다: 시작 노드가 아니고, Rank를 산 이웃도 없음.
        NotEnoughGold,  // 다음 Rank의 비용이 모자란다.
    }
}
