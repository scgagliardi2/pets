using System.Collections.Generic;
using Pets.Data;

namespace Pets.Meta
{
    /// <summary>What winning a node's fight gives the run (design doc §7: mons "grow via
    /// EXP/level").
    ///
    /// **The whole line-up is paid, not just the survivors** — a Reserve behind a Lead that never
    /// faints would otherwise never grow, and the floor (RunProgression) means it wouldn't be left
    /// behind anyway, so paying only survivors buys nothing but bookkeeping.
    ///
    /// **The amount depends on the node**, which it didn't when EXP was a 1-point counter: a Gym is
    /// worth most of a level, a wild fight a third of one. That's the milestone a Gym should feel
    /// like, and it's affordable now that a level costs 7-17 EXP rather than 3 (ADR 0006). The
    /// rates and LevelCurve's costs are two halves of one number — see LevelCurve for how they're
    /// sized against each other, and RunBudgetTests for the assertion that keeps them that way.</summary>
    public static class BattleRewardResolver
    {
        /// <summary>A won wild encounter — the run's bread and butter, about a third of a level
        /// early on.</summary>
        public const int ExpPerPvEWin = 3;

        /// <summary>A won trainer fight. Slightly more than a wild one: it's a fight you can't
        /// catch anything from (design doc §5.1).</summary>
        public const int ExpPerPvPWin = 4;

        /// <summary>A beaten Gym Leader — the Location's finale, and most of a level on its
        /// own.</summary>
        public const int ExpPerGymWin = 8;

        /// <summary>EXP a won fight at this node type pays each mon in the line-up. Camp isn't a
        /// fight and has its own rate (CampResolver); anything else pays the PvE rate rather than
        /// nothing, so a node type added later is quietly playable instead of silently
        /// worthless.</summary>
        public static int ExpForWin(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.Gym: return ExpPerGymWin;
                case NodeType.PvP: return ExpPerPvPWin;
                default: return ExpPerPvEWin;
            }
        }

        /// <summary>Pays the run's line-up for a win at <paramref name="nodeType"/> and returns the
        /// evolutions it set off, so the caller can tell the player about them — an evolution is the
        /// one thing here worth more than a number ticking up.</summary>
        public static List<ExperienceResolver.Evolution> GrantWinRewards(
            RunState state, PokemonSpeciesLibrary library, NodeType nodeType) =>
            RunProgression.GrantToLineUp(state, ExpForWin(nodeType), library);
    }
}
