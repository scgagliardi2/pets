using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds a wild PvE line-up for a map node: species from the Location's type-biased pool
    /// (EncounterPool), carrying the EXP and at the group size the run's progress calls for
    /// (RunProgression), seeded so encounters are reproducible from a run seed (design doc §10.5).</summary>
    public static class EncounterGenerator
    {
        /// <param name="badges">Badges the run already has — what pitches the encounter's tier and EXP.</param>
        /// <param name="layer">The node's map layer; deeper into a Location, the wild mons carry more EXP.</param>
        public static List<PokemonInstance> GenerateWildLineUp(PokemonSpeciesLibrary library, PokemonType[] typeBias,
            int badges, int layer, int seed, string instanceIdPrefix)
        {
            var pool = EncounterPool.For(library, typeBias, badges, isGym: false);
            int exp = RunProgression.WildExp(badges, layer);
            int size = RunProgression.WildEncounterSize(badges);

            var rng = new DeterministicRandom(seed);
            var lineUp = new List<PokemonInstance>(size);
            for (int i = 0; i < size; i++)
            {
                var species = pool[rng.NextInt(pool.Count)];
                lineUp.Add(ExperienceResolver.CreateAtExp(species, $"{instanceIdPrefix}-{i}", exp, library));
            }
            return lineUp;
        }
    }
}
