using System;
using System.Collections.Generic;

namespace Pets.Meta
{
    /// <summary>How many balls of each tier the run is carrying (design doc §12.1). Plain C#, held
    /// by <see cref="RunState"/>, so it serializes with the run and unit-tests without a scene.
    ///
    /// Design doc §12.1 has balls bought at the Shop, and there is no Shop yet — so a run currently
    /// starts with <see cref="GrantStartingStock"/> and that's all it will ever get. That's a
    /// deliberate stand-in to make catching playable now, not a decision that balls should be free:
    /// when the Shop lands, it buys into this same inventory and the starting stock shrinks or goes
    /// away.</summary>
    [Serializable]
    public sealed class BallInventory
    {
        private readonly Dictionary<BallTier, int> counts = new Dictionary<BallTier, int>();

        /// <summary>What a run is handed at the start, standing in for the Shop. Weighted toward
        /// the weakest tier so the EXP cap in <see cref="BallCatalog.ExpCap"/> is something a player
        /// actually meets and has to weigh, rather than a rule they never see.</summary>
        public static readonly IReadOnlyDictionary<BallTier, int> StartingStock =
            new Dictionary<BallTier, int>
            {
                { BallTier.Poke, 5 },
                { BallTier.Great, 2 },
                { BallTier.Ultra, 1 },
            };

        public int CountOf(BallTier tier) => counts.TryGetValue(tier, out int n) ? n : 0;

        public bool Has(BallTier tier) => CountOf(tier) > 0;

        /// <summary>Any ball of any tier — what the Throw control asks before offering itself.</summary>
        public bool HasAny()
        {
            foreach (var tier in BallCatalog.AllTiers)
            {
                if (Has(tier))
                {
                    return true;
                }
            }
            return false;
        }

        public int Total()
        {
            int total = 0;
            foreach (var tier in BallCatalog.AllTiers)
            {
                total += CountOf(tier);
            }
            return total;
        }

        public void Add(BallTier tier, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            counts[tier] = CountOf(tier) + amount;
        }

        /// <summary>Spends one ball, reporting whether there was one to spend. Returning false
        /// rather than throwing because "the player threw their last ball a moment ago" is an
        /// ordinary race between a tray and a battle, not a programming error.</summary>
        public bool TrySpend(BallTier tier)
        {
            int have = CountOf(tier);
            if (have <= 0)
            {
                return false;
            }
            counts[tier] = have - 1;
            return true;
        }

        public void GrantStartingStock()
        {
            foreach (var entry in StartingStock)
            {
                Add(entry.Key, entry.Value);
            }
        }
    }
}
