using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 진행 상태(ProgressState)의 저장 형식. 판 진행·설정·콘텐츠는 담지 않는다.
    // 검사 전 값이다 — 불러올 때 지금 콘텐츠로 검사한다.
    [Serializable]
    public sealed class ProgressSaveData
    {
        // 지금 쓰는 형식 버전. 형식을 바꾸면 올리고, 옛 버전을 읽는 길을 둔다.
        public const int CurrentFormatVersion = 2;

        // 읽을 수 있는 가장 옛 형식 버전. 1은 동기화 상태(Sync)가 없는 저장이다.
        public const int OldestReadableFormatVersion = 1;

        public int FormatVersion;
        public long Gold;
        public int GrowthStage;
        // 노드마다 산 Rank(처음 산 순서). 사지 않은 노드는 적지 않는다.
        public List<NodeRankSaveData> Nodes = new();
        // 마지막 저장 시각(UTC, ISO 8601).
        public string SavedAtUtc;
        // 서버 동기화 상태(저장 포맷 2). 포맷 1 파일에는 없고, 그때는 처음 상태다.
        public SyncSaveData Sync = new();
    }
}
