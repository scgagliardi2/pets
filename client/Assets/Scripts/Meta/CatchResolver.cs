using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Stubbed catching (PLAN.md §6, Phase 0): "pick 1 from defeated" rather than the
    /// full drag-a-Pokéball Step-boundary throw system (design doc §12.1, Phase 1).</summary>
    public static class CatchResolver
    {
        /// <summary>Call with a copy of the wild line-up taken *before* running the fight —
        /// BattleSimulator removes fainted mons from the list it's given in place, so a snapshot
        /// is needed to still see who was defeated afterward.</summary>
        public static List<PokemonInstance> GetDefeated(List<PokemonInstance> wildLineUpSnapshot)
        {
            return wildLineUpSnapshot.Where(m => !m.IsAlive).ToList();
        }

        /// <summary>Adds a fresh, full-health copy of the defeated mon's species to the Box.</summary>
        public static void Catch(RunState state, PokemonInstance defeated, PokemonSpeciesLibrary library)
        {
            var species = library.GetById(defeated.SpeciesId);
            if (species == null)
            {
                return;
            }
            string instanceId = $"box-{species.Id}-{state.Box.Count}";
            state.Box.Add(PokemonInstanceFactory.Create(species, instanceId));
        }
    }
}
