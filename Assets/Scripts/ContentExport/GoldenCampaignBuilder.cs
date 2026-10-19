using System;
using System.Collections.Generic;
using BlackHole.Net;

namespace BlackHole.ContentExport
{
    // 캠페인이 끝난 이유(계약 3.8a의 campaign.stopReason).
    public enum CampaignStopReason
    {
        // 마지막 이정표에 닿았고 클리어 뒤 판까지 마쳤다.
        Cleared,

        // 이정표에 닿지 못한 채 정해 둔 판 수가 이어졌다.
        Stalled,

        // 판 수 상한.
        BattleLimit,

        // 다음 판의 결산에서 Gold가 long을 넘을 것이다. 그 판은 로그에 넣지 않는다.
        Overflow,

        // 다음 판이 안전 상한까지 끝나지 않았다. 그 판은 로그에 넣지 않는다.
        Capped,
    }

    // 로그 하나와, 그 로그를 적용한 뒤의 진행도(계약 3.8a의 checkpoints). 값은 클라이언트 ProgressState에서 읽은 것이다.
    public sealed class GoldenCampaignEntry
    {
        public PendingLog Log { get; }
        public long GoldAfter { get; }
        public int GrowthStageAfter { get; }

        public GoldenCampaignEntry(PendingLog log, long goldAfter, int growthStageAfter)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            GoldAfter = goldAfter;
            GrowthStageAfter = growthStageAfter;
        }
    }

    // 캠페인의 범위(계약 3.8a의 campaign).
    public sealed class GoldenCampaignMeta
    {
        public int Battles { get; }
        public int MaxGrowthStage { get; }
        public int ReachedGrowthStage { get; }
        public int PostClearBattles { get; }
        public CampaignStopReason StopReason { get; }

        // 마지막 이정표에 닿았는가.
        public bool Cleared => ReachedGrowthStage >= MaxGrowthStage;

        public GoldenCampaignMeta(int battles, int maxGrowthStage, int reachedGrowthStage, int postClearBattles, CampaignStopReason stopReason)
        {
            if (battles < 0) throw new ArgumentOutOfRangeException(nameof(battles));
            if (maxGrowthStage < 0) throw new ArgumentOutOfRangeException(nameof(maxGrowthStage));
            if (reachedGrowthStage < 0 || reachedGrowthStage > maxGrowthStage) throw new ArgumentOutOfRangeException(nameof(reachedGrowthStage));
            if (postClearBattles < 0) throw new ArgumentOutOfRangeException(nameof(postClearBattles));

            Battles = battles;
            MaxGrowthStage = maxGrowthStage;
            ReachedGrowthStage = reachedGrowthStage;
            PostClearBattles = postClearBattles;
            StopReason = stopReason;
        }
    }

    // 캠페인이 끝났을 때의 진행도(계약 3.8a의 final). 서버의 최종 진행도가 이것과 완전히 같아야 한다.
    public sealed class GoldenFinalState
    {
        public long Gold { get; }
        public int GrowthStage { get; }

        // 산 노드(처음 산 순서)와 Rank.
        public IReadOnlyList<(string NodeId, int Rank)> Nodes { get; }

        public GoldenFinalState(long gold, int growthStage, IReadOnlyList<(string NodeId, int Rank)> nodes)
        {
            Gold = gold;
            GrowthStage = growthStage;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }
    }

    // 결산의 시간 여유(계약 3.8a의 margins.minTimeSlack): 서버 S8 상한 − playedSeconds. 음수면 상한을 넘은 것이다.
    public sealed class GoldenTimeSlack
    {
        public long Seq { get; }
        public double Seconds { get; }

        public GoldenTimeSlack(long seq, double seconds)
        {
            Seq = seq;
            Seconds = seconds;
        }
    }

    // 골든 캠페인 로그 파일(계약 3.8a, W4-2a)의 글자를 만든다. 판을 돌리는 일은 하지 않는다 — 돌린 결과를 받아 쓴다.
    // 로그는 PendingLog.Json을 **글자 그대로** 넣는다(서버가 읽는 실제 요청 형식이고, 다시 직렬화하면 float 서식이 바뀔 수 있다).
    // 같은 입력이면 언제나 같은 글자다.
    public static class GoldenCampaignBuilder
    {
        public const int FormatVersion = 1;

        public static string Build(
            string contentVersion,
            GoldenCampaignMeta meta,
            IReadOnlyList<GoldenCampaignEntry> entries,
            GoldenFinalState final,
            GoldenTimeSlack minTimeSlack)
        {
            if (string.IsNullOrEmpty(contentVersion)) throw new ArgumentException("콘텐츠 버전이 필요하다.", nameof(contentVersion));
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (final == null) throw new ArgumentNullException(nameof(final));

            if (entries.Count == 0)
                throw new ArgumentException("로그가 하나 이상 있어야 한다(처음은 RESET).", nameof(entries));

            var logs = new List<object>(entries.Count);
            var checkpoints = new List<object>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                GoldenCampaignEntry entry = entries[i];
                long expected = i + 1;

                if (entry.Log.Seq != expected)
                    throw new ArgumentException($"seq는 1부터 1씩 늘어야 한다. {i}번째 로그의 seq가 {entry.Log.Seq}이다(기대 {expected}).", nameof(entries));

                logs.Add(new JsonRaw(entry.Log.Json));
                checkpoints.Add(new JsonInline(new List<object> { entry.Log.Seq, entry.GoldAfter, entry.GrowthStageAfter }));
            }

            var nodes = new List<object>(final.Nodes.Count);

            foreach ((string nodeId, int rank) in final.Nodes)
                nodes.Add(new JsonInline(new JsonObject { { "nodeId", nodeId }, { "rank", rank } }));

            object slack = minTimeSlack == null
                ? null
                : new JsonInline(new JsonObject { { "seq", minTimeSlack.Seq }, { "seconds", (float)minTimeSlack.Seconds } });

            return PyJson.Write(new JsonObject
            {
                { "formatVersion", FormatVersion },
                { "contentVersion", contentVersion },
                {
                    "campaign", new JsonObject
                    {
                        { "battles", meta.Battles },
                        { "maxGrowthStage", meta.MaxGrowthStage },
                        { "reachedGrowthStage", meta.ReachedGrowthStage },
                        { "cleared", meta.Cleared },
                        { "postClearBattles", meta.PostClearBattles },
                        { "stopReason", StopReasonText(meta.StopReason) },
                    }
                },
                { "logs", logs },
                { "checkpoints", checkpoints },
                {
                    "final", new JsonObject
                    {
                        { "gold", final.Gold },
                        { "growthStage", final.GrowthStage },
                        { "nodes", nodes },
                    }
                },
                { "margins", new JsonObject { { "minTimeSlack", slack } } },
            }) + "\n";
        }

        // 파일에 쓰는 stopReason 글자(계약 3.8a).
        public static string StopReasonText(CampaignStopReason reason)
        {
            switch (reason)
            {
                case CampaignStopReason.Cleared: return "cleared";
                case CampaignStopReason.Stalled: return "stalled";
                case CampaignStopReason.BattleLimit: return "battleLimit";
                case CampaignStopReason.Overflow: return "overflow";
                case CampaignStopReason.Capped: return "capped";
                default: throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }
    }
}
