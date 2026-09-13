using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Which species an encounter may draw — shared by the wild line-up and the Gym Leader's
    /// team, so the two can't disagree about what belongs in the game at a given badge.
    ///
    /// Three filters, each closing a hole the unfiltered roster left open (PLAN.md §11 item 9):
    /// - **Base forms only.** A species some roster species evolves into is never drawn directly; a
    ///   Charizard shows up as a Charmander that has earned its way past both thresholds
    ///   (ExperienceResolver.CreateAtExp), under exactly the rules the player's mons follow.
    /// - **A tier cap that rises with badges** (RunProgression.MaxTier), which keeps the high-tier
    ///   base forms (Snorlax, Hariyama, the Eeveelutions) out of the early game.
    /// - **No Legendaries** except on the final Gym Leader's team.
    ///
    /// Then the Location's type bias, falling back to the filtered pool when nothing matches, and to
    /// the whole library only if the filters left nothing at all.</summary>
    public static class EncounterPool
    {
        public static List<PokemonSpeciesDefinitionAsset> For(PokemonSpeciesLibrary library, PokemonType[] typeBias,
            int badges, bool isGym)
        {
            var evolvedForms = new HashSet<int>(
                library.AllSpecies.Where(s => s != null && s.EvolvesInto != null).Select(s => s.EvolvesInto.Id));
            int? cap = RunProgression.MaxTier(badges);
            bool allowLegendaries = RunProgression.AllowsLegendaries(isGym, badges);

            var eligible = library.AllSpecies
                .Where(s => s != null
                    && !evolvedForms.Contains(s.Id)
                    && (allowLegendaries || !s.IsLegendary)
                    && (cap == null || s.Tier <= cap.Value))
                .ToList();

            var biased = eligible.Where(s => Matches(s, typeBias)).ToList();
            if (biased.Count > 0)
            {
                return biased;
            }
            return eligible.Count > 0 ? eligible : library.AllSpecies.Where(s => s != null).ToList();
        }

        private static bool Matches(PokemonSpeciesDefinitionAsset species, PokemonType[] typeBias) =>
            typeBias != null && (typeBias.Contains(species.Type1) || (species.HasSecondType && typeBias.Contains(species.Type2)));
    }
}
