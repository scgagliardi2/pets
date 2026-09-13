using System;
using System.Collections.Generic;
using Pets.Data;

namespace Pets.Meta
{
    /// <summary>How hard a Location's opposition is, and what it's drawn from (ADR 0006).
    ///
    /// Before this, every enemy in the game was built at the species' base stats from the whole
    /// unfiltered 183 (ADR 0004's open item): a Location-1 wild encounter could be a fully-evolved
    /// Legendary, and a party six Locations in beat everything on the board without trying —
    /// simulated against the real roster, a player one Location's worth of growth ahead won 97% of
    /// fights, and by mid-run 100%. There was no curve to be fair *against*.
    ///
    /// Two levers, both keyed off how far the run has come rather than off the calendar:
    ///
    /// **Level.** Opposition is built at the party's own level plus a delta per node type, through
    /// the same StatGrowth rule the player's mons grow by. The deltas are what the win rates are
    /// tuned with: simulated across the real roster with stage-filtered pools, -3 gives wild fights
    /// an ~85% win rate and -1 gives a Gym ~74%, which across a six-Location run costs about three
    /// Morale — a run with headroom to spare, that can still be lost by playing badly.
    ///
    /// **Pool.** Early Locations draw base forms only, later ones open up to evolved species, and
    /// Legendaries are the last Gym's alone. That is what keeps the difficulty curve from being
    /// re-rolled by the species lottery: a level delta means nothing when the draw can be anything
    /// from a Caterpie to a Rayquaza.</summary>
    public static class RegionTier
    {
        /// <summary>Locations in a full run, and therefore badges needed to win it (design doc §14,
        /// and the answer this build gives to §20's open "what ends a run" question).</summary>
        public const int RegionsPerRun = 6;

        /// <summary>Levels a wild encounter sits below the party. The trash-mob delta: you should
        /// beat these while spending attention on what to catch, not on whether you survive.</summary>
        public const int WildLevelDelta = -3;

        /// <summary>Levels a trainer fight sits below the party — harder than wildlife, and worth
        /// more EXP for it (BattleRewardResolver).</summary>
        public const int TrainerLevelDelta = -2;

        /// <summary>Levels a Gym Leader sits below the party. Nearly even: the Location's finale is
        /// the fight a run is actually meant to be tested by.</summary>
        public const int GymLevelDelta = -1;

        /// <summary>The evolution stage a Location's opposition is drawn up to, by 1-based region
        /// index: base forms for the first two Locations, first evolutions through the middle two,
        /// the whole chain at the end. A player meets Charmeleon around when their own starter
        /// becomes one.</summary>
        public static int MaxEvolutionStageFor(int regionIndex)
        {
            if (regionIndex <= 2) return 0;
            if (regionIndex <= 4) return 1;
            return 2;
        }

        /// <summary>Legendaries are the final Gym's, and nothing else's (design doc §8 treats the
        /// roster's seven as Legendary-tier). Meeting one as a Location-1 wild encounter was the
        /// most visible symptom of the unfiltered pools.</summary>
        public static bool AllowsLegendaries(int regionIndex, NodeType nodeType) =>
            nodeType == NodeType.Gym && regionIndex >= RegionsPerRun;

        /// <summary>The level the run's own mons are at — what the opposition's level is measured
        /// against. Read from the run's earned EXP rather than from a mon, so a party that just
        /// caught something doesn't make the next fight easier by diluting its own average.</summary>
        public static int PartyLevel(RunState state) =>
            state == null ? LevelCurve.StartingLevel : LevelCurve.LevelForExp(state.RunExp);

        /// <summary>The level to build a <paramref name="nodeType"/> encounter at for this run.
        /// Never below the starting level: the first Location's wild fights are level 1 whatever the
        /// delta says.</summary>
        public static int EnemyLevel(RunState state, NodeType nodeType)
        {
            int delta;
            switch (nodeType)
            {
                case NodeType.Gym: delta = GymLevelDelta; break;
                case NodeType.PvP: delta = TrainerLevelDelta; break;
                default: delta = WildLevelDelta; break;
            }
            return Math.Max(LevelCurve.StartingLevel, PartyLevel(state) + delta);
        }

        /// <summary>Everything an encounter generator needs to know about where in a run it is.
        /// Passed around as one value so adding a third lever later doesn't mean threading another
        /// argument through every generator.</summary>
        public readonly struct Encounter
        {
            public readonly int Level;
            public readonly int MaxEvolutionStage;
            public readonly bool AllowLegendaries;

            public Encounter(int level, int maxEvolutionStage, bool allowLegendaries)
            {
                Level = level;
                MaxEvolutionStage = maxEvolutionStage;
                AllowLegendaries = allowLegendaries;
            }

            /// <summary>The tier a screen without a run behind it gets — the dev random battle, or
            /// a scene opened on its own: level 1, base forms, no Legendaries.</summary>
            public static Encounter Default =>
                new Encounter(LevelCurve.StartingLevel, MaxEvolutionStageFor(1), false);
        }

        /// <summary>The tier a node of <paramref name="nodeType"/> should be built at, for the
        /// Location this run is on.</summary>
        public static Encounter For(RunState state, NodeType nodeType)
        {
            int region = state?.RegionIndex ?? 1;
            return new Encounter(
                EnemyLevel(state, nodeType),
                MaxEvolutionStageFor(region),
                AllowsLegendaries(region, nodeType));
        }

        /// <summary>Narrows a species list to what <paramref name="tier"/> allows. If that filters
        /// empty, relaxes only the evolution-stage cap (still enforcing the Legendary rule) so an
        /// encounter can still be built without reintroducing forbidden Legendaries.</summary>
        public static List<PokemonSpeciesDefinitionAsset> Filter(
            IReadOnlyList<PokemonSpeciesDefinitionAsset> species, Encounter tier)
        {
            var pool = new List<PokemonSpeciesDefinitionAsset>();
            foreach (var candidate in species)
            {
                if (candidate == null || candidate.EvolutionStage > tier.MaxEvolutionStage)
                {
                    continue;
                }
                if (candidate.IsLegendary && !tier.AllowLegendaries)
                {
                    continue;
                }
                pool.Add(candidate);
            }

            if (pool.Count == 0)
            {
                foreach (var candidate in species)
                {
                    if (candidate == null)
                    {
                        continue;
                    }
                    if (candidate.IsLegendary && !tier.AllowLegendaries)
                    {
                        continue;
                    }
                    pool.Add(candidate);
                }
            }
            return pool;
        }
    }
}
