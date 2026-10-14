using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using BlackHole.Net;

namespace BlackHole.Sync
{
    // 서버 진행도(ProgressDto)와 로컬 진행도의 비교·변환.
    public static class ServerProgress
    {
        // 서버에 아무 기록도 없는가: 반영한 로그가 없고, 진행도가 처음 상태다.
        public static bool IsEmpty(ProgressDto server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            return server.lastAppliedSeq == 0 && server.gold == 0 && server.growthStage == 0
                && (server.nodes == null || server.nodes.Count == 0);
        }

        // 로컬 진행도(Gold, 성장도, 노드별 Rank)가 서버와 같은가. 노드의 순서는 보지 않는다.
        public static bool Matches(long gold, int growthStage, IEnumerable<(string NodeId, int Rank)> ranks, ProgressDto server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            if (gold != server.gold || growthStage != server.growthStage)
                return false;

            var expected = new Dictionary<string, int>(StringComparer.Ordinal);

            if (server.nodes != null)
            {
                foreach (ProgressNodeDto node in server.nodes)
                    expected[node.nodeId] = node.rank;
            }

            int count = 0;

            foreach ((string nodeId, int rank) in ranks)
            {
                count++;

                if (!expected.TryGetValue(nodeId, out int serverRank) || serverRank != rank)
                    return false;
            }

            return count == expected.Count;
        }

        // 진행 상태(게임 중)와 같은가.
        public static bool Matches(ProgressState progress, ProgressDto server)
        {
            var ranks = new List<(string NodeId, int Rank)>(progress.OwnedNodes.Count);

            foreach (string nodeId in progress.OwnedNodes)
                ranks.Add((nodeId, progress.RankOf(nodeId)));

            return Matches(progress.Gold, progress.GrowthStage, ranks, server);
        }

        // 서버 진행도를 저장 형식으로. 검사는 ProgressSave.Load가 한다.
        public static ProgressSaveData ToSaveData(ProgressDto server, DateTime utcNow, SyncSaveData sync)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            var data = new ProgressSaveData
            {
                FormatVersion = ProgressSaveData.CurrentFormatVersion,
                Gold = server.gold,
                GrowthStage = server.growthStage,
                SavedAtUtc = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                Sync = sync ?? new SyncSaveData(),
            };

            if (server.nodes != null)
            {
                foreach (ProgressNodeDto node in server.nodes)
                    data.Nodes.Add(new NodeRankSaveData { NodeId = node.nodeId, Rank = node.rank });
            }

            return data;
        }
    }
}
