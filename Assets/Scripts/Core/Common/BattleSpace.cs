namespace BlackHole.Core
{
    // 전투 공간의 기준. HQ는 Gameplay 공간의 논리적 원점 (0,0)이다(GAME_RULES 3.1).
    // 공전·출현 배치처럼 HQ를 기준으로 하는 규칙은 모두 이 원점을 쓰고, 각 시스템이 따로 중심 좌표를 두지 않는다.
    // 원점이 HQ라는 뜻이 곧 모든 공격·경로의 중심이 HQ라는 뜻은 아니다.
    public static class BattleSpace
    {
        public static readonly Point2 Origin = new Point2(0, 0);
    }
}
