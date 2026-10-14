using System;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 판 하나를 봇으로 끝까지 돌리고 결산한다. 실제 게임(BattleSystem)과 같은 순서다:
    // 판 만들기(GameSessionFactory) → 시작(Begin) → 매 프레임 [봇 조준 → SetAimPoint → Advance] → 끝나면 종료 → 남은 적 치우기 → 기록 → 결산(Settle).
    // 판 시작 연출(카메라가 넓어지는 동안)은 판 시간이 흐르지 않으므로 넣지 않는다.
    public static class BattleRunner
    {
        // 실제 게임의 한 프레임(60fps). 판은 프레임마다 한 번 진행한다.
        public const float DefaultFrameSeconds = 1f / 60;

        // 판 하나의 안전 상한(판 시간, 초). 판은 제한 시간이나 이정표로 끝나므로 보통 닿지 않는다.
        // 시간 추가(처치·성장)가 흐르는 시간보다 빠르면 판이 끝나지 않는다(마지막 이정표 뒤 등). 그때 여기서 끊고 BattleRecord.Capped에 남긴다.
        public const float SafetySeconds = 3600;

        // progress는 결산까지 반영된다(Gold·성장도). upgrades는 이 판의 노드 수치(NodePurchase.StatsFor).
        public static BattleRecord Run(
            GameContent content,
            ProgressState progress,
            UpgradeStatValues upgrades,
            IAimBot bot,
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
            bot.Begin(seed);

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

            foreach (EnemyKillCount kill in raw.Kills)
                record.Kills[kill.Enemy.Type] = record.KillsOf(kill.Enemy.Type) + kill.Count;

            return new BattleRun(record, raw, session.World); //record;
        }
    }

    // RunDetailed의 결과: 요약 기록, 판의 원본 기록, 판이 끝난 World.
    public sealed class BattleRun
    {
        public BattleRecord Record { get; }
        public BattleRawData Raw { get; }

        // 결산과 남은 적 정리까지 끝난 World. Hq의 Exp·시작 Level은 판이 끝난 그대로다.
        public World World { get; }

        internal BattleRun(BattleRecord record, BattleRawData raw, World world)
        {
            Record = record;
            Raw = raw;
            World = world;
        }
    }
}
