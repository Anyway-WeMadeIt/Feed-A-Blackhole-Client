using System;
using System.Collections.Generic;

namespace BlackHole.Net
{
    // 동기화 API의 본문(계약 3장, 5.4). 필드 이름이 곧 JSON 키라서 camelCase이고, 필드 순서가 곧 JSON 키 순서다.
    // JsonUtility는 null을 쓰지 못한다 — 문자열은 "", 목록은 []로 나간다.

    // ---------- 로그 payload ----------

    [Serializable]
    public sealed class NodePurchasePayloadDto
    {
        public string nodeId;
        public int rank;    // 구매 후 Rank
        public long cost;
    }

    [Serializable]
    public sealed class ResetPayloadDto
    {
    }

    [Serializable]
    public sealed class RunStartPayloadDto
    {
        public string runId;           // 소문자 UUID. battle-summary의 battleId와 같은 값
        public int seed;
        public int startGrowthStage;
        public long startGold;
        public int startLevel;
        public float timeLimitSeconds;
        public List<SupplyDto> startSupply = new();
    }

    [Serializable]
    public sealed class SupplyDto
    {
        public string enemyId;         // 소문자 종류 id(asteroid, planet, star, comet, galaxy)
        public int count;
    }

    [Serializable]
    public sealed class SettlementPayloadDto
    {
        public string runId;
        public float playedSeconds;
        public List<KillDto> kills = new();
        public int goldenCritCount;
        public long goldenCritBonusGold;
        public long earnedGold;
        public long exp;
        public int reachedLevel;
        public bool reachedMilestone;
        public int timeBonusCount;
        public long settledGold;
        public int nextGrowthStage;
    }

    [Serializable]
    public sealed class KillDto
    {
        public string enemyId;
        public int tier;               // 0부터
        public int size;               // 1부터
        public bool golden;
        public int count;              // 1 이상
    }

    // ---------- 응답 ----------

    [Serializable]
    public sealed class SyncResponseDto
    {
        public long appliedThroughSeq;
        public RejectedLogDto rejected = new();   // 거부가 없으면 서버는 null을 보내고, JsonUtility는 빈 객체로 읽는다
        public string contentVersion;
        public ProgressDto progress = new();

        // 거부가 있는가. rejected 객체가 아니라 code로 판단한다(계약 3.7).
        public bool HasRejection => rejected != null && !string.IsNullOrEmpty(rejected.code);
    }

    [Serializable]
    public sealed class RejectedLogDto
    {
        public long seq;
        public string type;            // 형식 오류로 알 수 없으면 ""
        public string code;            // RejectCodes. 거부가 없으면 ""
        public string message;         // 디버깅용
    }

    [Serializable]
    public sealed class ProgressDto
    {
        public long lastAppliedSeq;
        public long gold;
        public int growthStage;
        public List<ProgressNodeDto> nodes = new();   // 처음 산 순서
        public string updatedAt;
    }

    [Serializable]
    public sealed class ProgressNodeDto
    {
        public string nodeId;
        public int rank;
    }
}
