namespace BlackHole.Core
{
    // 판이 규칙 안에서 끝난 이유. 밖에서 끝내는 요청(종료 버튼, 포기, 테스트 도구)은 여기에 없다 — 그건 화면 흐름이 받는다.
    // 마지막 이정표(엔딩)인지는 이유가 아니라 진행 상태(Hq.NextStage ≥ MaxStage)로 판단한다. 튜토리얼인지는 판의 종류(BattleSystem.IsTutorial)다.
    public enum BattleEndReason
    {
        // 시간 제한이 다 됐다.
        TimeExpired,
        // 이정표 Level에 닿았다. 남은 시간과 관계없이 끝난다.
        MilestoneReached,
    }
}
