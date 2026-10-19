using System;

namespace BlackHole.Core
{
    public sealed class Enemy
    {
        public EnemyId Id { get; }
        public EnemyDefinition Definition { get; }

        // 색 등급(종류의 색 등급 표 번호).
        public int Tier { get; }

        // 크기(SizeRule.Base부터). 출현 때 그 종류의 열린 크기 중 하나로 정해진다. Stats는 이미 이 크기가 반영된 값이다. 픽업은 SizeRule.Base다.
        public int Size { get; }

        // 마지막 색 등급의 톤(0 = 색 등급 색 그대로, 1부터 = 추가 톤). 톤 수는 판 구성(이 종류와 상위 종류의 색 수, MassRule.LastTierTones)이 정하고 출현 때 같은 몫으로 정해진다(EnemySupply).
        // 색만 다르고 수치는 같다 — Stats·보상·장부에 들어가지 않는다. 마지막 색 등급이 아니면 늘 0.
        public int Tone { get; }

        // 붙은 특수 성질(황금·전기·달 …). 없으면 null. 출현 때 정해지고, 한 마리에 최대 하나다.
        // 이 판 구성의 성질 객체다(노드가 반영된 수치 — EnemyComposition.Traits).
        public EnemyTraitDefinition Trait { get; }

        // 특수 적인가(성질이 붙었는가). 특수 적은 사망 때 성질의 효과가 발동한다. 사망 효과의 피해도 받는다(달·픽업 제외) — 효과가 효과를 부른다(DeathEffects).
        public bool IsSpecial => Trait != null;

        // 재생성 세대. 0이면 판 공급으로 나온 적, n이면 재생성을 n번 거쳐 나온 적. 다음 재생성 확률은 세대마다 종류의 RespawnDecay만큼 줄어든다(DeathBonuses).
        public int RespawnGeneration { get; }

        // 재생성(파괴 때 RespawnChance 성공)으로 나온 적인가.
        public bool IsRespawned => RespawnGeneration > 0;

        // 적의 스탯 수치. 출현 때 판의 적 수치 표에서 (종류, 색 등급, 성질, 크기)의 값을 받음.
        public EnemyStats Stats { get; }

        public float Health { get; private set; }

        public bool IsAlive { get; private set; } = true;

        public Point2 Position { get; private set; }

        // 마지막으로 피해를 준 스킬·효과. 맞은 적이 없으면 null.
        public DamageSource? LastDamageSource { get; private set; }

        internal Enemy(
            EnemyId id,
            EnemyDefinition definition,
            int tier,
            EnemyTraitDefinition trait,
            int size,
            EnemyStats stats,
            Point2 position,
            int respawnGeneration = 0,
            int tone = 0)
        {
            if (size < SizeRule.Base || size > SizeRule.Max)
                throw new ArgumentOutOfRangeException(nameof(size), $"크기는 {SizeRule.Base}부터 {SizeRule.Max}까지다. 받은 값: {size}.");

            if (tone < 0)
                throw new ArgumentOutOfRangeException(nameof(tone), $"톤은 0 이상이다. 받은 값: {tone}.");

            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Tier = tier;
            Size = size;
            Trait = trait;
            Stats = stats;
            Health = stats.MaxHealth;
            Position = position;
            RespawnGeneration = respawnGeneration;
            Tone = tone;
        }

        // 점 point에서 거리 reach 안에 이 적의 원(반지름 Stats.Radius)이 닿는가. 공격과 사망 효과가 적을 맞히는 판정이다.
        internal bool IsWithin(Point2 point, float reach)
        {
            float touch = reach + Stats.Radius;
            return Position.DistanceSquared(point) <= touch * touch;
        }

        internal void Move(float delta)
        {
            Position = EnemyBehaviors.NextPosition(Position, Stats, delta);
        }

        // 공전이 아닌 힘(중력장)이 옮긴 자리. 공전은 위치만 보고 돌므로 다음 공전은 이 자리에서 이어진다.
        internal void MoveTo(Point2 position)
        {
            Position = position;
        }

        internal bool ApplyDamage(Damage damage)
        {
            if (!IsAlive)
                return false;

            Health = Math.Max(0, Health - damage.Amount);
            LastDamageSource = damage.Source;

            if (Health > 0)
                return false;

            IsAlive = false;
            return true;
        }
    }
}
