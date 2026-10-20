namespace BlackHole.Core
{
    // 적 종류. 종류마다 적 종류 에셋(EnemyKind)이 하나 있고, 적 종류 목록(EnemyCatalog)에 한 번만 들어간다.
    // 에셋에 숫자로 저장되므로 이미 있는 값의 숫자는 바꾸지 않는다.
    public enum EnemyType
    {
        Asteroid = 0,
        Planet = 1,
        Star = 2,
        // 픽업(주기 출현).
        Comet = 3,
        // 별 다음 단계(별 → 은하 변환). 화면에서 나선팔이 자전하고, 죽으면 나선이 중심으로 말려 들어간다.
        Galaxy = 4,
    }
}
