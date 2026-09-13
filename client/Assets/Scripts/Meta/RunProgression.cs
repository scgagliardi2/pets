using System;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>How difficulty scales across a run: an eight-badge run, like a mainline Pokémon game
    /// (design doc §15's open win condition, settled here — ADR 0007), with every Location's wild
    /// Pokémon and Gym Leader keyed to how many badges the player already has.
    ///
    /// **Enemies scale with progress, not with the player.** A wild encounter's level comes from the
    /// badge count and how far into the Location's map it is; it never looks at the player's own
    /// levels. Rubber-banding enemies to the player would make EXP worthless — a player who fights
    /// more should be ahead, and one who dodges fights should feel it.
    ///
    /// Every number here was fitted by simulating whole runs against the real roster and the sim's
    /// exchange rule, targeting roughly 85% wild wins, a Gym won on the first try about two times in
    /// three, and a starter evolving around the third and sixth Locations. RunProgressionTests pins
    /// the shape so a later tweak that breaks it fails a test.</summary>
    public static class RunProgression
    {
        /// <summary>Badges to win the run. Beating the eighth Gym ends it as a victory.</summary>
        public const int BadgesToWin = 8;

        /// <summary>Levels the curve moves up per badge.</summary>
        public const int LevelsPerBadge = 3;

        /// <summary>How far below a Location's baseline its first wild encounters sit; they catch up
        /// to and pass it deeper into the map.</summary>
        public const int WildLevelsBelowBaseline = 1;

        /// <summary>How far above the Location's baseline its Gym Leader's team sits.</summary>
        public const int GymLevelsAboveBaseline = 1;

        /// <summary>The smallest a Gym Leader's team is, before it grows with badges and matches the
        /// player's line-up.</summary>
        public const int MinGymTeamSize = 2;

        /// <summary>Base stat total the wild pool is capped at for the first Location — the same line
        /// Character Select draws for starters — and how much the cap rises per badge. Keeps Snorlax
        /// and the Eeveelutions out of the early game.</summary>
        public const int FirstLocationMaxBaseStatTotal = 180;
        public const int MaxBaseStatTotalIncreasePerBadge = 20;

        /// <summary>The level a Location is pitched at: 1 for the first, rising by
        /// <see cref="LevelsPerBadge"/> for each badge already earned.</summary>
        public static int BaselineLevel(int badges) => 1 + LevelsPerBadge * Math.Max(0, badges);

        /// <summary>A wild encounter's level, for a node on map layer <paramref name="layer"/>
        /// (1 = the first choice layer).</summary>
        public static int WildLevel(int badges, int layer) =>
            Math.Max(1, BaselineLevel(badges) - WildLevelsBelowBaseline + Math.Max(0, layer) / 2);

        public static int GymLevel(int badges) => BaselineLevel(badges) + GymLevelsAboveBaseline;

        /// <summary>How many wild mons an encounter fields: one in the first Location, while the
        /// player has only their starting pair; two until the fourth badge; three after.</summary>
        public static int WildEncounterSize(int badges) => badges <= 0 ? 1 : badges < 4 ? 2 : 3;

        /// <summary>A Gym Leader fields at least as many mons as the player brings — the line-up is a
        /// train, so a longer one is simply more health to chew through — and grows with badges
        /// regardless.</summary>
        public static int GymTeamSize(int badges, int lineUpCount) =>
            Math.Min(RunState.MaxPartySize, Math.Max(lineUpCount, MinGymTeamSize + Math.Max(0, badges) / 2));

        /// <summary>The highest base stat total an encounter may draw, or null for no cap (the final
        /// Location).</summary>
        public static int? MaxBaseStatTotal(int badges) =>
            IsFinalLocation(badges)
                ? (int?)null
                : FirstLocationMaxBaseStatTotal + MaxBaseStatTotalIncreasePerBadge * Math.Max(0, badges);

        /// <summary>Legendaries appear only on the final Gym Leader's team — the run's capstone —
        /// never in the wild, where a Groudon in the Forest was the worst thing the unfiltered pool
        /// used to do (PLAN.md §11).</summary>
        public static bool AllowsLegendaries(bool isGym, int badges) => isGym && IsFinalLocation(badges);

        public static bool IsFinalLocation(int badges) => badges >= BadgesToWin - 1;

    }
}
