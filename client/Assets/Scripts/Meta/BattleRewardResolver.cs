using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What winning a node's fight gives the run (design doc §7: mons "grow via EXP/level").
    ///
    /// **A win pays by the level of what was beaten**: <see cref="BaseExpPerWin"/> plus the foes'
    /// average level, doubled for a Gym. Scaling with the foe is what keeps the rising level curve
    /// (ExperienceResolver) moving at a steady pace across all eight badges; a flat reward would
    /// either race through the early game or crawl through the late one.
    ///
    /// **The whole line-up is paid, not just the survivors** — a Reserve behind a Lead that never
    /// faints would otherwise never grow — and then everything the run owns is caught up
    /// (ExperienceResolver.ApplyCatchUp), so the Box isn't left behind either.</summary>
    public static class BattleRewardResolver
    {
        public const int ExpPerPvEWin = 3;
        public const int ExpPerPvPWin = 4;
        public const int ExpPerGymWin = 8;

        /// <summary>EXP a win pays on top of the foes' average level.</summary>
        public const int BaseExpPerWin = 2;

        /// <summary>A Gym pays this many times what a wild fight at its level would.</summary>
        public const int GymExpMultiplier = 2;

        /// <summary>EXP each mon in the line-up earns for beating <paramref name="enemyLineUp"/>.</summary>
        public static int ExpForWin(IReadOnlyList<PokemonInstance> enemyLineUp, bool isGym)
        {
            int total = 0;
            int count = enemyLineUp?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                total += ExperienceResolver.LevelOf(enemyLineUp[i]);
            }
            int averageLevel = count > 0 ? (int)Math.Round((double)total / count, MidpointRounding.AwayFromZero) : 1;
            return (BaseExpPerWin + averageLevel) * (isGym ? GymExpMultiplier : 1);
        }

        public static int ExpForWin(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.Gym: return ExpPerGymWin;
                case NodeType.PvP: return ExpPerPvPWin;
                default: return ExpPerPvEWin;
            }
        }

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

        /// <summary>Pays the run's line-up for a win at <paramref name="nodeType"/> and returns the
        /// evolutions it set off, so the caller can tell the player about them — an evolution is the
        /// one thing here worth more than a number ticking up.</summary>
        public static List<ExperienceResolver.Evolution> GrantWinRewards(
            RunState state, PokemonSpeciesLibrary library, NodeType nodeType) =>
            RunProgression.GrantToLineUp(state, ExpForWin(nodeType), library);
    }
}
