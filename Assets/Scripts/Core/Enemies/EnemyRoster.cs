using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 생명주기.
    // 출현한 적을 보관하고 이동·피해·사망을 처리하며, 이번 Step의 피격·사망 기록과 판 누적 처치 수·처치 Gold를 기록한다.
    // 판 정리(ClearAlive)는 처치가 아니다 — 사망 기록도, 처치 수도, Gold도 없다.
    internal sealed class EnemyRoster
    {
        private readonly List<Enemy> _alive = new();
        private readonly Dictionary<EnemyDefinition, int> _aliveByKind = new();
        private readonly Dictionary<EnemyDefinition, int> _killsByKind = new();
        private readonly List<EnemyDefinition> _killOrder = new();
        private readonly Dictionary<KillBucket, int> _killsByBucket = new();
        private readonly List<KillBucket> _bucketOrder = new();
        private readonly List<HitRecord> _hits = new();
        private readonly List<DeathRecord> _deaths = new();
        private readonly DeathRewards _rewards;
        private int _nextEnemyId = 1;
        private long _nextHitSequence = 1;
        private long _nextDeathSequence = 1;

        // 살아 있는 적.
        public IReadOnlyList<Enemy> Alive { get; }

        // 이번 Step의 피격과 사망(일어난 순서). 다음 Step이 시작될 때 비운다.
        public IReadOnlyList<HitRecord> Hits { get; }
        public IReadOnlyList<DeathRecord> Deaths { get; }

        // 이 판에서 확정된 처치 보상의 Gold 합계(황금 치명타 보너스 포함).
        public long KillGold { get; private set; }

        // 이 판의 황금 치명타 횟수와 그 보너스 Gold의 합계(Kill Gold에 이미 들어 있다)
        public int GoldenCritCount { get; private set; }
        public long GoldenCritBonusGold { get; private set; }

        public EnemyRoster(DeathRewards rewards)
        {
            _rewards = rewards;
            Alive = _alive.AsReadOnly();
            Hits = _hits.AsReadOnly();
            Deaths = _deaths.AsReadOnly();
        }

        // 지금 살아 있는 특정 종류의 적 수.
        public int CountAlive(EnemyDefinition kind) =>
            kind != null && _aliveByKind.TryGetValue(kind, out int count) ? count : 0;

        // 지금 살아 있는 특정 종류 중 이 성질이 붙은 적 수.
        public int CountAlive(EnemyDefinition kind, EnemyTraitDefinition trait)
        {
            int count = 0;

            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i].Definition == kind && ReferenceEquals(_alive[i].Trait, trait))
                    count++;
            }

            return count;
        }

        // 이 판에서 지금까지의 종류별 처치 수(처음 처치한 순서).
        public IReadOnlyList<EnemyKillCount> GetKillCounts()
        {
            var kills = new EnemyKillCount[_killOrder.Count];

            for (int i = 0; i < kills.Length; i++)
                kills[i] = new EnemyKillCount(_killOrder[i], _killsByKind[_killOrder[i]]);

            return System.Array.AsReadOnly(kills);
        }

        // 이 판에서 지금까지의 칸별 처치 수 (처음 처치한 순서)
        public IReadOnlyList<KillBucketCount> GetKillBuckets()
        {
            var buckets = new KillBucketCount[_bucketOrder.Count];

            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = new KillBucketCount(_bucketOrder[i], _killsByBucket[_bucketOrder[i]]);

            return System.Array.AsReadOnly(buckets);
        }

        public Enemy Spawn(EnemyDefinition definition, int tier, EnemyTraitDefinition trait, int size, EnemyStats stats, Point2 position, bool respawned = false)
        {
            EnemyId id = new(_nextEnemyId++);
            var enemy = new Enemy(
                id,
                definition,
                tier,
                trait,
                size,
                stats,
                position,
                respawned);

            _alive.Add(enemy);
            _aliveByKind[definition] = CountAlive(definition) + 1;
            return enemy;
        }

        public void Move(float delta)
        {
            for (int i = 0; i < _alive.Count; i++)
                _alive[i].Move(delta);
        }

        // true는 이번 피해로 죽었다는 뜻.
        public bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!ContainsAlive(enemy))
                return false;

            bool died = enemy.ApplyDamage(damage);
            _hits.Add(new HitRecord(_nextHitSequence++, enemy, damage));

            if (!died)
                return false;

            RecordDeath(enemy);
            return true;
        }

        // 이 목록에 살아 있는 적인가. 피해는 이것을 먼저 본 뒤에만 적을 바꾼다.
        private bool ContainsAlive(Enemy enemy) => enemy.IsAlive && _alive.Contains(enemy);

        // 사망 확정: 보상을 한 번 정하고(DeathRewards), 사망 기록·Gold·살아 있는 목록과 종류별 수·처치 수를 갱신한다.
        // Gold는 흡수 연출을 기다리지 않고 지금 더한다.
        private void RecordDeath(Enemy enemy)
        {
            DeathReward reward = _rewards.Resolve(enemy);
            _deaths.Add(new DeathRecord(_nextDeathSequence++, enemy, reward));
            KillGold = checked(KillGold + reward.TotalGold);

            if (reward.IsCritical)
            {
                GoldenCritCount++;
                GoldenCritBonusGold = checked(GoldenCritBonusGold + reward.BonusGold);
            }

            RecordBucket(enemy);

            if (_alive.Remove(enemy))
                _aliveByKind[enemy.Definition] = CountAlive(enemy.Definition) - 1;

            if (_killsByKind.TryGetValue(enemy.Definition, out int kills))
            {
                _killsByKind[enemy.Definition] = kills + 1;
            }
            else
            {
                _killsByKind.Add(enemy.Definition, 1);
                _killOrder.Add(enemy.Definition);
            }
        }

        // 처치 하나를 그 칸(종류, 색 등급, 크기, 황금 여부)에 더한다.
        private void RecordBucket(Enemy enemy)
        {
            var bucket = new KillBucket(
                enemy.Definition,
                enemy.Tier,
                enemy.Size,
                DeathRewards.IsGolden(enemy));

            if (_killsByBucket.TryGetValue(bucket, out int kills))
            {
                _killsByBucket[bucket] = kills + 1;
            }
            else
            {
                _killsByBucket.Add(bucket, 1);
                _bucketOrder.Add(bucket);
            }
        }

        // 판 정리: 남은 적을 목록에서 치운다(처치가 아니다). 치운 수를 돌려준다.
        public int ClearAlive()
        {
            int cleared = _alive.Count;
            _alive.Clear();
            _aliveByKind.Clear();
            return cleared;
        }

        // 이번 Step의 피격·사망 기록을 비운다(World.Step이 처음에 부른다).
        public void BeginStep()
        {
            _hits.Clear();
            _deaths.Clear();
        }
    }
}
