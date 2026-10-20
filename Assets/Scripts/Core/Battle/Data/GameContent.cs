using System;

namespace BlackHole.Core
{
    public sealed class GameContent
    {
        public TimeLimitDefinition TimeLimit { get; }
        public BreakerDefinition Breaker { get; }
        public EnemyContent Enemies { get; }
        public HqGrowthDefinition Growth { get; }

        public GameContent(
            TimeLimitDefinition timeLimit,
            BreakerDefinition breaker,
            EnemyContent enemies,
            HqGrowthDefinition growth)
        {
            TimeLimit = timeLimit;
            Breaker = breaker;
            Enemies = enemies;
            Growth = growth;
        }
    }
}
