namespace BlackHole.Core
{
    // NodeEffects 시트 한 행: 노드의 Rank 하나가 주는 효과 하나. 같은 (노드, Rank)에 여러 행이 있어도 된다.
    // 시트의 "표시" 칸은 Value·단위로 만든 글자라 읽지 않는다.
    public sealed class NodeEffectRowData
    {
        // 시트 행 번호(머리칸이 1행). 진단 위치에 쓴다. 시트에서 오지 않았으면 0이다.
        public int Row;
        public string NodeId;
        public int Rank;
        public string StatId;
        public float Value;
        // 시트 칸 이름은 "단위"다. 수치 정의의 단위와 같아야 한다(오타 검사용).
        public string Unit;
    }
}
