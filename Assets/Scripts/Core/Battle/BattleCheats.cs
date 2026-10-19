#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace BlackHole.Core
{
    // 테스트 도구(개발 패널·시나리오)만 부르는 판 조작. 에디터와 개발 빌드에만 컴파일된다.
    // 게임 규칙을 새로 만들지 않고, 판이 원래 쓰는 길(출현·EXP·제한 시간)에 값을 넣는다:
    // - 소환한 적은 이 판의 판 구성 수치를 받는다(EnemyStatTable). 판 구성에 없는 성질·크기는 거부한다.
    // - 더한 EXP는 다음 Step에서 Level을 올리고, Level업의 성장 공급·성장 시간도 원래대로 따라온다(성장 공급은 GrowthPacer의 시간차대로).
    // - 더한 시간과 시간 고정은 판 통계의 더해진 시간(addedSeconds)에 세지 않는다.
    // 조작한 판은 테스트 도구가 테스트 표시를 붙인다(ContentTag).
    public static class BattleCheats
    {
        // 시간 고정을 켤 때 남겨 두는 최소 시간(초). 남은 시간이 거의 없을 때 고정하면 Step이 그만큼으로 잘려 판이 멈춘 듯 느려진다.
        private const float FrozenMinimumRemaining = 1f;

        // 종류·색 등급(0부터)·성질(없으면 null)·크기(1부터)를 정해 count마리를 출현 띠에 만든다. 만든 수를 돌려준다(동시 상한까지만).
        public static int Spawn(GameSession session, EnemyDefinition kind, int tier, EnemyTraitType? trait, int size, int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "1 이상이어야 한다.");

            World world = session.World;
            EnemyComposition composition = world.Stats.CompositionOf(kind);

            if (tier < 0 || tier >= kind.Tiers.Count)
                throw new ArgumentOutOfRangeException(nameof(tier), $"'{kind.Type}'의 색 등급은 0부터 {kind.Tiers.Count - 1}까지다.");

            if (size < SizeRule.Base || size > composition.Size)
                throw new ArgumentOutOfRangeException(nameof(size), $"이 판에서 '{kind.Type}'의 크기는 {SizeRule.Base}부터 {composition.Size}까지다.");

            return world.SpawnExact(kind, tier, TraitOf(kind, composition, trait), size, count);
        }

        // 살아 있는 적을 모두 치운다. 처치가 아니다(보상·처치 수 없음).
        public static void ClearEnemies(GameSession session) => session.World.ClearAliveEnemies();

        // 적 이동 배율(1이 원래 속도, 0이면 멈춤).
        public static float EnemyMoveScaleOf(GameSession session) => session.World.EnemyMoveScale;

        public static void SetEnemyMoveScale(GameSession session, float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0)
                throw new ArgumentOutOfRangeException(nameof(scale), "0 이상의 유한한 값이 필요하다.");

            session.World.EnemyMoveScale = scale;
        }

        // EXP를 더한다. Level은 다음 Step에서 오른다.
        public static void AddExp(GameSession session, long exp)
        {
            if (exp < 0)
                throw new ArgumentOutOfRangeException(nameof(exp), "0 이상이어야 한다.");

            session.World.Hq.AddExp(exp);
        }

        // 다음 Level에 닿도록 EXP를 채운다. 목표 Level(이정표)에 닿으면 판은 다음 Step에서 이정표로 끝난다.
        // 이미 목표 Level이거나 사다리 끝이면 false.
        public static bool RaiseLevel(GameSession session)
        {
            Hq hq = session.World.Hq;

            if (hq.ReachedMilestone || hq.NextLevelExp is not long next)
                return false;

            if (hq.GoalLevel != HqGrowthDefinition.NoGoal && hq.Level >= hq.GoalLevel)
                return false;

            hq.AddExp(Math.Max(0, next - hq.Exp));
            return true;
        }

        // 판을 이 Level에서 시작한 것처럼 EXP를 채운다. 판 시작 직후에 부른다(시나리오).
        // 목표 Level 이상은 거부한다 — 첫 Step에서 바로 이정표로 끝나 버린다.
        // 오르는 Level의 성장 공급은 시간차 없이 그 Step에 바로 나온다(시작 상태처럼). 그 뒤의 Level업은 원래대로 시간차를 둔다.
        public static void ReachLevel(GameSession session, int level)
        {
            Hq hq = session.World.Hq;

            if (hq.GoalLevel != HqGrowthDefinition.NoGoal && level >= hq.GoalLevel)
                throw new ArgumentOutOfRangeException(nameof(level), $"목표 Level({hq.GoalLevel}) 미만이어야 한다.");

            if (level <= hq.Level)
                return;

            long target = hq.Growth.ExpToReach(level)
                ?? throw new ArgumentOutOfRangeException(nameof(level), $"Level 사다리 밖이다: {level}.");

            hq.AddExp(Math.Max(0, target - hq.Exp));
            session.World.FlushGrowthOnNextStep = true;
        }

        // 예약된 성장 공급을 시간차 없이 지금 모두 낸다(게이지 연출도 끝낸다).
        public static void FlushGrowth(GameSession session) => session.World.FlushGrowthNow();

        // 제한 시간에 더한다. 판 통계의 더해진 시간에는 세지 않는다.
        public static void AddTime(GameSession session, float seconds) => session.ExtendTimeUncounted(seconds);

        public static bool IsTimeFrozen(GameSession session) => session.TimeFrozen;

        // 시간 고정: 켜 두면 남은 시간이 줄지 않는다. 켤 때 남은 시간이 1초보다 적으면 1초로 채운다.
        public static void SetTimeFrozen(GameSession session, bool frozen)
        {
            if (frozen && session.Remaining < FrozenMinimumRemaining)
                session.ExtendTimeUncounted(FrozenMinimumRemaining - session.Remaining);

            session.TimeFrozen = frozen;
        }

        private static EnemyTraitDefinition TraitOf(EnemyDefinition kind, EnemyComposition composition, EnemyTraitType? trait)
        {
            if (trait == null)
                return null;

            foreach (EnemyTraitDefinition candidate in composition.Traits)
            {
                if (candidate.Type == trait.Value)
                    return candidate;
            }

            throw new ArgumentException($"'{trait.Value}'는 '{kind.Type}'의 성질이 아니다.", nameof(trait));
        }
    }
}
#endif
