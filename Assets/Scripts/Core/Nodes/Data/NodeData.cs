using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 하나의 배치: 어느 칸에 놓였고, 무엇과 이어졌고, 시작 노드인가. 노드 도구가 원본이다.
    // 비용·효과(Rank)는 노드 콘텐츠(NodeDefinition)가 가진다. ID로 짝짓는다.
    [Serializable]
    public sealed class NodeData
    {
        // 노드 ID. 노드 콘텐츠(Nodes 시트)에 있는 ID여야 한다. 진행 상태의 저장 키이기도 하다.
        public string Id;

        // 시작 노드(상시 노출)
        public bool Start;

        // 격자 칸. (한 칸에 노드 하나)
        public int X;
        public int Y;

        // 선으로 이어진 노드의 ID. 선은 방향이 없어 한쪽 노드에만 적어도 됨.
        public List<string> Links = new();
    }
}
