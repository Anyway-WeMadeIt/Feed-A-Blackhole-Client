using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class NodeDefinition
    {
        public string Id { get; }
        public IReadOnlyList<NodeRankDefinition> Ranks { get; }

        public int MaxRank => Ranks.Count;

        internal NodeDefinition(
            string id,
            IReadOnlyList<NodeRankDefinition> ranks)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Ranks = ranks ?? throw new ArgumentNullException(nameof(ranks));
        }

        public NodeRankDefinition RankAt(int rank)
        {
            if (rank < 1 || rank > MaxRank)
                throw new ArgumentOutOfRangeException(nameof(rank));

            return Ranks[rank - 1];
        }
    }
}
