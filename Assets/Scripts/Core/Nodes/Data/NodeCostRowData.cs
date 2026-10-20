namespace BlackHole.Core
{
    // NodeCost 시트 한 행: 노드의 Rank 하나를 살 때의 비용.
    public sealed class NodeCostRowData
    {
        // 시트 행 번호(머리칸이 1행). 진단 위치에 쓴다. 시트에서 오지 않았으면 0이다.
        public int Row;
        public string NodeId;
        public int Rank;
        public long Cost;
    }
}
