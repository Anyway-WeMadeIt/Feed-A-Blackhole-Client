using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 블랙홀 성장: 모든 판이 함께 쓰는 Level 사다리와 이정표. 성장 효과(Level업 시간 등)는 노드가 정한다.
    [Serializable]
    public sealed class HqGrowthData
    {
        // LevelExp[i]는 Level (i + 1)에 닿는 누적 EXP(앞 줄보다 커야 한다).
        public List<long> LevelExp = new();
        // 이정표(Level이 커지는 순서). 판이 그 Level에 닿으면 판이 끝나고 결산이 잔액을 목표 잔액까지 채운다.
        public List<HqMilestoneData> Milestones = new();

        // 은하가 나오기 시작하는 성장도(0 = 은하 없음). 이 성장도부터 판마다 은하 1개가 시작 공급에 더해지고,
        // 색은 이정표마다 하나씩 올라간다(성장도 − GalaxyStage = 색 등급). 블랙홀 구역의 진행 단위다.
        public int GalaxyStage;
    }
}
