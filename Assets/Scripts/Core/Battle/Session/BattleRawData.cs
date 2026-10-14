using System.Collections.Generic;

namespace BlackHole.Core
{
    // 끝난 전투 한 판의 전투 결과 스냅샷:
    // 판을 정리한 뒤에도 남는 유일한 기록.
    public sealed class BattleRawData
    {
        public int Seed { get; }
        public float PlayedSeconds { get; }

        // 종류별 처치 수(처음 처치한 순서)
        public IReadOnlyList<EnemyKillCount> Kills { get; }

        public int TotalKills { get; }

        // 칸(종류, 색 등급, 크기, 황금 여부)별 처치 수(처음 처치한 순서). 결산 로그(SETTLEMENT)의 kills.
        // 칸은 중복되지 않고 처치 수는 1 이상이다. 합계는 TotalKills와 같다.
        public IReadOnlyList<KillBucketCount> KillBuckets { get; }

        // 황금 치명타 횟수와 보너스 Gold 합계. 보너스는 EarnedGold에 이미 들어 있다.
        // EarnedGold == Σ(칸 처치 수 × 칸 Gold) + GoldenCritBonusGold.
        public int GoldenCritCount { get; }
        public long GoldenCritBonusGold { get; }

        // 시간 추가 성공 수
        public int TimeBonusCount { get; }

        // 이 판에서 번 Gold. 사망 순간마다 그 적의 Gold가 더해진 합계다. 이정표로 끝나지 않았으면 결산이 진행 상태에 더한 값(SettledGold)과 같다.
        public long EarnedGold { get; }

        // 이 판이 끝났을 때 블랙홀의 Level과 누적 EXP(시작 Level에 닿는 EXP부터 센다). 결산 뒤 버린다 — 다음 판은 성장도의 시작 Level에서 시작한다.
        public int ReachedLevel { get; }

        public long Exp { get; }

        // 이 판의 성장도와 결산 뒤의 성장도(이정표에 닿았으면 +1).
        public int Stage { get; }

        public int NextGrowthStage { get; }

        // 이 판에서 닿은 이정표. 없으면 null이다. 있으면 판은 그 Step에서 끝났다.
        public HqMilestone Milestone { get; }

        public bool ReachedMilestone => Milestone != null;

        // 결산이 진행 상태에 더한 Gold: 이정표로 끝났으면 목표 잔액까지의 차액(번 Gold는 버린다), 아니면 번 Gold(EarnedGold).
        public long SettledGold { get; }

        // 스킬별 피해와 수집 통계.
        public BattleStats Stats { get; }

        internal BattleRawData(
            int seed,
            float playedSeconds,
            IReadOnlyList<EnemyKillCount> kills,
            IReadOnlyList<KillBucketCount> killBuckets,
            int goldenCritCount,
            long goldenCritBonusGold,
            int timeBonusCount,
            long earnedGold,
            int reachedLevel,
            long exp,
            int stage,
            int nextStage,
            HqMilestone milestone,
            long settledGold,
            BattleStats stats)
        {
            Seed = seed;
            PlayedSeconds = playedSeconds;
            Kills = kills;
            KillBuckets = killBuckets;
            GoldenCritCount = goldenCritCount;
            GoldenCritBonusGold = goldenCritBonusGold;
            TimeBonusCount = timeBonusCount;
            EarnedGold = earnedGold;
            ReachedLevel = reachedLevel;
            Exp = exp;
            Stage = stage;
            NextGrowthStage = nextStage;
            Milestone = milestone;
            SettledGold = settledGold;
            Stats = stats;

            foreach (EnemyKillCount kill in kills)
                TotalKills += kill.Count;
        }
    }
}
