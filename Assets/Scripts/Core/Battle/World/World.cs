using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적, 조준점과 Breaker, 사망 효과, 블랙홀, 이 판이 번 Gold 등을 처리
    public sealed class World
    {
        private readonly EnemyRoster _enemies;
        private readonly EnemySupply _supply;
        private readonly DeathBonuses _deathBonuses;
        private readonly BattleStatsCounter _statsCounter = new();

        public BreakerSkill Breaker { get; }
        public Point2? AimPoint { get; private set; }

        public IReadOnlyList<Enemy> Enemies => _enemies.Alive;

        // 이번 Step의 피격과 사망(일어난 순서). 다음 Step이 시작될 때 비운다.
        public IReadOnlyList<HitRecord> Hits => _enemies.Hits;
        public IReadOnlyList<DeathRecord> Deaths => _enemies.Deaths;

        // 사망 효과의 대기열과 이번 Step의 효과 기록(번개 이동, 폭발, 레이저), 켜진 중력장.
        public DeathEffects DeathEffects { get; }

        // 블랙홀.
        public Hq Hq { get; }

        // 이 판이 번 Gold(처치 보상 합계). 진행 상태에는 판이 끝난 뒤 결산(GameSession.Settle)이 더한다.
        public long EarnedGold => _enemies.KillGold;

        // 이 판의 종류별 판 구성과 (종류, 색 등급, 성질, 크기)별 수치. 판 조립 때 정해졌다.
        // 밖에서는 읽기만 한다(표는 읽기 API만 연다). 테스트 도구(메모·소환)가 이 판에 적용된 수치를 읽는다.
        public EnemyStatTable Stats { get; }

        // 적 이동 배율. 게임에서는 늘 1이고, 테스트 도구(BattleCheats)만 바꾼다(0이면 멈춤).
        internal float EnemyMoveScale { get; set; } = 1f;

        // 적이 블랙홀을 중심으로 공전하는가. 플레이어 설정(Object Rotation)이 정한다 — 끄면 적이 제자리에 머물고 중력장의 끌림만 받는다.
        // 기본은 켜짐이라 테스트는 바뀌지 않는다.
        internal bool OrbitEnabled { get; set; } = true;

        internal World(
            int seed,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement,
            Hq hq,
            BreakerDefinition breaker,
            IReadOnlyList<SupplyRequest> growthSupply,
            float killTimeBonus = 0,
            float killTimeCooldown = 0)
        {
            BattleRandom goldenCritRandom = new(seed, RandomStream.GoldenCrit);
            DeathRewards deathRewards = new(goldenCritRandom);
            _enemies = new EnemyRoster(deathRewards);
            Stats = stats;
            Hq = hq;
            DeathEffects = new DeathEffects(seed);

            BattleRandom criticalRandom = new(seed, RandomStream.Critical);
            Breaker = new BreakerSkill(breaker, criticalRandom);

            _supply = new EnemySupply(
                seed,
                _enemies,
                stats,
                placement,
                periodicSpawnPlacement,
                growthSupply,
                CountActive);

            _deathBonuses = new DeathBonuses(seed, stats, _supply, killTimeBonus, killTimeCooldown);
        }

        internal void SetAimPoint(Point2? aimPoint) => AimPoint = aimPoint;

        // 이 판에서 지금까지의 종류별 처치 수(처음 처치한 순서).
        internal IReadOnlyList<EnemyKillCount> GetKillCounts() => _enemies.GetKillCounts();

        // 이 판에서 지금까지의 칸별 처치 수(처음 처치한 순서).
        internal IReadOnlyList<KillBucketCount> GetKillBuckets() => _enemies.GetKillBuckets();

        // 이 판의 황금 치명타 횟수와 보너스 Gold 합계(EarnedGold에 이미 들어 있다).
        internal int GoldenCritCount => _enemies.GoldenCritCount;
        internal long GoldenCritBonusGold => _enemies.GoldenCritBonusGold;

        // 이 판의 시간 추가 성공 수.
        internal int TimeBonusCount => _deathBonuses.TimeBonusCount;

        // 이 판에서 지금까지의 스킬별 피해와 수집 통계. 더해진 시간은 제한 시간을 가진 판(GameSession)이 넘긴다.
        internal BattleStats CreateStats(float addedSeconds) => _statsCounter.Snapshot(Breaker.TickCount, addedSeconds);

        // 생성 요청: 지금 바로 적을 만드는 메서드가 아니라, 나중에 생성할 요청만 큐에 넣는다
        internal void RequestSpawn(SupplyRequest request) => _supply.Request(request);

        internal void ProcessSpawnRequests() => _supply.ProcessRequests();

        // 판 시작 때 주기 출현 종류(혜성)를 하나씩 둔다. 등장 확률이 0이면 두지 않는다.
        internal void SpawnStartPickups() => _supply.SpawnStartPickups();

        // 판(GameSession)이 Step 뒤에 가져가는 시간 추가 성공 수. 가져가면 0이 된다.
        internal float TakeTimeBonusSeconds() => _deathBonuses.TakeTimeBonusSeconds();

        // 판이 끝난 뒤 남은 적과 처리되지 않은 생성 요청·사망 효과를 치운다.
        // 처치가 아니다(사망 기록·처치 수·Gold 없음).
        internal void ClearRemainingEnemies()
        {
            _supply.Clear();
            DeathEffects.Clear();
            _enemies.ClearAlive();
        }

        // 테스트 도구(BattleCheats)가 부른다. 정해진 적을 바로 만든다. 만든 수를 돌려준다.
        internal int SpawnExact(EnemyDefinition kind, int tier, EnemyTraitDefinition trait, int size, int count) =>
            _supply.SpawnExact(kind, tier, trait, size, count);

        // 테스트 도구(BattleCheats)가 부른다. 살아 있는 적만 치운다. 처치가 아니다(사망 기록·처치 수·Gold 없음).
        internal void ClearAliveEnemies() => _enemies.ClearAlive();

        internal bool DealDamage(Enemy enemy, Damage damage)
        {
            if (!_enemies.DealDamage(enemy, damage))
                return false;

            Hq.AddExp(enemy.Stats.Exp);
            DeathEffects.Enqueue(enemy);
            _deathBonuses.Roll(enemy);

            return true;
        }

        internal int Step(float delta)
        {
            // 0. 이번 Step의 기록(피격·사망, 사망 효과, Breaker Tick)을 비운다.
            _enemies.BeginStep();
            DeathEffects.BeginStep();
            Breaker.BeginStep();

            // 1. Enemy Action. 이동 배율 0(테스트 도구의 멈춤)이면 위치를 다시 계산하지 않는다 — 각도 재계산의 반올림으로 조금씩 밀리지 않게.
            //    켜진 중력장이 있으면 그 범위 안의 적은 공전 대신 중력장 중심으로 끌린다. 중력장의 시간은 이동 배율과 무관하게 흐른다.
            if (EnemyMoveScale > 0)
                MoveEnemies(delta * EnemyMoveScale);

            DeathEffects.AgeFields(delta);
            _deathBonuses.Advance(delta);

            // 2. Passive Attack: (DealDamage).
            Breaker.Advance(delta, this);

            // 3. Death Effect: 이번 Step에 죽은 특수 적의 성질 효과를 사망 순서대로 처리.
            DeathEffects.Resolve(this, delta);

            // 이번 Step의 피격·사망을 판 통계에 더한다. 이 뒤로 이번 Step에는 피해가 없다.
            _statsCounter.Count(_enemies.Hits, _enemies.Deaths);

            // 4. HQ EXP / Level: 쌓인 EXP로 블랙홀의 Level 계산.
            int raised = Hq.RaiseLevels();

            if (Hq.ReachedMilestone)
                return raised;

            // 5. Growth: 오른 Level마다 성장 공급을 요청.
            _supply.RequestGrowth(raised);

            // 6. Enemy Supply: 쌓인 생성 요청(성장 공급·재생성)을 처리.
            _supply.ProcessRequests();

            // 7. Periodic Spawn: 주기 출현(혜성)의 등장 판정 진행.
            _supply.AdvancePeriodicSpawns(delta);
            return raised;
        }

        // 적 이동: 중력장 범위 안의 적은 그 중력장에 끌리고(DeathEffects.Pull), 끌린 정도의 나머지만큼 공전한다.
        // 중력장이 켜지고 꺼지는 순간과 범위 가장자리에서는 둘이 섞여 움직임이 끊기지 않는다.
        // 누르고 있는 동안(조준점) Breaker 공격 원 안의 적은 공전이 HoldSlow 배로 느려진다(붙잡힘). 중력장 끌림은 그대로다.
        private void MoveEnemies(float delta)
        {
            bool fields = DeathEffects.HasGravityFields;
            bool holding = IsHoldingAny;

            if (!fields && !holding)
            {
                if (OrbitEnabled)
                    _enemies.Move(delta);

                return;
            }

            if (fields)
                DeathEffects.BeginPull();

            IReadOnlyList<Enemy> enemies = _enemies.Alive;

            for (int i = 0; i < enemies.Count; i++)
            {
                float pulled = fields ? DeathEffects.Pull(enemies[i], delta) : 0;

                if (!OrbitEnabled || pulled >= 1)
                    continue;

                float orbit = delta * (1 - pulled);

                if (holding && Breaker.IsHolding(this, enemies[i]))
                    orbit *= Breaker.Definition.HoldSlow;

                enemies[i].Move(orbit);
            }
        }

        // 지금 붙잡기가 일어나는가: 조준점이 있고 붙잡기가 켜져 있다(HoldSlow < 1).
        private bool IsHoldingAny => AimPoint.HasValue && Breaker.Definition.HoldSlow < 1;

        // 이 적이 지금 Breaker에 붙잡혀 있는가(조준점이 있고 공격 원 안이라 거의 멈춰 있다). 화면이 붙잡힌 적을 표시할 때 쓴다.
        public bool IsHeld(Enemy enemy) => IsHoldingAny && Breaker.IsHolding(this, enemy);

        // 이 성질이 지금 판에 있는 수: 살아 있는 그 성질 적 + 그 성질이 준 버프 중 Breaker에 남은 중첩(달).
        // 성질의 동시 상한(EnemyTraitDefinition.MaxActive, 원작 "달 최대 개수")은 이 수에 건다(EnemySupply).
        private int CountActive(EnemyDefinition kind, EnemyTraitDefinition trait)
        {
            int held = trait.Effect.Type == DeathEffectType.MoonBuff ? Breaker.MoonBuffs.Count : 0;
            return _enemies.CountAlive(kind, trait) + held;
        }
    }
}
