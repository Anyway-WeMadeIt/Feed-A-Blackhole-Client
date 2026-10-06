using System.Collections.Generic;

namespace BlackHole.Core
{
    // 공격 Tick(World.Step의 2. Passive Attack 자리):
    // 빈 Tick도 주기를 소비.
    // 처치 버프(달·혜성)는 받은 것마다 중첩으로 따로 보관하고 따로 끝난다. 중첩당 수치는 이 Breaker의 정의(판마다 고정)가 정하고,
    // 같은 버프의 중첩은 합연산한다(보너스 합 = 중첩 수 × 중첩당 보너스). 합은 노드가 반영된 수치에 (1 + 합)으로 곱한다.
    // - 달: 공격 속도와 공격 범위를 함께 올린다.
    //   공격 속도 = 노드가 반영된 공격 속도 × (1 + 달 속도 보너스 합), 반지름 = 노드가 반영된 반지름 × (1 + 달 범위 보너스 합).
    // - 혜성: 중첩이 하나라도 있으면 모든 Tick이 치명타다.
    //   치명타 피해 보너스 = 노드가 반영된 치명타 피해 보너스 × (1 + 혜성 보너스 합). 치명타 피해 = 피해 × (1 + 치명타 피해 보너스).
    // 버프 시간은 공격한 뒤에 준다 — 중첩이 끝나는 Step의 Tick까지는 그 중첩이 효과를 낸다.
    public sealed class BreakerSkill
    {
        // 진행 시간을 더한 값의 끝자리 오차. 이만큼 모자라도 Tick 시각에 닿은 것으로 본다.
        private const float TimeEpsilon = 1e-5f;

        private readonly BattleRandom _critical;
        private readonly List<Enemy> _targets = new();
        private readonly List<BreakerTick> _ticks = new();
        private readonly List<BreakerBuff> _moon = new();
        private readonly List<BreakerBuff> _comet = new();

        private readonly List<BreakerBuff> _planet = new();
        private readonly List<BreakerBuff> _star = new();

        // 다음 Tick까지 남은 주기(기본 주기 기준).
        private float _untilNextTick;
        // 마지막으로 받은 버프의 번호(BreakerBuff.Number).
        private int _buffCount;

        public BreakerDefinition Definition { get; }

        // 켜져 있는가.
        // 지금 끄고 켜는 곳은 개발용 스킬 콘솔뿐이다(게임 규칙으로 끄는 일은 없다).
        public bool Enabled { get; private set; } = true;

        // 지금까지 일어난 Tick 수. 빈 Tick도 센다.
        public int TickCount { get; private set; }

        // 마지막 Tick이 피해를 준 적의 수. 빈 Tick이면 0이다.
        public int LastTickHitCount { get; private set; }

        // 마지막 진행 동안의 Tick(일어난 순서). 다음 진행이 시작될 때 비운다.
        public IReadOnlyList<BreakerTick> Ticks { get; }

        // 달 버프의 중첩(받은 순서). 중첩 수가 곧 목록의 길이다.
        public IReadOnlyList<BreakerBuff> MoonBuffs { get; }

        // 달 중첩의 공격 속도·공격 범위 보너스 합. 중첩이 없으면 0이다.
        public float MoonSpeedBonus => _moon.Count * Definition.MoonSpeedBonus;

        public float MoonRadiusBonus => _moon.Count * Definition.MoonRadiusBonus;

        // 혜성 버프의 중첩(받은 순서).
        public IReadOnlyList<BreakerBuff> CometBuffs { get; }

        // 혜성 중첩의 치명타 피해 보너스 증가 합. 중첩이 없으면 0이다.
        public float CometCritDamageBonus => _comet.Count * Definition.CometCritDamageBonus;

        // 혜성 중첩이 하나라도 있는가(확정 치명타).
        public bool IsGuaranteedCritical => _comet.Count > 0;

        // 지금 Tick이 쓰는 공격 원의 반지름(달 버프 포함). 판정과 화면의 범위 표시가 이 값을 쓴다.
        public float CurrentRadius => Definition.Radius * (1 + MoonRadiusBonus);

        // 지금 치명타 Tick에 적용되는 치명타 피해 보너스(혜성 버프 포함). 치명타 피해 = 피해 × (1 + 이 값).
        public float CurrentCritDamage => Definition.CritDamage * (1 + CometCritDamageBonus);


        // 현재 행성 치명타 보너스
        public float CurrentPlanetBonus => Definition.PlanetBonus * _planet.Count;

        // 현재 별 치명타 보너스
        public float CurrentStarBonus => Definition.StarBonus * _star.Count;

        // critical은 이 Breaker의 치명타만 쓰는 난수다.
        internal BreakerSkill(BreakerDefinition definition, BattleRandom critical)
        {
            Definition = definition;
            _critical = critical;
            Ticks = _ticks.AsReadOnly();
            MoonBuffs = _moon.AsReadOnly();
            CometBuffs = _comet.AsReadOnly();
        }

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
                return;

            Enabled = enabled;
            _untilNextTick = 0;
        }

        internal void BeginAdvance() => _ticks.Clear();

        // 한 Step 동안 주기가 여러 번 차면 그만큼 Tick한다. 같은 Step 안의 Tick은 같은 조준점과 같은 적 위치를 본다.
        internal void Advance(float delta, BattlePlayer owner, World world)
        {
            if (Enabled)
            {
                _untilNextTick -= delta * (1 + MoonSpeedBonus);

                while (_untilNextTick <= TimeEpsilon)
                {
                    Tick(owner, world);
                    _untilNextTick += Definition.Interval;
                }
            }

            AgeBuffs(delta);
        }

        // 새 중첩을 더한다. 이미 있는 중첩의 시간은 바꾸지 않는다. 중첩의 시간은 이 Breaker의 정의(이 판의 고정값)가 정한다.
        internal void GrantMoon() => _moon.Add(new BreakerBuff(++_buffCount, Definition.MoonDuration));

        internal void GrantComet() => _comet.Add(new BreakerBuff(++_buffCount, Definition.CometDuration));

        internal void GrantPlanet() => _planet.Add(new BreakerBuff(++_buffCount, Definition.PlanetBonus));

        internal void GrantStar() => _star.Add(new BreakerBuff(++_buffCount, Definition.StarBonus));

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

        private void Tick(BattlePlayer owner, World world)
        {
            TickCount++;
            _targets.Clear();
            Point2? center = owner.AimPoint;
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

            // 기본 데미지
            float damage = Definition.Damage;

            // 행성 + 별 데미지 보너스
            float planetDamage = Definition.Damage * CurrentPlanetBonus;
            float starDamage = Definition.Damage * CurrentStarBonus;

            damage += planetDamage + starDamage;

            if (critical) damage *= 1 + CurrentCritDamage;
            var damageData = new Damage(damage, owner.Id, critical);

            foreach (Enemy target in _targets)
                world.DealDamage(target, damageData);

            LastTickHitCount = _targets.Count;
            _ticks.Add(new BreakerTick(TickCount, center, radius, _targets.Count, critical));
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
