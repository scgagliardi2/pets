using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds a wild PvE line-up biased toward a Location's type pool (design doc §4),
    /// seeded so encounters are reproducible from a run seed (design doc §10.5), and built at the
    /// level and stage its Location calls for (RegionTier).</summary>
    public static class EncounterGenerator
    {
        /// <summary>Picks two species (with repeats allowed) from those matching any of
        /// <paramref name="typeBias"/> that <paramref name="tier"/> allows, falling back to the
        /// whole roster if the bias matches nothing, and converts them into a fresh wild line-up at
        /// the tier's level.
        ///
        /// The tier is what makes a wild fight belong to the Location it's in: without it this drew
        /// from all 183 species at base stats, so a Location-1 Forest could field a fully-evolved
        /// Legendary and a Location-6 one a Caterpie.</summary>
        public static List<PokemonInstance> GenerateWildLineUp(
            PokemonSpeciesLibrary library, PokemonType[] typeBias, int seed, string instanceIdPrefix,
            RegionTier.Encounter tier)
        {
            var biased = library.AllSpecies.Where(s => typeBias.Contains(s.Type1) || (s.HasSecondType && typeBias.Contains(s.Type2))).ToList();
            if (biased.Count == 0)
            {
                biased = library.AllSpecies;
            }
            var pool = RegionTier.Filter(biased, tier);

            var rng = new DeterministicRandom(seed);
            var lineUp = new List<PokemonInstance>();
            for (int i = 0; i < 2; i++)
            {
                var species = pool[rng.NextInt(pool.Count)];
                lineUp.Add(PokemonInstanceFactory.Create(species, $"{instanceIdPrefix}-{i}", tier.Level));
            }
            return lineUp;
        }
    }
}
