namespace BlackHole.Core
{
    // Nodes 시트 한 행: 어떤 노드가 있는가. 시트의 Memo 칸은 기획 메모라 읽지 않는다.
    public sealed class NodeRowData
    {
        // 시트 행 번호(머리칸이 1행). 진단 위치에 쓴다. 시트에서 오지 않았으면 0이다.
        public int Row;
        public string NodeId;
        // 시트 칸 이름은 "Rank 수"다.
        public int RankCount;
    }
}
