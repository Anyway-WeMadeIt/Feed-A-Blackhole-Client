namespace BlackHole.Core
{
    internal enum RandomStream
    {
        Placement = 0,
        Tier = 1,
        Trait = 2,
        Critical = 3,
        Size = 4,
        PeriodicSpawn = 5,
        DeathEffect = 6,
        PeriodicSpawnPlacement = 7,
        Respawn = 8,
        TimeBonus = 9,
        Rain = 10,
        GoldenCrit = 11,
        Tone = 12,
    }

    internal sealed class BattleRandom
    {
        private uint _state;

        public BattleRandom(int seed, RandomStream stream)
        {
            _state = unchecked((uint)seed ^ ((uint)stream * 0x85EBCA6Bu));
        }

        public bool Roll(float chance)
        {
            if (chance <= 0)
                return false;

            return chance >= 1 || NextFloat() < chance;
        }

        public float NextFloat()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                uint z = _state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return (z >> 8) * (1f / 16777216f);
            }
        }
    }
}
