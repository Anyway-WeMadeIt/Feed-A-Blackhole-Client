namespace BlackHole.Core
{
    // 사망이 확정된 순간의 기록. 보상·사망 효과·화면 연출은 이것을 읽는다.
    // 적 객체를 연출이 끝날 때까지 붙잡지 않도록 필요한 값을 복사해 둔다(Gameplay Lifetime ≠ Presentation Lifetime).
    public readonly struct DeathRecord
    {
        // 판 안에서 사망 순서대로 늘어나는 번호. 같은 기록을 두 번 소비하지 않는 데 쓴다.
        public long Sequence { get; }
        public EnemyId EnemyId { get; }
        public EnemyType EnemyType { get; }
        // 붙어 있던 특수 성질. 없으면 null.
        public EnemyTraitType? TraitType { get; }
        // 죽은 적의 색 등급·톤(Enemy.Tone)과 크기(SizeRule). 파편의 색·수 같은 연출이 쓴다. 픽업의 크기는 SizeRule.Base다.
        public int Tier { get; }
        public int Tone { get; }
        public int Size { get; }
        // 픽업(혜성)이 죽었는가. 화면은 픽업의 파편·Gold 텍스트를 내지 않는다.
        public bool IsPickup { get; }
        // 이 사망의 보상 내역(기본 Gold·황금 치명타 보너스). 판의 Gold 합계에 든 값과 같다. Gold 텍스트가 쓴다.
        public DeathReward Reward { get; }
        // 황금 성질이 붙어 있었는가(Gold 배율이 이미 기본 Gold에 들어 있다). Gold 텍스트의 색이 쓴다.
        public bool IsGolden => TraitType == EnemyTraitType.Golden;
        public Point2 Position { get; }
        public float Radius { get; }

        internal DeathRecord(long sequence, Enemy enemy, DeathReward reward)
        {
            Sequence = sequence;
            EnemyId = enemy.Id;
            EnemyType = enemy.Definition.Type;
            TraitType = enemy.Trait?.Type;
            Tier = enemy.Tier;
            Tone = enemy.Tone;
            Size = enemy.Size;
            IsPickup = enemy.Definition.IsPickup;
            Reward = reward;
            Position = enemy.Position;
            Radius = enemy.Stats.Radius;
        }
    }
}
