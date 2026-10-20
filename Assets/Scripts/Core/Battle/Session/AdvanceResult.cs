namespace BlackHole.Core
{
    // 판 진행 한 번의 결과(GameSession.Advance). 진행하지 않았으면 기본값(Level업 0, 끝나지 않음)이다.
    public readonly struct AdvanceResult
    {
        // 이번 진행에서 오른 블랙홀 Level 수. 이정표에 닿아 끝난 진행은 0이다(성장 연출 없이 끝난다).
        public int RaisedLevels { get; }

        // 이번 진행에서 판이 끝났는가. 이미 끝난 판의 진행은 false다 — 끝난 순간 한 번만 true다.
        public bool Ended { get; }

        public AdvanceResult(int raisedLevels, bool ended)
        {
            RaisedLevels = raisedLevels;
            Ended = ended;
        }
    }
}
