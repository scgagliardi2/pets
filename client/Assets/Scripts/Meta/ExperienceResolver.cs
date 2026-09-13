using System;
using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>EXP, levels, growth and evolution (design doc §7, §12.3) — the one place a mon's
    /// stats change outside a battle. See ADR 0006 for the model and why it replaced ADR 0005's.
    ///
    /// A mon's **level** is `LevelCurve.LevelForExp(Exp)` raised to its `MinLevel` floor, and its
    /// stats are `StatGrowth.StatsFor(species base, that level)`. Nothing here accumulates: ADR
    /// 0005's best decision was that `stats = f(species, growth)` is always the whole story, and
    /// that survives intact — only `f` changed, from a flat +10 per EXP to a proportional gain per
    /// level.
    ///
    /// **Evolution is gated on level**, at <see cref="EvolutionLevels"/>, counted against how many
    /// times this particular mon has already evolved. The old rule (every 3 EXP) was sized for a
    /// run that was one Location long: across six it crossed ten thresholds against a roster whose
    /// longest chain is two, so every evolution a run would ever see happened in Region 1.</summary>
    public static class ExperienceResolver
    {
        /// <summary>The level a mon reaches its first evolution at, then its second. Indexed by
        /// PokemonInstance.TimesEvolved, so a mon that has evolved once next evolves at
        /// `EvolutionLevels[1]`.
        ///
        /// Against LevelCurve's pacing these land the first evolution early in Location 2 and the
        /// final form around the end of Location 4 — the mainline games' shape, where your starter
        /// is fully evolved for the last third of the journey rather than the last tenth.
        ///
        /// Counted per instance rather than from the species' chain depth, for the same reason as
        /// before: a curated base form whose real pre-evolution isn't in the roster (Pikachu,
        /// Snorlax) is stage 1 but has evolved zero times, and should reach Raichu on the first
        /// threshold like anything else. A mon with more chain left than this table has entries
        /// (nothing in the roster today) simply stops evolving at the end of it.</summary>
        public static readonly int[] EvolutionLevels = { 4, 9 };

        /// <summary>One evolution that just happened, so a caller can tell the player about it —
        /// the moment is worth a line on the battle result panel, and nothing else would ever
        /// surface it.</summary>
        public readonly struct Evolution
        {
            public readonly PokemonInstance Mon;
            public readonly string FromName;
            public readonly string ToName;

            public Evolution(PokemonInstance mon, string fromName, string toName)
            {
                Mon = mon;
                FromName = fromName;
                ToName = toName;
            }
        }

        /// <summary>The level this mon is actually at: what its own EXP has bought, or the floor
        /// its run holds it at, whichever is higher (PokemonInstance.MinLevel).</summary>
        public static int LevelOf(PokemonInstance mon) =>
            mon == null ? LevelCurve.StartingLevel : Math.Max(LevelCurve.LevelForExp(mon.Exp), mon.MinLevel);

        /// <summary>Grants EXP and applies the growth and any evolutions it earns, in place.
        /// Returns the evolutions that happened, newest last; usually empty.
        ///
        /// Note this pays one mon. Paying a *run* — which also moves the floor every other mon it
        /// owns is held at — is RunProgression's job, and is what the award sites call.</summary>
        public static List<Evolution> GrantExp(PokemonInstance mon, int amount, PokemonSpeciesLibrary library)
        {
            if (mon == null || amount <= 0)
            {
                return new List<Evolution>();
            }

            mon.Exp += amount;
            return ApplyGrowth(mon, library);
        }

        /// <summary>Raises the level this mon is held at, if <paramref name="level"/> is above its
        /// current floor, and applies the growth and evolutions that follow. Never lowers it: a
        /// veteran that is ahead of its run's floor stays ahead.</summary>
        public static List<Evolution> SetMinLevel(PokemonInstance mon, int level, PokemonSpeciesLibrary library)
        {
            if (mon == null || level <= mon.MinLevel)
            {
                return new List<Evolution>();
            }

            mon.MinLevel = Math.Min(level, LevelCurve.MaxLevel);
            return ApplyGrowth(mon, library);
        }

        /// <summary>The level at which this mon next evolves, or <see cref="int.MaxValue"/> if it
        /// has run out of evolutions to reach (a final form, a branching line the content layer
        /// leaves unresolved, or a chain longer than <see cref="EvolutionLevels"/>). Public so the
        /// Team screen can show "evolves at 9" without restating the rule.</summary>
        public static int NextEvolutionLevel(PokemonInstance mon)
        {
            if (mon == null || mon.TimesEvolved >= EvolutionLevels.Length)
            {
                return int.MaxValue;
            }
            return EvolutionLevels[mon.TimesEvolved];
        }

        /// <summary>Whether this mon has somewhere to evolve to at all — false for a final form,
        /// for the branching lines the content layer leaves unresolved
        /// (PokemonSpeciesDefinitionAsset.EvolvesInto), and for a mon that has used up
        /// <see cref="EvolutionLevels"/>.</summary>
        public static bool CanEverEvolve(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library != null
            && NextEvolutionLevel(mon) != int.MaxValue
            && library.GetById(mon.SpeciesId)?.EvolvesInto != null;

        /// <summary>Rebuilds CurrentStats (and tops HP back up) from the mon's species and level.
        /// Called after any change to either; also the thing that would rebuild a loaded save's
        /// stats once there is a save layer.
        ///
        /// Anything written onto CurrentStats that doesn't follow from species + level is transient
        /// — the next grant overwrites it. A permanent modifier (an item) has to become an input to
        /// StatGrowth rather than a one-off addition here.
        ///
        /// HP goes back to full because nothing carries damage between fights (ADR 0003) — when
        /// that changes, this needs to preserve the damage taken rather than the HP value, or
        /// growing will quietly heal.</summary>
        public static void Recompute(PokemonInstance mon, PokemonSpeciesLibrary library)
        {
            var species = library?.GetById(mon.SpeciesId);
            if (species == null)
            {
                return;
            }

            mon.CurrentStats = StatGrowth.StatsFor(
                new Stats { Attack = species.BaseAttack, Health = species.BaseHealth, Speed = species.BaseSpeed },
                LevelOf(mon));
            mon.CurrentHP = mon.CurrentStats.Health;
        }

        /// <summary>Resolves however many evolutions the mon's current level has earned, then
        /// rebuilds its stats — the shared tail of every way a mon can grow.</summary>
        private static List<Evolution> ApplyGrowth(PokemonInstance mon, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<Evolution>();

            // A loop, not an if: a mon can cross both thresholds at once — a Charmander caught in
            // Location 5 joins at the run's floor level and should arrive as a Charizard rather
            // than needing two more fights to catch up with a chain it has already outgrown.
            while (TryEvolve(mon, library, out var evolution))
            {
                evolutions.Add(evolution);
            }

            Recompute(mon, library);
            return evolutions;
        }

        /// <summary>One evolution step, if the mon has reached the level for it and its species has
        /// somewhere to go. The passive is re-resolved at the new species' stage — a passive
        /// persists through evolution and only its magnitude scales (design doc §8,
        /// content-schema.md §1), which is what MagnitudeByStage is for.</summary>
        private static bool TryEvolve(PokemonInstance mon, PokemonSpeciesLibrary library, out Evolution evolution)
        {
            evolution = default;
            if (LevelOf(mon) < NextEvolutionLevel(mon))
            {
                return false;
            }

            var species = library?.GetById(mon.SpeciesId);
            var next = species?.EvolvesInto;
            if (next == null)
            {
                return false;
            }

            mon.SpeciesId = next.Id;
            mon.TimesEvolved++;
            mon.PassiveId = next.Passive != null ? next.Passive.Id : null;
            mon.ResolvedPassive = next.Passive != null
                ? PokemonInstanceFactory.ResolvePassive(next.Passive, next.EvolutionStage)
                : null;

            evolution = new Evolution(mon, species.DisplayName, next.DisplayName);
            return true;
        }
    }
}
