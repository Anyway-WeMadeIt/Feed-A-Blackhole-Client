namespace BlackHole.Unity
{
    // 테스트 도구가 쓰는 적·성질 ID: 밸런스 프로필 경로(enemy/<id>/…)와 느낌 메모의 키.
    // 게임 열거형(EnemyType·EnemyTraitType)의 이름에서 첫 글자만 소문자로 쓴다. Asteroid → asteroid, GoldenAsteroid → goldenAsteroid.
    // 이미 저장된 프로필·메모가 이 규칙을 쓰므로 바꾸지 않는다.
    internal static class PlaytestIds
    {
        public static string Of(string typeName) =>
            char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }
}
