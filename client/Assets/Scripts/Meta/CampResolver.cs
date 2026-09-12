using System.Collections.Generic;
using Pets.Data;

namespace Pets.Meta
{
    /// <summary>Camp / Pokémon Center node resolution (design doc §5.1): grants EXP to the active
    /// line-up and a temporary Attack buff for the next fight.</summary>
    public static class CampResolver
    {
        /// <summary>A rest is worth the same as a won fight. It used to be 30, back when EXP was a
        /// points pool and a level cost 100 — carried over unchanged that would now be +300 to
        /// every stat and ten evolutions in one click. See ExperienceResolver for the model.</summary>
        public const int ExpGranted = 1;

        private const float NextBattleAttackBonusPercent = 0.2f;

        /// <summary>Returns the evolutions the rest set off, for the same reason
        /// BattleRewardResolver does — the Pokémon Center overlay is where the player would see
        /// them.</summary>
        public static List<ExperienceResolver.Evolution> Resolve(RunState state, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<ExperienceResolver.Evolution>();
            foreach (var mon in state.LineUp)
            {
                evolutions.AddRange(ExperienceResolver.GrantExp(mon, ExpGranted, library));
            }
            state.NextBattleAttackBonusPercent = NextBattleAttackBonusPercent;
            return evolutions;
        }
    }
}
