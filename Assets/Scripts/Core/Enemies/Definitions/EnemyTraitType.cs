namespace BlackHole.Core
{
    // 특수 성질 종류. 성질이 사망 효과를 정한다(EnemyContentLoader):
    // Golden → 황금(Gold 배율), Electric → 연쇄 번개, Moon → 달 버프, Laser → 레이저, Supernova → 폭발, Comet → 혜성 버프,
    // Gravity → 중력장(주변 적을 끌어 뭉침).
    // 에셋에 숫자로 저장되므로 이미 있는 값의 숫자는 바꾸지 않는다.
    public enum EnemyTraitType
    {
        Golden = 0,
        Electric = 1,
        Moon = 2,
        Laser = 3,
        Supernova = 4,
        Comet = 5,
        Gravity = 6,
    }
}
