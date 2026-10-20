using System;

namespace BlackHole.Core
{
    // 판 콘텐츠의 저작 형식. 검증 전 값이며 실행에 쓰지 않음
    // - 시간 관련 규칙.
    // - 스킬 설정 수치.
    // - 적 수치.
    // - 블랙홀 성장 수치.
    [Serializable]
    public sealed class ContentData
    {
        public BattleRulesData BattleRules;
        public BreakerData Breaker;
        public EnemyContentData Enemies = new(); // 적 종류, 출현 배치, 전투 시작 공급.
        public HqGrowthData Growth; // 블랙홀 성장: 성장도마다의 판 Level 표와 이정표.
    }
}
