namespace BlackHole.Core
{
    // 한 처치의 보상 내역. 사망이 확정될 때 한 번 정해지고(DeathRewards), 판의 Gold 합계와 사망 기록이 같은 값을 쓴다.
    public readonly struct DeathReward
    {
        // 적의 Gold(크기·황금 배율이 이미 들어 있다).
        public long BaseGold { get; }
        // 황금 치명타 보너스. 치명타가 아니면 0.
        public long BonusGold { get; }
        public bool IsCritical { get; }
        public long TotalGold => checked(BaseGold + BonusGold);

        internal DeathReward(long baseGold, long bonusGold, bool isCritical)
        {
            BaseGold = baseGold;
            BonusGold = bonusGold;
            IsCritical = isCritical;
        }
    }
}
