using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class ContentLoadResult
    {
        public GameContent Content { get; }
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        public bool Succeeded => Content != null;

        internal ContentLoadResult(GameContent content, List<ContentDiagnostic> diagnostics)
        {
            Content = content;
            Diagnostics = diagnostics.AsReadOnly();
        }
    }
}
