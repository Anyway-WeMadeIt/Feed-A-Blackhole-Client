namespace BlackHole.Core
{
    // 피해가 들어간 순간의 기록. 피격 연출(흔들림·파편·피해량 텍스트)과 스킬별 피해 집계가 이것을 읽는다.
    // 사망 기록(DeathRecord)처럼 적 객체를 붙잡지 않도록 필요한 값을 복사해 둔다.
    // 적을 죽인 피해도 기록한다. 그 사망은 사망 기록에 따로 남는다(같은 적이면 피격 기록이 먼저다).
    public readonly struct HitRecord
    {
        // 판 안에서 피해 순서대로 늘어나는 번호. 같은 기록을 두 번 소비하지 않는 데 쓴다.
        public long Sequence { get; }
        public EnemyId EnemyId { get; }
        public EnemyType EnemyType { get; }
        // 맞은 적의 색 등급과 톤(Enemy.Tone). 피격 파편의 색이 쓴다.
        public int Tier { get; }
        public int Tone { get; }
        // 픽업(혜성)이 맞았는가. 화면은 픽업의 피격 연출을 하지 않는다.
        public bool IsPickup { get; }
        public Point2 Position { get; }
        // 맞은 적의 반지름(크기가 반영된 값). 피격 파편의 크기가 쓴다.
        public float Radius { get; }
        public float Amount { get; }
        public bool IsCritical { get; }
        public DamageSource Source { get; }
        // 사망 효과의 피해면 그 효과를 낸 특수 적의 종류(Damage.SourceEnemyType). Breaker의 피해면 null.
        public EnemyType? SourceEnemyType { get; }

        internal HitRecord(long sequence, Enemy enemy, Damage damage)
        {
            Sequence = sequence;
            EnemyId = enemy.Id;
            EnemyType = enemy.Definition.Type;
            Tier = enemy.Tier;
            Tone = enemy.Tone;
            IsPickup = enemy.Definition.IsPickup;
            Position = enemy.Position;
            Radius = enemy.Stats.Radius;
            Amount = damage.Amount;
            IsCritical = damage.IsCritical;
            Source = damage.Source;
            SourceEnemyType = damage.SourceEnemyType;
        }
    }
}
