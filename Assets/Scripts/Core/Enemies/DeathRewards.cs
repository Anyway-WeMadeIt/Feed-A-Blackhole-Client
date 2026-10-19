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
                return new DeathReward(gold, CritBonusOf(gold, golden.CritRewardScale), true);
            }

            return new DeathReward(gold, 0, false);
        }

        // 황금 치명타의 보너스 Gold: 기본 Gold × 치명타 Gold 배율(반올림, .5는 0에서 먼 쪽).
        // 곱셈은 float이다(서버도 같아야 한다, 계약 C28). 곱한 결과를 (float)로 한 번 더 받아 float 정밀도로 확정한다:
        // 그러지 않으면 런타임이 중간 결과를 더 높은 정밀도로 들고 있을 때(Unity 에디터의 Mono) float 정밀도(2^24)를 넘는 값이
        // IL2CPP 빌드·서버와 달라진다. 서버용 기대값 표(W4-1a)가 이 식을 그대로 부른다.
        internal static long CritBonusOf(long gold, float critRewardScale)
        {
            float product = (float)((float)gold * critRewardScale);
            return (long)Math.Round(product, MidpointRounding.AwayFromZero);
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
