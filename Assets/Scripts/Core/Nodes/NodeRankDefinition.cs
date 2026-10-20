using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class NodeRankDefinition
    {
        public int Rank { get; }
        public long Cost { get; }
        public IReadOnlyList<NodeEffect> Effects { get; }

        internal NodeRankDefinition(
            int rank,
            long cost,
            IReadOnlyList<NodeEffect> effects)
        {
            if (rank < 1)
                throw new ArgumentOutOfRangeException(nameof(rank));

            Rank = rank;
            Cost = cost;
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }
    }
}
