using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>EXP, growth and evolution (design doc §7 "grow them via EXP/level", §12.3
    /// evolution) — the one place a mon's stats change outside a battle.
    ///
    /// EXP is a **small counter, not a points pool**: one per battle won, two per duplicate
    /// combined in. Every point is worth a flat <see cref="StatGainPerExp"/> on all three stats,
    /// and every <see cref="ExpPerEvolution"/> points a mon evolves if its species can. There is no
    /// Level, no curve and no per-stat weighting — the numbers are explicit placeholders to be
    /// tuned (PLAN.md §10), and a flat model is the one that's obvious to reason about while
    /// they're being tuned.
    ///
    /// **Stats are derived, never accumulated.** <see cref="Recompute"/> rebuilds CurrentStats from
    /// the mon's *current* species plus its EXP, so `species base + StatGainPerExp * Exp` is always
    /// the whole story. The previous version added a percentage onto the running total on each
    /// level-up, which meant the stats depended on the order things happened in and couldn't be
    /// re-derived from the save data — and made an evolution's change of base stats impossible to
    /// apply without double-counting.</summary>
    public static class ExperienceResolver
    {
        /// <summary>Attack, Health and Speed each gain this much per point of EXP. Flat and equal
        /// across the three on purpose: a placeholder to be tweaked (design doc §8 says the same
        /// about the base stats themselves).</summary>
        public const int StatGainPerExp = 10;

        /// <summary>EXP between evolutions. Counted from the mon's total EXP against how many times
        /// it has already evolved, so a fresh mon evolves at 3, its next form at 6, and so on —
        /// rather than the whole chain resolving at once the moment it first hits 3.</summary>
        public const int ExpPerEvolution = 3;

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

        /// <summary>Grants EXP and applies the growth and any evolutions it earns, in place.
        /// Returns the evolutions that happened, newest last; usually empty.</summary>
        public static List<Evolution> GrantExp(PokemonInstance mon, int amount, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<Evolution>();
            if (mon == null || amount <= 0)
            {
                return evolutions;
            }

            mon.Exp += amount;

            // A loop, not an if: combining a duplicate grants 2 at once, and a Camp plus a win can
            // land a mon past two thresholds between one look at the Team screen and the next.
            while (TryEvolve(mon, library, out var evolution))
            {
                evolutions.Add(evolution);
            }

            Recompute(mon, library);
            return evolutions;
        }

        /// <summary>Total EXP at which <paramref name="mon"/> next evolves, whether or not its
        /// species can. Public so the Team screen can show "2 / 3" without restating the rule.</summary>
        public static int ExpToNextEvolution(PokemonInstance mon) =>
            ExpPerEvolution * (mon.TimesEvolved + 1);

        /// <summary>Whether this mon has somewhere to evolve to at all — false for a final form,
        /// and for the branching lines the content layer leaves unresolved
        /// (PokemonSpeciesDefinitionAsset.EvolvesInto).</summary>
        public static bool CanEverEvolve(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library != null && library.GetById(mon.SpeciesId)?.EvolvesInto != null;

        /// <summary>Rebuilds CurrentStats (and tops HP back up) from the mon's species and EXP.
        /// Called after any change to either; also the thing that would rebuild a loaded save's
        /// stats once there is a save layer.
        ///
        /// HP goes back to full because nothing carries damage between fights today (ADR 0003) —
        /// when that changes, this needs to preserve the damage taken rather than the HP value, or
        /// growing will quietly heal.</summary>
        public static void Recompute(PokemonInstance mon, PokemonSpeciesLibrary library)
        {
            var species = library?.GetById(mon.SpeciesId);
            if (species == null)
            {
                return;
            }

            int gain = StatGainPerExp * mon.Exp;
            mon.CurrentStats = new Stats
            {
                Attack = species.BaseAttack + gain,
                Health = species.BaseHealth + gain,
                Speed = species.BaseSpeed + gain,
            };
            mon.CurrentHP = mon.CurrentStats.Health;
        }

        /// <summary>One evolution step, if the mon has earned it and its species has somewhere to
        /// go. The passive is re-resolved at the new species' stage — a passive persists through
        /// evolution and only its magnitude scales (design doc §8, content-schema.md §1), which is
        /// what MagnitudeByStage is for and what nothing used until now.</summary>
        private static bool TryEvolve(PokemonInstance mon, PokemonSpeciesLibrary library, out Evolution evolution)
        {
            evolution = default;
            if (mon.Exp < ExpToNextEvolution(mon))
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
