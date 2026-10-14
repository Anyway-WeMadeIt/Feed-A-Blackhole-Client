using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 공격 Tick(World.Step의 2. Passive Attack 자리):
    // 누르고 있는 동안(조준점이 있는 동안)만 친다. 조준점이 생기는 Step에 곧바로 한 번 치고, 그 뒤 주기마다 친다.
    // 조준점이 없으면 치지 않고 주기도 흐르지 않는다 — 다음에 누르면 또 곧바로 친다. 빈 Tick(조준점 없음)은 없다.
    // 누르고 있는 동안 공격 원 안의 적은 거의 멈춘다(Definition.HoldSlow, World.MoveEnemies). 손가락이 링을 가리는 터치에서 적을 붙잡아 두는 방식이다.
    // 처치 버프(달·혜성)는 받은 것마다 중첩으로 따로 보관하고 따로 끝난다. 중첩당 수치는 이 Breaker의 정의(판마다 고정)가 정하고,
    // 같은 버프의 중첩은 합연산한다(보너스 합 = 중첩 수 × 중첩당 보너스, 달 속도는 체감 적용). 범위·치명타는 노드가 반영된 수치에 (1 + 합)으로 곱하고,
    // 공격 속도는 노드 배율에 더한다.
    // - 달: 공격 속도와 공격 범위를 함께 올린다.
    //   중첩 수는 따로 막지 않는다. 달 성질의 동시 상한(EnemyTraitDefinition.MaxActive, 원작 "달 최대 개수")이 화면의 달과 남은 중첩을
    //   합쳐 세어 달 출현을 막으므로(World.CountActive) 중첩도 그 수를 넘지 않는다.
    //   공격 속도 배율 = 노드 배율(Definition.Speed) + 달 속도 보너스 합 — 덧셈이다. 곱하면 노드 속도와 달이 서로를 키워 후반에 폭주한다.
    //   반지름 = 노드가 반영된 반지름 × (1 + 달 범위 보너스 합). 체감(MoonRadiusDecay)이 있으면 범위 보너스는 상한이 되고 달마다 그 상한에 다가간다.
    //   반지름 배율은 MoonRadiusMaxScale(2배)에서 멈춘다 — 원작도 달 몇 개로 지름이 2배가 되면 더 먹어도 커지지 않는다. 중첩 수·공격 속도는 그대로 오른다.
    // - 혜성: 중첩이 하나라도 있으면 모든 Tick이 치명타다.
    //   치명타 피해 보너스 = 노드가 반영된 치명타 피해 보너스 × (1 + 혜성 보너스 합). 치명타 피해 = 피해 × (1 + 치명타 피해 보너스).
    // 버프 시간은 공격한 뒤에 준다 — 중첩이 끝나는 Step의 Tick까지는 그 중첩이 효과를 낸다.
    public sealed class BreakerSkill
    {
        // 진행 시간을 더한 값의 끝자리 오차. 이만큼 모자라도 Tick 시각에 닿은 것으로 본다.
        private const float TimeEpsilon = 1e-5f;

        // 달 버프로 커지는 반지름의 최대 배율. (1 + 달 범위 보너스 합)이 이보다 커도 이 배율까지만 쓴다(원작: 지름 2배).
        public const float MoonRadiusMaxScale = 2f;

        private readonly BattleRandom _critical;
        private readonly List<Enemy> _targets = new();
        private readonly List<BreakerTick> _ticks = new();
        private readonly List<BreakerBuff> _moon = new();
        private readonly List<BreakerBuff> _comet = new();

        // 다음 Tick까지 남은 주기(기본 주기 기준). 조준점이 없는 동안에도 흘러서 0에서 멈춘다 — 식은 뒤 누르면 곧바로 치고,
        // 식기 전에 다시 누르면 남은 만큼 기다린다(빠르게 두드려도 주기보다 자주 치지 못한다).
        private float _untilNextTick;
        // 마지막으로 받은 버프의 번호(BreakerBuff.Number).
        private int _buffCount;

        public BreakerDefinition Definition { get; }

        // 지금까지 일어난 Tick 수. 빈 Tick도 센다.
        public int TickCount { get; private set; }

        // 마지막 Tick이 피해를 준 적의 수. 빈 Tick이면 0이다.
        public int LastTickHitCount { get; private set; }

        // 이번 Step의 Tick(일어난 순서). 다음 Step이 시작될 때 비운다.
        public IReadOnlyList<BreakerTick> Ticks { get; }

        // 달 버프의 중첩(받은 순서). 중첩 수가 곧 목록의 길이다.
        public IReadOnlyList<BreakerBuff> MoonBuffs { get; }

        // 지금 공격 속도 배율: 노드 배율 + 달 속도 보너스 합(덧셈). 1이면 기본 주기마다 친다.
        public float CurrentSpeed => Definition.Speed + MoonSpeedBonus;

        // 지금 공격 주기(초, 달 버프 포함). Definition.Interval은 노드 배율만 반영한 주기다.
        public float CurrentInterval => Definition.Interval * Definition.Speed / CurrentSpeed;

        // 달 중첩의 공격 속도 보너스 합. 중첩이 없으면 0이다.
        // 체감(MoonSpeedDecay = d)이 있으면 n개의 합은 보너스 × (1 − d^n) / (1 − d) — 상한 보너스 / (1 − d)에 수렴한다. 0이면 선형.
        public float MoonSpeedBonus
        {
            get
            {
                int n = _moon.Count;
                float d = Definition.MoonSpeedDecay;

                if (n == 0)
                    return 0;

                if (d <= 0)
                    return n * Definition.MoonSpeedBonus;

                return Definition.MoonSpeedBonus * (1 - (float)Math.Pow(d, n)) / (1 - d);
            }
        }

        // 달 중첩의 범위 보너스 합. 중첩이 없으면 0이다.
        // 체감(MoonRadiusDecay = d)이 있으면 Definition.MoonRadiusBonus(기본 + 노드 moonPlanet.rangeScale)는 상한이고, 달 n개의 합은 상한 × (1 − d^n)이다.
        // 첫 달은 상한의 (1 − d)만 더하고 뒤로 갈수록 적게 더한다(d 0.7이면 30% → 51% → 66% → 76%). 0이면 선형(달 수 × 보너스).
        public float MoonRadiusBonus
        {
            get
            {
                int n = _moon.Count;
                float d = Definition.MoonRadiusDecay;

                if (n == 0)
                    return 0;

                if (d <= 0)
                    return n * Definition.MoonRadiusBonus;

                return Definition.MoonRadiusBonus * (1 - (float)Math.Pow(d, n));
            }
        }

        // 혜성 버프의 중첩(받은 순서).
        public IReadOnlyList<BreakerBuff> CometBuffs { get; }

        // 혜성 중첩의 치명타 피해 보너스 증가 합. 중첩이 없으면 0이다.
        // 체감(CometCritDecay = d)이 있으면 n개의 합은 보너스 × (1 − d^n) / (1 − d) — 상한 보너스 / (1 − d)에 수렴한다(달 속도와 같은 식). 0이면 선형.
        public float CometCritDamageBonus
        {
            get
            {
                int n = _comet.Count;
                float d = Definition.CometCritDecay;

                if (n == 0)
                    return 0;

                if (d <= 0)
                    return n * Definition.CometCritDamageBonus;

                return Definition.CometCritDamageBonus * (1 - (float)Math.Pow(d, n)) / (1 - d);
            }
        }

        // 혜성 중첩이 하나라도 있는가(확정 치명타).
        public bool IsGuaranteedCritical => _comet.Count > 0;

        // 지금 Tick이 쓰는 공격 원의 반지름(달 버프 포함). 판정과 화면의 범위 표시가 이 값을 쓴다.
        // 달 버프 배율은 MoonRadiusMaxScale까지다.
        public float CurrentRadius => Definition.Radius * Math.Min(1 + MoonRadiusBonus, MoonRadiusMaxScale);

        // 지금 치명타 Tick에 적용되는 치명타 피해 보너스(혜성 버프 포함). 치명타 피해 = 피해 × (1 + 이 값).
        public float CurrentCritDamage => Definition.CritDamage * (1 + CometCritDamageBonus);

        // critical은 이 Breaker의 치명타만 쓰는 난수다.
        internal BreakerSkill(BreakerDefinition definition, BattleRandom critical)
        {
            Definition = definition;
            _critical = critical;
            Ticks = _ticks.AsReadOnly();
            MoonBuffs = _moon.AsReadOnly();
            CometBuffs = _comet.AsReadOnly();
        }

        internal void BeginStep() => _ticks.Clear();

        // 주기는 조준점이 있든 없든 흐르지만, 치는 것은 조준점이 있는 Step뿐이다. 한 Step 동안 주기가 여러 번 차면 그만큼 Tick한다.
        // 같은 Step 안의 Tick은 같은 조준점과 같은 적 위치를 본다. 조준점이 없는 동안 주기는 0에서 멈춘다(미리 쌓이지 않는다).
        internal void Advance(float delta, World world)
        {
            // 주기는 Definition.Interval(노드 배율 반영) 단위로 쌓으므로, 달 보너스는 노드 배율에 더한 만큼의 비율로 시간을 흘린다.
            _untilNextTick -= delta * (CurrentSpeed / Definition.Speed);

            if (world.AimPoint.HasValue)
            {
                while (_untilNextTick <= TimeEpsilon)
                {
                    Tick(world);
                    _untilNextTick += Definition.Interval;
                }
            }
            else if (_untilNextTick < 0)
            {
                _untilNextTick = 0;
            }

            AgeBuffs(delta);
        }

        // 조준점이 있고 이 적이 공격 원 안에 있는가(붙잡혀 거의 멈추는 적).
        internal bool IsHolding(World world, Enemy enemy) =>
            world.AimPoint is Point2 center && enemy.IsWithin(center, CurrentRadius);

        // 새 중첩을 더한다. 이미 있는 중첩의 시간은 바꾸지 않는다. 중첩의 시간은 이 Breaker의 정의(이 판의 고정값)가 정한다.
        internal void GrantMoon() => _moon.Add(new BreakerBuff(++_buffCount, Definition.MoonDuration));

        internal void GrantComet() => _comet.Add(new BreakerBuff(++_buffCount, Definition.CometDuration));

        private void AgeBuffs(float delta)
        {
            Age(_moon, delta);
            Age(_comet, delta);
        }

        // 중첩마다 시간을 줄이고 끝난 중첩을 뺀다.
        // 매 Step 경로: 뒤에서부터 돌며 제자리에서 뺀다(RemoveAll은 대리자를 할당한다).
        private static void Age(List<BreakerBuff> buffs, float delta)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                BreakerBuff aged = buffs[i].Aged(delta);

                if (aged.Remaining <= 0)
                    buffs.RemoveAt(i);
                else
                    buffs[i] = aged;
            }
        }

        private void Tick(World world)
        {
            TickCount++;
            _targets.Clear();
            Point2? center = world.AimPoint;
            float radius = CurrentRadius;

            if (center.HasValue)
            {
                IReadOnlyList<Enemy> enemies = world.Enemies;

                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].IsWithin(center.Value, radius))
                        _targets.Add(enemies[i]);
                }
            }

            bool critical = _targets.Count > 0 && RollCritical();
            float multiplier = critical ? 1 + CurrentCritDamage : 1;

            foreach (Enemy target in _targets)
            {
                float amount = (Definition.Damage + BonusAgainst(target)) * multiplier;
                var damage = new Damage(amount, DamageSource.Breaker, critical);

                world.DealDamage(target, damage);
            }

            LastTickHitCount = _targets.Count;
            _ticks.Add(new BreakerTick(TickCount, center, radius, _targets.Count, critical));
        }

        // 대상 종류에 따른 추가 피해(노드 수치 breaker.planetBonusDamage·starBonusDamage·galaxyBonusDamage). 행성·별·은하가 아니면 0이다.
        private float BonusAgainst(Enemy target)
        {
            switch (target.Definition.Type)
            {
                case EnemyType.Planet:
                    return Definition.PlanetBonusDamage;
                case EnemyType.Star:
                    return Definition.StarBonusDamage;
                case EnemyType.Galaxy:
                    return Definition.GalaxyBonusDamage;
                default:
                    return 0;
            }
        }

        // 확정 치명타 중이면 굴리지 않고 치명타다. 확률이 0이나 1이면 굴리지 않는다.
        private bool RollCritical()
        {
            if (IsGuaranteedCritical || Definition.CritChance >= 1)
                return true;

            return Definition.CritChance > 0 && _critical.NextFloat() < Definition.CritChance;
        }
    }
}
