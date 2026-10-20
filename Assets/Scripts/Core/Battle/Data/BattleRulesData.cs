using System;

namespace BlackHole.Core
{
    // 판 규칙(BattleRules 에셋)의 저작 형식. ContentLoader가 TimeLimitDefinition으로 검증한다.
    [Serializable]
    public sealed class BattleRulesData
    {
        public float TimeLimit; // 한 판 제한 시간
        public float KillTimeBonus; // 적이 파괴될 때 시간 추가
        public float MaxKillTimeBonus; // 한 판에 파괴로 더할 수 있는 시간의 합(초). 0이면 제한 없음.
        public float KillTimeCooldown; // 시간 추가가 성공한 뒤 다음 판정까지 기다리는 초. 0이면 쿨타임 없음.
    }
}
