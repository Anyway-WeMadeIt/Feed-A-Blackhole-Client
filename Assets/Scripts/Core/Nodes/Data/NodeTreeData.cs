using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리 배치의 저작 형식. 검증 전 값이며 실행에 쓰지 않는다
    // (오직 NodeTreeLoader만이 읽음)
    //
    // Unity 쪽 노드 목록 에셋(NodeCatalog)이 이 형식을 그대로 담고, 노드 도구로 칸·선·시작 노드를 고친다.
    // 어떤 노드가 있는지(ID·Rank·비용·효과)는 노드 콘텐츠(NodeContent, 시트)가 정한다. 배치는 그 노드를 어디에 놓고 무엇과 잇는지만 가진다.
    [Serializable]
    public sealed class NodeTreeData
    {
        public List<NodeData> Nodes = new();

        // 구역(우주 구역). 화면 표시용이며 비어 있어도 된다.
        public List<NodeRegionData> Regions = new();
    }
}
