using System.Collections.Generic;

namespace BlackHole.Core
{
    // 로드 결과. 오류가 하나라도 있으면 Tree는 null이다(부분 통과 금지).
    public sealed class NodeTreeLoadResult
    {
        public NodeTree Tree { get; }
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        // 콘텐츠에는 있지만 배치되지 않은 노드 ID(콘텐츠 순서). 오류가 아니다 — 트리에서 빠지고 살 수 없다.
        public IReadOnlyList<string> Unplaced { get; }
        public bool Succeeded => Tree != null;

        internal NodeTreeLoadResult(NodeTree tree, List<ContentDiagnostic> diagnostics, List<string> unplaced)
        {
            Tree = tree;
            Diagnostics = diagnostics.AsReadOnly();
            Unplaced = unplaced.AsReadOnly();
        }
    }
}
