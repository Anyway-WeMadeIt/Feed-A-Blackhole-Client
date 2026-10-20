using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 판을 만들 때 정해진 시작 값(계약 5.3). 판 시작 로그(RUN_START)에 싣는다.
    // GameSession에 연결하는 것(GameSession.StartInfo)은 W3-2에서 한다.
    public sealed class RunStartInfo
    {
        public int Seed { get; }
        public int GrowthStage { get; }

        // 블랙홀 시작 Level.
        public int StartLevel { get; }

        // 노드가 반영된 시작 제한 시간(초). 판 중에 늘어난 시간은 들지 않는다.
        public float TimeLimitSeconds { get; }

        // 시작 공급(변환 반영, 처음 나온 종류 순서, 0마리 종류 없음).
        public IReadOnlyList<SupplyRequest> StartSupply { get; }

        public RunStartInfo(int seed, int growthStage, int startLevel, float timeLimitSeconds,
            IReadOnlyList<SupplyRequest> startSupply)
        {
            Seed = seed;
            GrowthStage = growthStage;
            StartLevel = startLevel;
            TimeLimitSeconds = timeLimitSeconds;
            StartSupply = startSupply ?? throw new ArgumentNullException(nameof(startSupply));
        }
    }
}
