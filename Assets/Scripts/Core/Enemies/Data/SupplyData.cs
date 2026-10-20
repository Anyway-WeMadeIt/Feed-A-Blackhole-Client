using System;

namespace BlackHole.Core
{
    // 적 공급 한 건: 어떤 종류를 몇 마리.
    [Serializable]
    public sealed class SupplyData
    {
        // 공급 설정에서 종류 에셋이 비어 있으면 null이다(EnemyContentLoader가 진단한다).
        public EnemyType? Enemy;
        public int Count;
    }
}
