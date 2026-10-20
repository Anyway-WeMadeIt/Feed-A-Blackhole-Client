using System;

namespace BlackHole.Core
{
    // 색 등급 한 줄: 크기 1의 베이스 수치. 크기 k는 셋 모두에 k를 곱한다(SizeRule).
    [Serializable]
    public sealed class EnemyTierData
    {
        public float MaxHealth;
        // 사망 때 판의 합계에 드는 Gold의 기본값. 0 이상. 황금 성질은 배율을 더 곱한다.
        public long Gold;
        // 사망 때 블랙홀에 드는 EXP. 0 이상.
        public long Exp;
    }
}
