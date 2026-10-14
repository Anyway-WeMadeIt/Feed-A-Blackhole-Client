using System.Collections.Generic;

namespace BlackHole.Core
{
    // 검사를 통과한 저장. 게임에 쓸 값은 지금 콘텐츠에 맞춘 것이다(ProgressSave).
    public sealed class SavedProgress
    {
        public long Gold => Data.Gold;
        public int GrowthStage { get; }
        // 게임에 쓸 Rank(처음 산 순서): 노드 콘텐츠에 있는 노드만, MaxRank까지.
        public IReadOnlyList<(string NodeId, int Rank)> Ranks { get; }
        // 마지막 저장 시각(UTC, ISO 8601).
        public string SavedAtUtc => Data.SavedAtUtc;

        // 불러온 동기화 상태(포맷 1이면 처음 상태).
        public SyncSaveData Sync => Data.Sync;

        // 불러온 원래 값. 다음 저장 때 게임에 쓰지 않은 값을 되돌려 적는다.
        internal ProgressSaveData Data { get; }

        internal SavedProgress(ProgressSaveData data, int growthStage, List<(string NodeId, int Rank)> ranks)
        {
            Data = data;
            GrowthStage = growthStage;
            Ranks = ranks.AsReadOnly();
        }
    }
}
