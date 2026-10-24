using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 불러온 게임 정의. 판 조립과 노드 구매는 Content·NodeTree를, 업그레이드 화면은 NodeItems를 쓴다.
    public sealed class LoadedContent
    {
        public GameContent Content { get; }
        public NodeTree NodeTree { get; }
        // 업그레이드 화면에 그릴 노드(배치 순서): 격자 칸, 첫 가격, 그림을 고르는 스탯, 최대 Rank.
        public IReadOnlyList<NodeItem> NodeItems { get; }

        // 업그레이드 화면에 그릴 구역(이름 열쇠, 노드 ID). 비어 있을 수 있다.
        public IReadOnlyList<NodeRegionItem> NodeRegions { get; }

        // 서버 콘텐츠 묶음의 버전(계약 1.3). 동기화 로그에 싣고, 서버 목차의 값과 같아야 로그가 받아들여진다.
        // 원본 콘텐츠(밸런스 프로필 적용 전)로 계산한다. 계산하지 못했으면 ""다(서버가 로그를 거부한다).
        public string ServerContentVersion { get; }

        internal LoadedContent(GameContent content, NodeTree nodeTree, IReadOnlyList<NodeItem> nodeItems,
            IReadOnlyList<NodeRegionItem> nodeRegions, string serverContentVersion)
        {
            Content = content;
            NodeTree = nodeTree;
            NodeItems = nodeItems;
            NodeRegions = nodeRegions;
            ServerContentVersion = serverContentVersion ?? "";
        }
    }
}
