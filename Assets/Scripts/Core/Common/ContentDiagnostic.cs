namespace BlackHole.Core
{
    // 콘텐츠 오류 하나. Path가 핵심이다 — "BattleRules.TimeLimit"처럼 고칠 자리를 바로 가리킨다.
    public sealed class ContentDiagnostic
    {
        public string Path { get; }
        public string Message { get; }

        public ContentDiagnostic(string path, string message)
        {
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => Path.Length == 0 ? Message : $"{Path}: {Message}";
    }
}
