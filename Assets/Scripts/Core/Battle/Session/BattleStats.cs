namespace BlackHole.Core
{
    // 끝난 판의 스킬별 피해와 수집 통계(결산창의 오른쪽 칸, 분석 기록).
    // 피해는 피격 기록(HitRecord)의 피해량 합이다 — 적의 남은 체력을 넘은 몫도 센다.
    // 종류별 처치 수는 BattleRawData.Kills에 있다.
    public sealed class BattleStats
    {
        // Breaker가 준 피해 전부(치명타 포함).
        public double BreakerDamage { get; }

        // Breaker 피해 중 치명타 Tick의 몫. BreakerDamage에 들어 있다.
        public double BreakerCriticalDamage { get; }

        // Breaker의 Tick 수(원작 "브레이커 클릭 수"). 맞힌 적이 없는 Tick도 센다.
        public int BreakerTicks { get; }

        // 전기 소행성·전기 별이 죽어 낸 번개의 피해.
        public double ElectricAsteroidDamage { get; }

        public double ElectricStarDamage { get; }

        // 레이저 별의 레이저 피해.
        public double LaserDamage { get; }

        // 초신성 별의 폭발 피해.
        public double SupernovaDamage { get; }

        // 황금 소행성의 처치 보상 합(황금 치명타 보너스 포함). 판이 번 Gold(EarnedGold)에 들어 있다.
        public long GoldenAsteroidGold { get; }

        // 처치해 Breaker 버프로 받은 달·혜성 수.
        public int CollectedMoons { get; }

        public int CollectedComets { get; }

        // 이 판의 제한 시간에 더해진 초(블랙홀 성장 + 파괴 때 시간 추가).
        public float AddedSeconds { get; }

        internal BattleStats(
            double breakerDamage,
            double breakerCriticalDamage,
            int breakerTicks,
            double electricAsteroidDamage,
            double electricStarDamage,
            double laserDamage,
            double supernovaDamage,
            long goldenAsteroidGold,
            int collectedMoons,
            int collectedComets,
            float addedSeconds)
        {
            BreakerDamage = breakerDamage;
            BreakerCriticalDamage = breakerCriticalDamage;
            BreakerTicks = breakerTicks;
            ElectricAsteroidDamage = electricAsteroidDamage;
            ElectricStarDamage = electricStarDamage;
            LaserDamage = laserDamage;
            SupernovaDamage = supernovaDamage;
            GoldenAsteroidGold = goldenAsteroidGold;
            CollectedMoons = collectedMoons;
            CollectedComets = collectedComets;
            AddedSeconds = addedSeconds;
        }
    }
}
