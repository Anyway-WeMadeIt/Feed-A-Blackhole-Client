using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 콘텐츠의 저작 형식: 시트 4개의 행을 그대로 담는다. 검사 전 값이며 실행에 쓰지 않는다(오직 NodeContentLoader만이 읽음).
    // 행 순서에 뜻을 두지 않는다. 노드·Rank·수치는 키로 짝짓는다.
    // 에셋에 저장하지 않는다 — CSV에서 매번 만든다(NodeContentCsv).
    public sealed class NodeContentData
    {
        public List<UpgradeStatRowData> Stats = new();
        public List<NodeRowData> Nodes = new();
        public List<NodeCostRowData> Costs = new();
        public List<NodeEffectRowData> Effects = new();
    }
}
