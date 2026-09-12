using System.Collections.Generic;
using Pets.Data;

namespace Pets.Meta
{
    /// <summary>What winning a node's fight gives the run (design doc §7: mons "grow via
    /// EXP/level").
    ///
    /// **The whole line-up is paid, not just the survivors**, and the amount is the same for a
    /// Gym as for a wild fight. Both of those used to be otherwise — survivors only, and a Gym
    /// worth three PvE nodes — and both changed when EXP became a small counter rather than a
    /// points pool (see ExperienceResolver): at one point per win there's no room left to express
    /// "more" without making a Gym worth an instant evolution, and paying only the survivors would
    /// mean a Reserve mon behind a Lead that never faints can never grow at all.
    ///
    /// The amount is a flat placeholder like every other number in the EXP model (PLAN.md §10).</summary>
    public static class BattleRewardResolver
    {
        /// <summary>EXP each mon in the line-up earns for a won fight.</summary>
        public const int ExpPerWin = 1;

        /// <summary>Pays the run's line-up for a win and returns the evolutions it set off, so the
        /// caller can tell the player about them — three wins is a mon's first evolution, which is
        /// the one thing here worth more than a number ticking up.</summary>
        public static List<ExperienceResolver.Evolution> GrantWinRewards(RunState state, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<ExperienceResolver.Evolution>();
            if (state == null)
            {
                return evolutions;
            }

            foreach (var mon in state.LineUp)
            {
                evolutions.AddRange(ExperienceResolver.GrantExp(mon, ExpPerWin, library));
            }
            return evolutions;
        }
    }
}
