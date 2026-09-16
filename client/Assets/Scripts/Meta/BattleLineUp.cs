using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds a real fight's combatants from the run's mons: the same copy
    /// BattleCombatant.FromLineUp makes, plus each mon's species types, which is what team type
    /// synergies count (Simulation/TeamSynergy, ADR 0015). The types live on the combatant because a
    /// PokemonInstance only holds a species id, and the simulator can't look one up.</summary>
    public static class BattleLineUp
    {
        public static List<BattleCombatant> Assemble(IReadOnlyList<PokemonInstance> lineUp, PokemonSpeciesLibrary library)
        {
            var combatants = BattleCombatant.FromLineUp(lineUp);
            if (library == null)
            {
                return combatants;
            }
            foreach (var combatant in combatants)
            {
                var species = library.GetById(combatant.Source.SpeciesId);
                if (species != null)
                {
                    combatant.Types = TypesOf(species);
                }
            }
            return combatants;
        }

        public static List<PokemonType> TypesOf(PokemonSpeciesDefinitionAsset species)
        {
            var types = new List<PokemonType> { species.Type1 };
            if (species.HasSecondType && species.Type2 != species.Type1)
            {
                types.Add(species.Type2);
            }
            return types;
        }
    }
}
