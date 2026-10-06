using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 아래 값은 모두 [임시]
    // 블랙홀 성장 설정 에셋. 값은 이 에셋을 Inspector에서 직접 고친다.
    [CreateAssetMenu(fileName = "HqGrowthSetup", menuName = "BlackHole/Hq Growth Setup")]
    public sealed class HqGrowthSetup : ScriptableObject
    {
        [Serializable]
        public struct Milestone
        {
            [Tooltip("판이 이 Level에 닿으면 판이 끝나고 성장도가 1 오른다. 1 이상, Level 사다리 안. 앞 이정표보다 커야 한다.")]
            public int level;
            [Tooltip("결산이 잔액을 이 값까지 채운다(차액 지급, 그 판에서 번 Gold는 버린다). 앞 이정표보다 커야 한다.")]
            public long targetGold;
            [Tooltip("이 이정표에 닿은 뒤의 전장 배율(1 이상, 앞 이정표 이상). 카메라 크기와 적 출현 띠가 이 배율로 넓어진다. 원작 실측: 1.68, 2.27, 2.83.")]
            public float fieldScale;
        }

        [Header("Level 사다리 (줄 번호 + 1 = Level, 누적 EXP)")]
        [Tooltip("Level 1, 2, …에 닿는 누적 EXP. 앞 줄보다 커야 한다. 사다리 끝에서는 EXP만 쌓인다.")]
        [SerializeField] private List<long> levelExp = new List<long>();

        [Header("이정표 (Level이 커지는 순서)")]
        [SerializeField] private List<Milestone> milestones = new List<Milestone>();

        public HqGrowthData ToData()
        {
            var data = new HqGrowthData
            {
                LevelExp = levelExp != null ? new List<long>(levelExp) : new List<long>(),
            };

            foreach (Milestone milestone in milestones)
                data.Milestones.Add(new HqMilestoneData { Level = milestone.level, TargetGold = milestone.targetGold, FieldScale = milestone.fieldScale });

            return data;
        }
    }
}
