using System.Collections.Generic;

namespace BlackHole.Core
{
    // 한 판의 통계를 Step마다 모은다: 이번 Step의 피격·사망 기록을 더한다(World.Step).
    // 기록은 다음 Step이 시작될 때 비워지므로, 그 Step에 피해가 끝난 뒤(사망 효과 처리 뒤) 한 번 센다.
    internal sealed class BattleStatsCounter
    {
        private double _breakerDamage;
        private double _breakerCriticalDamage;
        private double _electricAsteroidDamage;
        private double _electricStarDamage;
        private double _laserDamage;
        private double _supernovaDamage;
        private long _goldenAsteroidGold;
        private int _collectedMoons;
        private int _collectedComets;

        internal void Count(IReadOnlyList<HitRecord> hits, IReadOnlyList<DeathRecord> deaths)
        {
            for (int i = 0; i < hits.Count; i++)
                CountHit(hits[i]);

            for (int i = 0; i < deaths.Count; i++)
                CountDeath(deaths[i]);
        }

        // Tick 수와 더해진 시간은 이것을 가진 쪽(Breaker, 판의 제한 시간)이 센다.
        internal BattleStats Snapshot(int breakerTicks, float addedSeconds)
        {
            return new BattleStats(
                _breakerDamage,
                _breakerCriticalDamage,
                breakerTicks,
                _electricAsteroidDamage,
                _electricStarDamage,
                _laserDamage,
                _supernovaDamage,
                _goldenAsteroidGold,
                _collectedMoons,
                _collectedComets,
                addedSeconds);
        }

        private void CountHit(HitRecord hit)
        {
            switch (hit.Source)
            {
                case DamageSource.Breaker:
                    _breakerDamage += hit.Amount;

                    if (hit.IsCritical)
                        _breakerCriticalDamage += hit.Amount;
                    break;
                // 전기 성질은 소행성(연쇄 번개)과 별(전기 별 번개)에 붙고 효과가 따로다.
                case DamageSource.ChainLightning:
                    _electricAsteroidDamage += hit.Amount;
                    break;
                case DamageSource.StarLightning:
                    _electricStarDamage += hit.Amount;
                    break;
                case DamageSource.LaserBurst:
                    _laserDamage += hit.Amount;
                    break;
                case DamageSource.Explosion:
                    _supernovaDamage += hit.Amount;
                    break;
            }
        }

        // 황금 성질은 소행성에만 붙는다. 달·혜성은 죽으면 언제나 Breaker 버프가 된다(DeathEffects).
        private void CountDeath(DeathRecord death)
        {
            switch (death.TraitType)
            {
                case EnemyTraitType.Golden:
                    _goldenAsteroidGold = checked(_goldenAsteroidGold + death.Reward.TotalGold);
                    break;
                case EnemyTraitType.Moon:
                    _collectedMoons++;
                    break;
                case EnemyTraitType.Comet:
                    _collectedComets++;
                    break;
            }
        }
    }
}
