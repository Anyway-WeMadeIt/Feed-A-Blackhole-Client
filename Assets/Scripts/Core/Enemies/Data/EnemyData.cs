using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    [Serializable]
    public sealed class EnemyData
    {
        public EnemyType Type;
        public float MoveSpeed;
        // 크기 1의 반지름. 모든 색이 같다.
        public float Radius;
        // 크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 픽업은 크기가 없어 쓰지 않는다.
        public float RadiusStep;
        // 색 등급 표(색마다의 베이스 HP·Gold·EXP). 공급되는 종류는 6색, 픽업은 한 줄이다.
        // 어떤 색이 나오는지는 질량(노드 asteroid.massScale 등), 어떤 크기가 나오는지는 크기(노드 asteroid.size 등)가 정한다.
        public List<EnemyTierData> Tiers = new List<EnemyTierData>();
        // 붙을 수 있는 특수 성질(성질 종류마다 하나). 생성 확률은 노드(electricAsteroid.spawnChance 등)가 정하고 기본 0%다.
        // 픽업은 정확히 하나이고 언제나 붙는다.
        public List<EnemyTraitData> Traits = new List<EnemyTraitData>();
        // 변환 대상 종류(소행성 → 행성 → 별 → 은하). null이면 변환하지 않는다. 변환 수는 노드(asteroid.toPlanet·planet.toStar·star.toGalaxy)만 정한다.
        public EnemyType? UpgradesTo;
        // 주기 출현의 판정 주기(초). 0이면 공급되는 보통 종류다(0보다 크면 픽업). 등장 확률은 노드(comet.spawnChance)가 정한다.
        public float SpawnPeriod;
        // 혜성 비 한 번에 나오는 수. 0이면 혜성 비가 없다.
        public int RainCount;
        // 파괴 때 시간 추가가 성공하면 더하는 초. 0이면 판 설정(BattleRules.killTimeBonus)을 쓴다.
        public float KillTimeBonus;
        // 재생성 세대 감쇠(0~1). 재생성으로 나온 적이 죽을 때 재생성 확률에 세대마다 이 값을 곱한다. 0이면 1세대까지만(사슬 금지), 1이면 감쇠 없음.
        public float RespawnDecay;
        // 혜성 비 쿨타임(초). 혜성 비가 한 번 내리면 이 시간 동안은 혜성 비 판정을 하지 않는다(하나씩은 나온다). 0이면 없음.
        public float RainCooldown;
        // 판 시작 뒤 이 시간(초)까지는 혜성 비가 내리지 않는다. 0이면 없음.
        public float RainDelay;
    }
}
