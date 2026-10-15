namespace BlackHole.Unity
{
    // 게임 코드가 성능 기록(PerfHud)에 알리는 입구.
    // 에디터·개발 빌드에서만 호출이 남고, 릴리스 빌드에서는 호출 줄 자체가 컴파일에서 빠진다(Conditional).
    // 그래서 부르는 쪽은 #if 없이 한 줄로 부르면 된다.
    internal static class PerfProbe
    {
        // 판이 시작됐다. 이 판의 CSV를 새로 연다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void BattleStarted(int seed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PerfHud.Instance != null)
                PerfHud.Instance.BeginBattle(seed);
#endif
        }

        // 판이 끝났다(결산·포기 모두). CSV를 닫고 요약 한 줄을 남긴다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void BattleEnded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PerfHud.Instance != null)
                PerfHud.Instance.EndBattle();
#endif
        }

        // 지금 살아 있는 적 수. 매 프레임 전투 진행 뒤에 넣는다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void SetAliveEnemies(int count)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PerfHud.Instance != null)
                PerfHud.Instance.AliveEnemies = count;
#endif
        }

        // 블랙홀 레벨이 올랐다(레벨업 프레임의 스파이크를 CSV에서 찾기 위해).
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LevelUp(int levels)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PerfHud.Instance != null)
                PerfHud.Instance.AddLevelUps(levels);
#endif
        }
    }
}
