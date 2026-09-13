using System.Collections.Generic;
using Pets.Data;

namespace Pets.Meta
{
    /// <summary>Camp / Pokémon Center node resolution (design doc §5.1): grants EXP to the active
    /// line-up and a temporary Attack buff for the next fight.</summary>
    public static class CampResolver
    {
        /// <summary>A rest is worth less than the wild fight you didn't have — it costs nothing
        /// and risks nothing. Sized against LevelCurve's 7-17 EXP per level, so a Pokémon Center is
        /// a nudge rather than a level (ADR 0006).</summary>
        public const int ExpGranted = 2;

        private const float NextBattleAttackBonusPercent = 0.2f;

        /// <summary>Returns the evolutions the rest set off, for the same reason
        /// BattleRewardResolver does — the Pokémon Center overlay is where the player would see
        /// them.</summary>
        public static List<ExperienceResolver.Evolution> Resolve(RunState state, PokemonSpeciesLibrary library)
        {
            var evolutions = RunProgression.GrantToLineUp(state, ExpGranted, library);
            state.NextBattleAttackBonusPercent = NextBattleAttackBonusPercent;
            return evolutions;
        }
    }
}
