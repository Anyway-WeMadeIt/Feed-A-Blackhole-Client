namespace BlackHole.Net
{
    // 로그 type 글자(계약 3.3).
    public static class LogTypes
    {
        public const string RunStart = "RUN_START";
        public const string Settlement = "SETTLEMENT";
        public const string NodePurchase = "NODE_PURCHASE";
        public const string Reset = "RESET";
    }

    // 로그 거부 코드 전체(계약 3.6). 응답의 rejected.code.
    public static class RejectCodes
    {
        public const string SeqGap = "SEQ_GAP";
        public const string MalformedLog = "MALFORMED_LOG";
        public const string ContentVersionMismatch = "CONTENT_VERSION_MISMATCH";

        public const string UnknownNode = "UNKNOWN_NODE";
        public const string MaxRankReached = "MAX_RANK_REACHED";
        public const string NodeHidden = "NODE_HIDDEN";
        public const string NotEnoughGold = "NOT_ENOUGH_GOLD";
        public const string RankMismatch = "RANK_MISMATCH";
        public const string CostMismatch = "COST_MISMATCH";

        public const string DuplicateRunId = "DUPLICATE_RUN_ID";

        public const string RunNotOpen = "RUN_NOT_OPEN";
        public const string SettlementInvalidKills = "SETTLEMENT_INVALID_KILLS";
        public const string SettlementExpMismatch = "SETTLEMENT_EXP_MISMATCH";
        public const string SettlementGoldMismatch = "SETTLEMENT_GOLD_MISMATCH";
        public const string SettlementTimeExceeded = "SETTLEMENT_TIME_EXCEEDED";
        public const string SettlementResultMismatch = "SETTLEMENT_RESULT_MISMATCH";
    }

    // 동기화 한도(계약 3.5).
    public static class SyncLimits
    {
        public const int MaxLogsPerRequest = 500;
        public const int MaxRequestBytes = 1024 * 1024;
    }
}
