using System;

namespace BlackHole.Core
{
    // 처치 보상 계산: 사망이 확정된 적 하나의 보상 내역(DeathReward)을 정한다.
    // - 기본 Gold: 적의 Gold.
    // - 황금 치명타: 황금 성질이면 치명타 확률로 기본 Gold × 치명타 Gold 배율(반올림)을 보너스로 더 준다(원작 "보너스 치명타 돈").
    internal sealed class DeathRewards
    {
        private readonly BattleRandom _goldenCritRandom;

        internal DeathRewards(BattleRandom goldenCritRandom)
        {
            _goldenCritRandom = goldenCritRandom;
        }

        internal DeathReward Resolve(Enemy dead)
        {
            long gold = dead.Stats.Gold;

            if (TryGetGolden(dead, out GoldenDefinition golden) && _goldenCritRandom.Roll(golden.CritChance))
            {
                long bonus = (long)Math.Round(gold * golden.CritRewardScale, MidpointRounding.AwayFromZero);
                return new DeathReward(gold, bonus, true);
            }

            return new DeathReward(gold, 0, false);
        }

        // 이 적의 성질이 황금이면 그 정의를 준다. 처치 보상(황금 치명타)과 처치 장부의 칸(KillBucket.Golden)이 이 판정 하나를 같이 쓴다.
        internal static bool TryGetGolden(Enemy enemy, out GoldenDefinition golden)
        {
            if (enemy.Trait?.Effect is GoldenDefinition found)
            {
                golden = found;
                return true;
            }

            golden = default;
            return false;
        }

        internal static bool IsGolden(Enemy enemy) => TryGetGolden(enemy, out _);
    }
}
