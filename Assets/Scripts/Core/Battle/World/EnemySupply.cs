using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 적 공급을 담당한다.
    // 생성 요청에 따라 일반 적을 만들고, 혜성 같은 주기 출현 적을 별도 주기로 생성한다.
    // 적 종류의 변환과 시작, 성장 공급량은 판 조립 때 이미 정해져 들어온다.
    internal sealed class EnemySupply
    {
        // 일반 공급 적의 성능 보호용 상한. 게임 규칙상의 최대 개체 수는 아니다.
        private const int SafetyMaxAlive = 1000;

        private readonly EnemyRoster _enemies;
        private readonly EnemyStatTable _stats;
        private readonly EnemyPlacementDefinition _placement;
        private readonly PeriodicSpawnPlacementDefinition _periodicSpawnPlacement;

        // Level업 한 번마다 요청할 적 종류와 수.
        private readonly IReadOnlyList<SupplyRequest> _growthSupply;

        // 이 성질의 현재 활성 수.
        // 살아 있는 적과 Breaker에 남아 있는 해당 버프 중첩을 함께 센다.
        private readonly Func<EnemyDefinition, EnemyTraitDefinition, int> _countActive;

        private readonly BattleRandom _placementRandom;
        private readonly BattleRandom _periodicSpawnPlacementRandom;
        private readonly BattleRandom _periodicSpawnRandom;
        private readonly BattleRandom _rainRandom;

        // 종류별 색 등급·성질·크기 비율을 판 전체에 걸쳐 맞추는 몫 선택기.
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _tierPickers = new();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _traitPickers = new();
        private readonly Dictionary<EnemyDefinition, QuotaPicker> _sizePickers = new();

        // 주기 출현 종류마다 다음 출현 판정까지 누적된 시간.
        private readonly List<SpawnClock> _spawnClocks = new();

        // 일반 공급 적의 안전 상한 계산에서 제외할 픽업 종류.
        private readonly List<EnemyDefinition> _pickupKinds = new();

        // 다음 공급 처리 때 생성할 요청.
        private readonly List<SupplyRequest> _requests = new();

        private sealed class SpawnClock
        {
            public EnemyDefinition Kind;
            public float Elapsed;
            // 마지막 혜성 비가 내린 판 시각(초). 없으면 음수.
            public float LastRainAt = -1;
        }

        // 판이 흐른 시간(초). 혜성 비의 시작 유예(RainDelay)와 쿨타임(RainCooldown) 판정에 쓴다.
        private float _elapsed;

        internal EnemySupply(
            int seed,
            EnemyRoster enemies,
            EnemyStatTable stats,
            EnemyPlacementDefinition placement,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement,
            IReadOnlyList<SupplyRequest> growthSupply,
            Func<EnemyDefinition, EnemyTraitDefinition, int> countActive)
        {
            _enemies = enemies;
            _stats = stats;
            _placement = placement;
            _periodicSpawnPlacement = periodicSpawnPlacement;
            _growthSupply = growthSupply;
            _countActive = countActive;
            _placementRandom = new BattleRandom(seed, RandomStream.Placement);
            _periodicSpawnPlacementRandom = new BattleRandom(seed, RandomStream.PeriodicSpawnPlacement);
            _periodicSpawnRandom = new BattleRandom(seed, RandomStream.PeriodicSpawn);
            _rainRandom = new BattleRandom(seed, RandomStream.Rain);

            var tierRandom = new BattleRandom(seed, RandomStream.Tier);
            var traitRandom = new BattleRandom(seed, RandomStream.Trait);
            var sizeRandom = new BattleRandom(seed, RandomStream.Size);

            foreach (EnemyDefinition kind in stats.Kinds)
            {
                EnemyComposition composition = stats.CompositionOf(kind);
                _tierPickers.Add(kind, new QuotaPicker(stats.TierRatiosOf(kind), tierRandom));

                if (kind.IsPickup)
                {
                    _pickupKinds.Add(kind);

                    if (composition.SpawnChance > 0)
                        _spawnClocks.Add(new SpawnClock { Kind = kind });

                    continue;
                }

                float traitSum = composition.TraitChanceSum;

                if (traitSum > 0)
                {
                    var cells = new float[composition.Traits.Count + 1];
                    cells[0] = Math.Max(0, 1 - traitSum);

                    for (int i = 0; i < composition.Traits.Count; i++)
                        cells[i + 1] = composition.TraitChances[i];

                    _traitPickers.Add(kind, new QuotaPicker(cells, traitRandom));
                }

                int size = composition.Size;

                if (size > SizeRule.Base)
                {
                    var sizes = new float[size - SizeRule.Base + 1];

                    for (int i = 0; i < sizes.Length; i++)
                        sizes[i] = 1;

                    _sizePickers.Add(kind, new QuotaPicker(sizes, sizeRandom));
                }
            }
        }

        // 생성 요청: 이 종류를 몇 마리. 다음 공급 처리(ProcessRequests) 때 사용.
        internal void Request(SupplyRequest request) => _requests.Add(request);

        // Level업 한 번마다 성장 공급을 요청).
        internal void RequestGrowth(int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                foreach (SupplyRequest request in _growthSupply)
                    _requests.Add(request);
            }
        }

        // 쌓인 생성 요청을 요청 순서대로, 요청한 수만큼 한 마리씩 출현 띠 안에 만듬.
        internal void ProcessRequests()
        {
            int room = SafetyMaxAlive - SuppliedAlive();

            foreach (SupplyRequest request in _requests)
            {
                for (int i = 0; i < request.Count && room > 0; i++, room--)
                {
                    EnemyDefinition kind = request.Enemy;
                    int tier = _tierPickers[kind].Pick();
                    EnemyTraitDefinition trait = TraitOf(kind);
                    int size = _sizePickers.TryGetValue(kind, out QuotaPicker sizePicker)
                        ? sizePicker.Pick() + SizeRule.Base
                        : SizeRule.Base;

                    _enemies.Spawn(
                        kind,
                        tier,
                        trait,
                        size,
                        _stats.Of(kind, tier, trait, size),
                        _placement.Pick(_placementRandom),
                        request.RespawnGeneration);
                }
            }

            _requests.Clear();
        }

        // 주기 출현 종류마다 출현 주기가 찰 때마다 등장 확률로 하나를 주기 출현 띠 안에 만듬.
        internal void AdvancePeriodicSpawns(float delta)
        {
            _elapsed += delta;

            foreach (SpawnClock clock in _spawnClocks)
            {
                clock.Elapsed += delta;
                EnemyDefinition kind = clock.Kind;
                EnemyComposition composition = _stats.CompositionOf(kind);

                while (clock.Elapsed >= kind.SpawnPeriod)
                {
                    clock.Elapsed -= kind.SpawnPeriod;

                    if (_periodicSpawnRandom.NextFloat() >= composition.SpawnChance)
                        continue;

                    // 혜성 비: 판 시작 유예(RainDelay)가 지났고 마지막 비로부터 쿨타임(RainCooldown)이 지났을 때만 판정한다. 아니면 하나만 나온다(난수도 쓰지 않는다).
                    bool rainAllowed = kind.RainCount > 1 && _elapsed >= kind.RainDelay
                        && (clock.LastRainAt < 0 || _elapsed - clock.LastRainAt >= kind.RainCooldown);
                    int count = rainAllowed && _rainRandom.Roll(composition.RainChance) ? kind.RainCount : 1;

                    if (count > 1)
                        clock.LastRainAt = _elapsed;
                    EnemyTraitDefinition trait = composition.Traits[0];

                    for (int i = 0; i < count; i++)
                    {
                        int tier = _tierPickers[kind].Pick();
                        Point2 position = _periodicSpawnPlacement.Resolve(_placement).Pick(_periodicSpawnPlacementRandom);

                        _enemies.Spawn(
                            kind,
                            tier,
                            trait,
                            SizeRule.Base,
                            _stats.Of(kind, tier, trait),
                            position);
                    }
                }
            }
        }

        internal void Clear() => _requests.Clear();

        // 테스트 도구(BattleCheats)가 부른다: 종류·색 등급·성질·크기를 정해 count마리를 출현 띠에 바로 만든다.
        // 성질과 크기는 이 판의 판 구성 안의 것이어야 한다(EnemyStatTable.Of가 검사한다). 동시 상한(SafetyMaxAlive)까지만 만든다.
        internal int SpawnExact(EnemyDefinition kind, int tier, EnemyTraitDefinition trait, int size, int count)
        {
            int room = SafetyMaxAlive - SuppliedAlive();
            int made = 0;

            for (; made < count && made < room; made++)
            {
                _enemies.Spawn(
                    kind,
                    tier,
                    trait,
                    size,
                    _stats.Of(kind, tier, trait, size),
                    _placement.Pick(_placementRandom));
            }

            return made;
        }

        private int SuppliedAlive()
        {
            int count = _enemies.Alive.Count;

            for (int i = 0; i < _pickupKinds.Count; i++)
                count -= _enemies.CountAlive(_pickupKinds[i]);

            return count;
        }

        // 정해진 종류에 붙을 성질: 성질 몫이 있으면 그 확률만큼 성질 하나.
        // 뽑힌 성질의 동시 상한(MaxActive)이 찼으면 붙지 않음.
        // 상한은 그 성질의 지금 수(_countActive).
        private EnemyTraitDefinition TraitOf(EnemyDefinition kind)
        {
            if (!_traitPickers.TryGetValue(kind, out QuotaPicker picker))
                return null;

            int picked = picker.Pick();

            if (picked == 0)
                return null;

            EnemyTraitDefinition trait = _stats.CompositionOf(kind).Traits[picked - 1];

            return trait.MaxActive > 0 && _countActive(kind, trait) >= trait.MaxActive
                ? null
                : trait;
        }
    }
}
