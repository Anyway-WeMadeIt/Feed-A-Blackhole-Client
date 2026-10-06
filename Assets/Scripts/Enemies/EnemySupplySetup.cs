using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 공급·배치 설정 에셋: HQ를 둘러싼 출현 띠, 픽업(혜성)의 출현 띠(일반 띠 바깥 반지름 기준 오프셋), 전체 개체 수 상한, 전투 시작 공급(SYSTEM_CATALOG S08).
    // 공급은 종류 에셋을 직접 가리킨다 — ID 문자열을 치지 않는다. 가리키는 종류는 적 종류 목록(EnemyCatalog)에 있어야 한다.
    // 전체 개체 수 상한은 성능 예산이다: 공급 계기와 노드가 무엇을 약속하든 판의 적 수는 이 수를 넘지 않는다.
    // 공급 수 노드를 모두 산 전투 시작 공급이 이 수를 넘으면 콘텐츠 로드가 실패한다.
    // 값은 이 에셋을 Inspector에서 직접 고친다.
    [CreateAssetMenu(fileName = "EnemySupplySetup", menuName = "BlackHole/Enemy Supply Setup")]
    public sealed class EnemySupplySetup : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyKind kind;
            public int count;
        }

        [Header("출현 배치: HQ(0,0)로부터의 거리 띠")]
        [SerializeField] private float minDistance = 2;
        [SerializeField] private float maxDistance = 4;

        [Header("픽업(혜성) 출현 배치: 위 띠의 바깥 반지름(maxDistance) 기준 오프셋. 음수는 안쪽")]
        [Tooltip("픽업 띠의 안쪽 = 일반 띠 바깥 반지름 + 이 값. 일반 띠가 바뀌어도 따라간다.")]
        [SerializeField] private float pickupInnerOffset = -1;
        [Tooltip("픽업 띠의 바깥 = 일반 띠 바깥 반지름 + 이 값. 안쪽 오프셋 이상이어야 한다.")]
        [SerializeField] private float pickupOuterOffset = 0.5f;

        [Header("전체 개체 수 상한 (성능 예산)")]
        [Tooltip("한 판에 동시에 살아 있을 수 있는 적의 최대 수. 넘는 생성 요청은 버린다. 성능 기준 상황을 측정해 정한다.")]
        [SerializeField] private int maxAliveEnemies = 200;

        [Header("전투 시작 공급 (요청 순서대로 나온다)")]
        [SerializeField] private List<Entry> startSupply = new List<Entry>();

        // Core 저작 형식에 출현 배치, 전체 개체 수 상한, 전투 시작 공급을 채운다. 검증은 EnemyContentLoader가 한다.
        // 종류 칸이 비어 있는 공급은 ID 없는 공급이 되어 로더가 경로와 함께 보고한다.
        public void WriteTo(EnemyContentData data)
        {
            data.EnemyPlacement = new EnemyPlacementData { MinDistance = minDistance, MaxDistance = maxDistance };
            data.PickupPlacement = new PickupPlacementData { InnerOffset = pickupInnerOffset, OuterOffset = pickupOuterOffset };
            data.MaxAliveEnemies = maxAliveEnemies;

            foreach (Entry entry in startSupply)
            {
                data.StartSupply.Add(new SupplyData
                {
                    Enemy = entry.kind != null ? entry.kind.Id : null,
                    Count = entry.count,
                });
            }
        }
    }
}
