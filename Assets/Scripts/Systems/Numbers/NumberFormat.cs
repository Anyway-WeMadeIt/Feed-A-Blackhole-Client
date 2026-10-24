namespace BlackHole.Systems.Numbers
{
    // 큰 수 표기(설정 Number Format). 선택지 번호와 같은 순서다 — 게임의 GameSettings.NumberFormatOptions.
    // 전체 자릿수(1,234,567) 표기는 두지 않는다 — 적 위 획득 글자와 고정 폭 상자에서 큰 수가 넘친다. 두 표기는 길이가 거의 일정하다.
    public enum NumberFormat
    {
        Short,      // 1.2M (1,000,000 미만은 950,000) — NumberText.Short
        Scientific, // 1.2e6 — NumberText.Scientific
    }
}
