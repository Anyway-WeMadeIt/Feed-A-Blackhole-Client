using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class EnemyData
    {
        public string Id;
        public float MoveSpeed;
        // 크기 1의 반지름. 모든 색이 같다.
        public float Radius;
        // 크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 픽업은 크기가 없어 쓰지 않는다.
        public float RadiusStep;
        // 색 등급 표(색마다의 베이스 HP·Gold·EXP). 공급되는 종류는 7색, 픽업은 한 줄이다.
        // 어떤 색이 나오는지는 질량(노드 enemy.<종류>.mass), 어떤 크기가 나오는지는 크기(노드 enemy.<종류>.size)가 정한다.
        public List<EnemyTierData> Tiers = new List<EnemyTierData>();
        // 붙을 수 있는 특수 성질(성질 ID 유일). 생성 확률은 노드(enemy.<종류>.trait.<성질>.chance)가 정하고 기본 0%다.
        // 픽업은 정확히 하나이고 언제나 붙는다.
        public List<EnemyTraitData> Traits = new List<EnemyTraitData>();
        // 변환 대상 종류의 ID(소행성 → 행성 → 별). 비어 있으면 변환하지 않는다. 비율은 노드(enemy.<종류>.upgrade)만 정한다.
        public string UpgradesTo;
        // 픽업의 등장 판정 주기(초). 0이면 공급되는 보통 종류다. 등장 확률은 노드(enemy.<종류>.chance)가 정한다.
        public float PickupPeriod;
    }
}
