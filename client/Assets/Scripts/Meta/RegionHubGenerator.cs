using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>The Region Hub's offer (design doc §5.2): the candidate Locations a player chooses
    /// between before travelling to the next one.
    ///
    /// Deterministic from the run's seed and which Location it is on, so leaving the hub and coming
    /// back shows the same three choices rather than rerolling until one looks good — the choice is
    /// the point of the screen. The same reasoning as the node map's per-node seeds
    /// (NodeResolutionController.SeedFor).</summary>
    public static class RegionHubGenerator
    {
        /// <summary>Candidate Locations offered at once (design doc §5.2 proposes three).</summary>
        public const int OfferCount = 3;

        /// <summary>One candidate Location: what it is, and the seed its node-map will be generated
        /// from if it's the one picked.</summary>
        public readonly struct Offer
        {
            public readonly LocationType Type;
            public readonly int MapSeed;

            public Offer(LocationType type, int mapSeed)
            {
                Type = type;
                MapSeed = mapSeed;
            }
        }

        /// <summary>The three Locations on offer for this run's next step. Distinct types, so a
        /// choice is always actually a choice — the same rule the node map's layers follow.</summary>
        public static List<Offer> Offers(RunState state)
        {
            int runSeed = state?.RunSeed ?? 0;
            int regionIndex = state?.RegionIndex ?? 1;
            var rng = new DeterministicRandom(runSeed ^ (regionIndex * 7919));

            var remaining = new List<LocationType>(LocationCatalog.AllTypes);
            var offers = new List<Offer>(OfferCount);
            for (int i = 0; i < OfferCount && remaining.Count > 0; i++)
            {
                int index = rng.NextInt(remaining.Count);
                var type = remaining[index];
                remaining.RemoveAt(index);
                offers.Add(new Offer(type, rng.NextInt(int.MaxValue - 1) + 1));
            }
            return offers;
        }
    }
}
