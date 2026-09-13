using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What winning a node's fight gives the run (design doc §7: mons "grow via EXP/level").
    ///
    /// **A win is worth one point, and so is every other win** — wild, Gym and Pokémon Center alike
    /// (ADR 0008). Five points is an evolution (ExperienceResolver.ExpPerEvolution), so a flat reward
    /// makes "five wins" a number the player can count on their fingers; the moment a Gym pays double,
    /// nobody can say when their Charmander evolves without doing arithmetic. What rises across a run
    /// is the opposition (RunProgression), not the payout — which is the same job ADR 0007's scaled
    /// rewards did, moved to the side of the ledger a player doesn't have to track.
    ///
    /// **The whole line-up is paid, not just the survivors** — a Reserve behind a Lead that never
    /// faints would otherwise never grow — and then everything the run owns is caught up
    /// (ExperienceResolver.ApplyCatchUp), so the Box isn't left behind either.</summary>
    public static class BattleRewardResolver
    {
        /// <summary>EXP a won fight pays every mon in the line-up. One, for every kind of fight.</summary>
        public const int ExpPerWin = 1;

        /// <summary>EXP each mon in the line-up earns for beating <paramref name="enemyLineUp"/>.
        /// Neither argument changes the answer — they're kept because a caller shouldn't have to know
        /// that, and because whether a Gym is worth more is exactly the kind of thing balancing
        /// revisits.</summary>
        public static int ExpForWin(IReadOnlyList<PokemonInstance> enemyLineUp, bool isGym) => ExpPerWin;

        /// <summary>Pays the run's line-up for a win and reports what grew. Box mons the catch-up
        /// raised aren't in the report: they didn't fight, and the Team screen shows where they got to.</summary>
        public static GrowthReport GrantWinRewards(RunState state, IReadOnlyList<PokemonInstance> enemyLineUp, bool isGym,
            PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            if (state == null)
            {
                return report;
            }

            int amount = ExpForWin(enemyLineUp, isGym);
            report.ExpGranted = amount;
            foreach (var mon in state.LineUp)
            {
                report.Merge(ExperienceResolver.GrantExp(mon, amount, library));
            }
            ExperienceResolver.ApplyCatchUp(state, library);
            return report;
        }
    }
}
