using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리 배치의 구역(우주 구역) 하나: 이름 열쇠와 그 구역에 드는 노드 ID. 화면이 구역 이름과 테두리를 그릴 때만 쓴다.
    // 구매·검증에는 들지 않는다(NodeTreeLoader가 읽지 않는다). 구역에 없는 노드는 어느 구역에도 속하지 않는 것으로 그린다.
    [Serializable]
    public sealed class NodeRegionData
    {
        // 구역 열쇠. 화면 글자는 번역표 "node.region.<Id>"에서 찾는다.
        public string Id;

        public List<string> Nodes = new();
    }
}
