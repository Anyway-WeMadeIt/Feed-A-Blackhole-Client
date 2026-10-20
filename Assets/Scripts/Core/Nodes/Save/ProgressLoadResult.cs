using System.Collections.Generic;

namespace BlackHole.Core
{
    // 저장 검사 결과. 깨진 저장이면 Progress는 null이다.
    public sealed class ProgressLoadResult
    {
        public SavedProgress Progress { get; }
        // 깨진 저장으로 본 이유.
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        // 지금 콘텐츠에 맞춰 게임에서 바꿔 쓴 값. 파일에는 원래 값이 남는다.
        public IReadOnlyList<ContentDiagnostic> Adjustments { get; }
        public bool Succeeded => Progress != null;

        internal ProgressLoadResult(SavedProgress progress, List<ContentDiagnostic> diagnostics, List<ContentDiagnostic> adjustments)
        {
            Progress = progress;
            Diagnostics = diagnostics.AsReadOnly();
            Adjustments = adjustments.AsReadOnly();
        }
    }
}
