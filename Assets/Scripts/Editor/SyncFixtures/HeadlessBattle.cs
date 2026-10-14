using System;
using BlackHole.Core;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 판 하나를 봇으로 끝까지 돌리고 결산한다. 실제 게임(BattleSystem)과 같은 순서다:
    // 판 만들기(GameSessionFactory) → 시작(Begin) → 매 프레임 [봇 조준 → SetAimPoint → Advance] → 끝나면 종료 → 남은 적 치우기 → 기록 → 결산(Settle).
    // 판 시작 연출(카메라가 넓어지는 동안)은 판 시간이 흐르지 않으므로 넣지 않는다.
    //
    // 서버 동기화 픽스처(기대값 표, 골든 로그)와 칸별 처치 집계(W3-1)의 검증이 판을 헤드리스로 돌리는 데 쓴다.
    // 삭제된 시뮬레이션 도구의 BattleRunner에서 필요한 부분만 옮겼다(분석용 기록은 뺐다).
    internal static class HeadlessBattle
    {
        // 실제 게임의 한 프레임(60fps). 판은 프레임마다 한 번 진행한다.
        public const float DefaultFrameSeconds = 1f / 60;

        // 판 하나의 안전 상한(판 시간, 초). 판은 제한 시간이나 이정표로 끝나므로 보통 닿지 않는다.
        // 시간 추가(처치·성장)가 흐르는 시간보다 빠르면 판이 끝나지 않는다(마지막 이정표 뒤 등). 그때 여기서 끊고 Capped에 남긴다.
        public const float SafetySeconds = 3600;

        // progress는 결산까지 반영된다(Gold·성장도). upgrades는 이 판의 노드 수치(NodePurchase.StatsFor).
        public static HeadlessBattleResult Run(
            GameContent content,
            ProgressState progress,
            UpgradeStatValues upgrades,
            NearestEnemyBot bot,
            int seed,
            float frameSeconds = DefaultFrameSeconds,
            float maxSeconds = SafetySeconds) =>
            RunDetailed(content, progress, upgrades, bot, seed, frameSeconds, maxSeconds).Record;

        // Run과 같지만 판의 원본 기록(BattleRawData)과 판이 끝난 World(Hq 포함)도 돌려준다.
        // 결산 로그(SETTLEMENT)의 값이 판과 맞는지 검사하는 테스트가 쓴다.
        public static BattleRun RunDetailed(
            GameContent content,
            ProgressState progress,
            UpgradeStatValues upgrades,
            IAimBot bot,
            int seed,
            float frameSeconds = DefaultFrameSeconds,
            float maxSeconds = SafetySeconds)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            if (bot == null) throw new ArgumentNullException(nameof(bot));

            if (float.IsNaN(frameSeconds) || frameSeconds <= 0 || frameSeconds > 1)
                throw new ArgumentOutOfRangeException(nameof(frameSeconds), "0보다 크고 1 이하인 초가 필요하다.");

            if (float.IsNaN(maxSeconds) || maxSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSeconds), "양의 초가 필요하다.");

            long goldBefore = progress.Gold;
            GameSession session = GameSessionFactory.Create(content, progress, seed, upgrades);
            session.Begin();
            bot.Begin();

            while (!session.IsEnded && session.Elapsed < maxSeconds)
            {
                session.SetAimPoint(bot.Aim(session.World, frameSeconds));
                session.Advance(frameSeconds);
            }

            bool capped = !session.IsEnded;
            session.End();
            session.ClearRemainingEnemies();
            BattleRawData raw = session.CreateRawData();
            session.Settle();

            Hq hq = session.World.Hq;
            var record = new BattleRecord
            {
                Seed = seed,
                Bot = bot.Name,
                Stage = raw.Stage,
                NextStage = raw.NextGrowthStage,
                StartLevel = hq.StartLevel,
                GoalLevel = hq.GoalLevel,
                ReachedLevel = raw.ReachedLevel,
                ReachedMilestone = raw.ReachedMilestone,
                Capped = capped,
                PlayedSeconds = raw.PlayedSeconds,
                EarnedGold = raw.EarnedGold,
                SettledGold = raw.SettledGold,
                GoldBefore = goldBefore,
                GoldAfter = progress.Gold,
                TotalKills = raw.TotalKills,
                BreakerDamage = raw.Stats.BreakerDamage,
                ElectricAsteroidDamage = raw.Stats.ElectricAsteroidDamage,
                ElectricStarDamage = raw.Stats.ElectricStarDamage,
                LaserDamage = raw.Stats.LaserDamage,
                SupernovaDamage = raw.Stats.SupernovaDamage,
                BreakerTicks = raw.Stats.BreakerTicks,
                CollectedMoons = raw.Stats.CollectedMoons,
                CollectedComets = raw.Stats.CollectedComets,
                AddedSeconds = raw.Stats.AddedSeconds,
            };
            
            return new HeadlessBattleResult(seed, goldBefore, progress.Gold, session.StartInfo, raw, capped);
        }
    }

    // 헤드리스로 돌린 판의 결과. 로그 생성기(SyncLogFactory)가 StartInfo와 Raw를 그대로 쓴다.
    internal sealed class HeadlessBattleResult
    {
        public int Seed { get; }

        // 판을 시작할 때와 결산한 뒤의 진행도 Gold. 판 시작 로그의 startGold가 GoldBefore다.
        public long GoldBefore { get; }
        public long GoldAfter { get; }

        // 판을 만들 때 정해진 시작 값(판 시작 로그에 싣는다).
        public RunStartInfo StartInfo { get; }

        // 끝난 판의 전투 결과(결산 로그에 싣는다).
        public BattleRawData Raw { get; }

        // 판이 끝나지 않아 안전 상한에서 끊었는가.
        public bool Capped { get; }

        public HeadlessBattleResult(int seed, long goldBefore, long goldAfter, RunStartInfo startInfo, BattleRawData raw, bool capped)
        {
            Seed = seed;
            GoldBefore = goldBefore;
            GoldAfter = goldAfter;
            StartInfo = startInfo;
            Raw = raw;
            Capped = capped;
        }
    }
}
