namespace BlackHole.Core
{
    public readonly struct AdvanceResult
    {
        // 이번 진행에서 오른 블랙홀 Level 수. 이정표에 닿아 끝난 진행은 0이다(성장 연출 없이 끝난다).
        public int RaisedLevels { get; }

        // 이번 진행에서 판이 끝났으면 그 이유. 이미 끝난 판의 진행은 null이다 — 끝난 순간 한 번만 값이 있다.
        public BattleEndReason? EndReason { get; }

        // 이번 진행에서 판이 끝났는가.
        public bool Ended => EndReason.HasValue;

        public AdvanceResult(int raisedLevels, BattleEndReason? endReason)
        {
            RaisedLevels = raisedLevels;
            EndReason = endReason;
        }
    }
}
