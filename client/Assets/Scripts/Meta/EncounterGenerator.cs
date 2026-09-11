using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds a wild PvE line-up biased toward a Location's type pool (design doc §4),
    /// seeded so encounters are reproducible from a run seed (design doc §10.5).</summary>
    public static class EncounterGenerator
    {
        /// <summary>Picks two species (with repeats allowed) from those matching any of
        /// <paramref name="typeBias"/>, falling back to the full roster if none match, and
        /// converts them into a fresh wild line-up.</summary>
        public static List<PokemonInstance> GenerateWildLineUp(
            PokemonSpeciesLibrary library, PokemonType[] typeBias, int seed, string instanceIdPrefix)
        {
            var pool = library.AllSpecies.Where(s => typeBias.Contains(s.Type1) || (s.HasSecondType && typeBias.Contains(s.Type2))).ToList();
            if (pool.Count == 0)
            {
                pool = library.AllSpecies;
            }

            var rng = new DeterministicRandom(seed);
            var lineUp = new List<PokemonInstance>();
            for (int i = 0; i < 2; i++)
            {
                var species = pool[rng.NextInt(pool.Count)];
                lineUp.Add(PokemonInstanceFactory.Create(species, $"{instanceIdPrefix}-{i}"));
            }
            return lineUp;
        }
    }
}
