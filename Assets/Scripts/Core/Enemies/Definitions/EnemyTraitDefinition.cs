using System;

namespace BlackHole.Core
{
    // 특수 성질 하나의 정의: 성질 종류와 사망 효과(성질 종류가 정한다).
    // 성질은 종류가 아니라 출현 때 한 마리에 붙는 특성이다(전기 소행성 = 전기 성질이 붙은 소행성). 종류(EnemyDefinition)가 붙을 수 있는 성질 목록을 가진다.
    // - 출현 때 종류의 성질마다 생성 확률(노드 electricAsteroid.spawnChance 등, %)로 하나를 고른다. 기본 0% — 노드를 사야 붙는다.
    // - 한 마리에 성질은 최대 하나다(배타). 한 종류의 성질 확률 합은 100%를 넘을 수 없다.
    // - 성질이 붙은 적은 수치(색·질량·크기)를 종류에서 그대로 받는다. 수치를 바꾸는 성질은 황금(Gold 배율)뿐이다.
    // - 성질이 붙은 적은 특수 적이다: 사망 때 성질의 효과가 발동한다. 사망 효과의 피해도 받으므로(달·픽업 제외) 효과가 다른 특수 적을 죽이면 연쇄한다(DeathEffects).
    // - MaxActive가 0보다 크면 이 성질은 판에 동시에 그 수까지만 있다(원작 "달 최대 개수"). 달처럼 버프를 주는 성질은 화면의 적과
    //   Breaker에 남은 그 버프 중첩을 합쳐 센다(World.CountActive). 다 찼으면 성질이 뽑혀도 붙지 않으므로(EnemySupply) 버프 중첩도 그 수를 넘지 않는다.
    public sealed class EnemyTraitDefinition
    {
        public EnemyTraitType Type { get; }
        public DeathEffectDefinition Effect { get; }
        // 이 성질의 동시 상한(살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩). 0이면 상한이 없다.
        public int MaxActive { get; }

        public EnemyTraitDefinition(EnemyTraitType type, DeathEffectDefinition effect, int maxActive = 0)
        {
            if (!Enum.IsDefined(typeof(EnemyTraitType), type))
                throw new ArgumentOutOfRangeException(nameof(type), $"알 수 없는 성질 종류 {(int)type}.");

            if (maxActive < 0)
                throw new ArgumentOutOfRangeException(nameof(maxActive), "0 이상이어야 한다(0 = 상한 없음).");

            Type = type;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect), "성질에는 사망 효과가 있어야 한다.");
            MaxActive = maxActive;
        }

        // 이 판의 노드가 반영된 성질. 효과의 수치나 상한만 바뀌고 종류는 같다.
        internal EnemyTraitDefinition With(DeathEffectDefinition effect) => new EnemyTraitDefinition(Type, effect, MaxActive);

        internal EnemyTraitDefinition WithMaxActive(int maxActive) => new EnemyTraitDefinition(Type, Effect, maxActive);
    }
}
