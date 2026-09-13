using System;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>How difficulty scales across a run: an eight-badge run, like a mainline Pokémon game
    /// (design doc §15's open win condition, settled in ADR 0007), with every Location's wild Pokémon
    /// and Gym Leader keyed to how many badges the player already has.
    ///
    /// **Two dials, not one** (ADR 0008). A Location raises the **tier** its opposition is drawn from
    /// — which species turn up at all — and the **EXP** those mons carry, which is how much growth
    /// they've had on top. Tier is the step change and EXP is the slope between steps; the old single
    /// "level" number had to be both at once.
    ///
    /// **Enemies scale with progress, not with the player.** Both dials come from the badge count and
    /// how far into the Location's map a node is; neither ever looks at the player's own mons.
    /// Rubber-banding would make EXP worthless — a player who fights more should be ahead, and one
    /// who dodges fights should feel it.
    ///
    /// The pacing target is ADR 0007's: a player wins most wild fights, takes a Gym on the first try
    /// about two times in three, and sees a starter's first evolution in the third Location and its
    /// last in the sixth. What changed is that the numbers are now countable by hand — a mon earns one
    /// EXP a win, a Location is about <see cref="ExpPerBadge"/> wins, and an evolution costs
    /// ExperienceResolver.ExpPerEvolution — rather than fitted to a curve. RunProgressionTests pins
    /// the shape so a later tweak that breaks it fails a test.</summary>
    public static class RunProgression
    {
        /// <summary>Badges to win the run. Beating the eighth Gym ends it as a victory.</summary>
        public const int BadgesToWin = 8;

        /// <summary>EXP a Location is worth to a mon that fights its way through: two or three wild
        /// wins, sometimes a Pokémon Center, and the Gym, at one point each
        /// (BattleRewardResolver.ExpPerWin). It's therefore also the EXP the *next* Location's
        /// opposition is pitched forward by — the two have to climb at the same rate, and
        /// RunProgressionTests fails if they drift apart.</summary>
        public const int ExpPerBadge = 4;

        /// <summary>How much EXP a Location's first wild encounters sit below its baseline; they catch
        /// up to and pass it deeper into the map.</summary>
        public const int WildExpBelowBaseline = 1;

        /// <summary>How much EXP a Location's Gym Leader carries above its baseline.</summary>
        public const int GymExpAboveBaseline = 2;

        /// <summary>The smallest a Gym Leader's team is, before it grows with badges and matches the
        /// player's line-up.</summary>
        public const int MinGymTeamSize = 2;

        /// <summary>Highest species tier the pool may draw from in the first Location, and how much
        /// that rises per badge. Base forms are mostly tier 1–2, so this is mainly what keeps Snorlax
        /// and Hariyama out of the early game — the job ADR 0007 gave a base-stat-total cap, done
        /// against the tier the stats now actually resolve to.</summary>
        public const int FirstLocationMaxTier = 1;
        public const int MaxTierIncreasePerBadge = 1;

        /// <summary>The EXP a Location is pitched at: nothing for the first, rising by
        /// <see cref="ExpPerBadge"/> for each badge already earned.</summary>
        public static int BaselineExp(int badges) => ExpPerBadge * Math.Max(0, badges);

        /// <summary>A wild encounter's EXP, for a node on map layer <paramref name="layer"/>
        /// (1 = the first choice layer).</summary>
        public static int WildExp(int badges, int layer) =>
            Math.Max(0, BaselineExp(badges) - WildExpBelowBaseline + Math.Max(0, layer) / 2);

        public static int GymExp(int badges) => BaselineExp(badges) + GymExpAboveBaseline;

        /// <summary>How many wild mons an encounter fields: one in the first Location, while the
        /// player has only their starting pair; two until the fourth badge; three after.</summary>
        public static int WildEncounterSize(int badges) => badges <= 0 ? 1 : badges < 4 ? 2 : 3;

        /// <summary>A Gym Leader fields at least as many mons as the player brings — the line-up is a
        /// train, so a longer one is simply more health to chew through — and grows with badges
        /// regardless.</summary>
        public static int GymTeamSize(int badges, int lineUpCount) =>
            Math.Min(RunState.MaxPartySize, Math.Max(lineUpCount, MinGymTeamSize + Math.Max(0, badges) / 2));

        /// <summary>The highest species tier an encounter may draw, or null for no cap (the final
        /// Location).</summary>
        public static int? MaxTier(int badges) =>
            IsFinalLocation(badges)
                ? (int?)null
                : Math.Min(SpeciesTier.MaxTier, FirstLocationMaxTier + MaxTierIncreasePerBadge * Math.Max(0, badges));

        /// <summary>Legendaries appear only on the final Gym Leader's team — the run's capstone —
        /// never in the wild, where a Groudon in the Forest was the worst thing the unfiltered pool
        /// used to do (PLAN.md §11).</summary>
        public static bool AllowsLegendaries(bool isGym, int badges) => isGym && IsFinalLocation(badges);

        public static bool IsFinalLocation(int badges) => badges >= BadgesToWin - 1;

    }
}
