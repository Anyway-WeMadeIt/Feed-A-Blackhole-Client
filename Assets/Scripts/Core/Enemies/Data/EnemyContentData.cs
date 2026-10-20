using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 적 콘텐츠의 저작 형식. 검증 전 값이며 실행에 쓰지 않는다 — 적 콘텐츠 로더만 읽는다.
    // 적 종류는 적 종류 목록 에셋(EnemyCatalog)이, 출현 배치·전투 시작 공급은 적 공급 설정 에셋(EnemySupplySetup)이 채운다.
    [Serializable]
    public sealed class EnemyContentData
    {
        public List<EnemyData> Enemies = new List<EnemyData>();
        // 출현 위치. 필수다.
        public EnemyPlacementData EnemyPlacement;
        // 주기 출현 종류(혜성)의 출현 띠(일반 띠 바깥 반지름 기준 오프셋). 필수다.
        public PeriodicSpawnPlacementData PeriodicSpawnPlacement;
        // 전투 시작 공급. 전투를 시작할 때(0초) 한 번 공급한다.
        public List<SupplyData> StartSupply = new List<SupplyData>();
    }
}
