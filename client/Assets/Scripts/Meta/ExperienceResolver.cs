using System;
using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>EXP, growth and evolution (design doc §7 "grow them via EXP/level", §12.3) — the one
    /// place a mon's stats change outside a battle. See ADR 0008, which replaces ADR 0007's level
    /// curve with a counter small enough to hold in your head.
    ///
    /// **EXP is a plain count of wins, and every point is +1 Attack and +1 Health.** A Charmander is
    /// 2/2/1; win a fight and it's 3/3/1. There is no level, no curve and no rising cost — the
    /// species' tier (Pets.Data.SpeciesTier) says how strong it starts, and EXP says how far it has
    /// come since. ADR 0007's curve needed four constants and a Health multiplier before a player
    /// could be told what a win was worth; this needs one sentence.
    ///
    /// **Five points is an evolution**, at <see cref="ExpPerEvolution"/>, and a tier is worth exactly
    /// that much growth (<see cref="SpeciesTier.TotalIncreasePerTier"/>), so a mon that evolves one
    /// tier up neither gains nor loses at the moment it lands: a maxed 7/7/1 Charmander becomes a
    /// 7/6/2 Charmeleon and starts again from there. Everything past one tier is a real jump, which is
    /// why a Magikarp becoming a Gyarados feels like one.
    ///
    /// **EXP is lifetime, and never resets.** What resets is the growth applied on top of the current
    /// species: <see cref="ExpSinceEvolution"/> is the mon's total less the
    /// <see cref="ExpPerEvolution"/> each evolution consumed. Keeping the total means a mon's whole
    /// history is one number — which is what <see cref="ApplyCatchUp"/> compares, and what a save
    /// layer can rebuild a mon from.
    ///
    /// **Stats are derived, never accumulated** — ADR 0005's rule, unchanged. `StatGrowth.AtExp(species,
    /// ExpSinceEvolution(mon))` is always the whole story, so the same EXP gives the same stats however
    /// it arrived, and an evolution's new tier line applies by simply recomputing.
    ///
    /// **Nobody falls hopelessly behind.** <see cref="ApplyCatchUp"/> keeps every mon the run owns
    /// within <see cref="CatchUpExpGap"/> points of its most-experienced, so a mon caught late or left
    /// in the Box is still worth using. It levels experience, not power: a caught Caterpie gets the
    /// same EXP as the Charizard beside it and is still a Caterpie, so tiers keep meaning something.</summary>
    public static class ExperienceResolver
    {
        /// <summary>EXP a mon must earn on its current species before it evolves. A tier is worth
        /// exactly this much growth, which is what makes an evolution one tier up stat-neutral.</summary>
        public const int ExpPerEvolution = 5;

        /// <summary>How far behind the run's most-experienced mon any other mon it owns may fall
        /// before <see cref="ApplyCatchUp"/> raises it — about two fights.</summary>
        public const int CatchUpExpGap = 2;

        /// <summary>A bound on the evolution loop, so a malformed chain that cycles can't hang the
        /// game. No real chain is longer than three species.</summary>
        private const int MaxEvolutionsPerGrant = 8;

        /// <summary>One evolution that just happened, so a caller can tell the player about it.</summary>
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

        /// <summary>EXP this mon has earned since it last evolved — what its current species' stats
        /// grow on. Its lifetime total less the <see cref="ExpPerEvolution"/> each evolution spent.</summary>
        public static int ExpSinceEvolution(PokemonInstance mon) =>
            Math.Max(0, mon.Exp - ExpPerEvolution * Math.Max(0, mon.TimesEvolved));

        /// <summary>EXP still to earn before this mon's next evolution, or null if it has nowhere to
        /// go — what a "3 / 5" readout needs.</summary>
        public static int? ExpToNextEvolution(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            CanEverEvolve(mon, library) ? Math.Max(0, ExpPerEvolution - ExpSinceEvolution(mon)) : (int?)null;

        /// <summary>The lifetime EXP a mon of this species must have simply to exist: an evolved
        /// species is the record of the evolutions that produced it, each of which cost
        /// <see cref="ExpPerEvolution"/>. A Charmeleon can never be below 5.</summary>
        public static int MinExpFor(PokemonSpeciesDefinitionAsset species, PokemonSpeciesLibrary library) =>
            ExpPerEvolution * EvolutionsBefore(species, library);

        /// <summary>Whether this mon has somewhere to evolve to at all — false for a final form, and
        /// for the branching lines the content layer leaves unresolved
        /// (PokemonSpeciesDefinitionAsset.EvolvesInto).</summary>
        public static bool CanEverEvolve(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library != null && library.GetById(mon.SpeciesId)?.EvolvesInto != null;

        /// <summary>What this mon evolves into next, or null if it won't.</summary>
        public static PokemonSpeciesDefinitionAsset NextEvolution(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library?.GetById(mon.SpeciesId)?.EvolvesInto;

        /// <summary>The tier this mon's species sits in — the run's shorthand for how strong it
        /// is before any EXP.</summary>
        public static int TierOf(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library?.GetById(mon.SpeciesId)?.Tier ?? SpeciesTier.MinTier;

        /// <summary>Grants EXP and applies the growth and evolutions it earns, in place.</summary>
        public static GrowthReport GrantExp(PokemonInstance mon, int amount, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            if (mon == null || amount <= 0)
            {
                return report;
            }

            var before = mon.CurrentStats;
            string nameBefore = NameOf(mon, library);
            mon.Exp += amount;
            report.ExpGranted = amount;
            Grow(mon, before, nameBefore, library, report);
            return report;
        }

        /// <summary>Raises a mon to <paramref name="exp"/> lifetime EXP if it's below it; never
        /// lowers one.</summary>
        public static GrowthReport RaiseToExp(PokemonInstance mon, int exp, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            if (mon == null || mon.Exp >= exp)
            {
                return report;
            }

            var before = mon.CurrentStats;
            string nameBefore = NameOf(mon, library);
            report.ExpGranted = exp - mon.Exp;
            mon.Exp = exp;
            Grow(mon, before, nameBefore, library, report);
            return report;
        }

        /// <summary>A mon of <paramref name="species"/> carrying <paramref name="exp"/> lifetime EXP,
        /// already evolved as far as that EXP allows — what a wild encounter, a Gym Leader's team, a
        /// catch and a new run are all built from.
        ///
        /// An evolved species starts with its evolutions already paid for (see
        /// <see cref="MinExpFor"/>): a Charmeleon handed out at 2 EXP is really a 5-EXP mon that spent
        /// all of it getting here, so it is raised to 5 rather than being made a Charmeleon that has
        /// somehow never earned anything.</summary>
        public static PokemonInstance CreateAtExp(PokemonSpeciesDefinitionAsset species, string instanceId,
            int exp, PokemonSpeciesLibrary library)
        {
            var mon = PokemonInstanceFactory.Create(species, instanceId);
            mon.TimesEvolved = EvolutionsBefore(species, library);
            mon.Exp = Math.Max(Math.Max(0, exp), MinExpFor(species, library));
            for (int i = 0; i < MaxEvolutionsPerGrant && TryEvolve(mon, library, out _); i++)
            {
            }
            Recompute(mon, library);
            return mon;
        }

        /// <summary>How many roster species come before <paramref name="species"/> in its evolution
        /// chain — counted within the roster, not PokeAPI's chain depth, because the two disagree for
        /// a curated base form whose real pre-evolution isn't in the roster: Pikachu is stage 1 in
        /// the real chain but has nothing before it here, and should evolve on its first five points
        /// like anything else.</summary>
        public static int EvolutionsBefore(PokemonSpeciesDefinitionAsset species, PokemonSpeciesLibrary library)
        {
            if (library == null || species == null)
            {
                return 0;
            }

            int depth = 0;
            var current = species;
            // Bounded: a malformed chain that loops must not hang the game.
            while (depth < MaxEvolutionsPerGrant)
            {
                var previous = library.AllSpecies.FirstOrDefault(
                    s => s != null && s.EvolvesInto != null && s.EvolvesInto.Id == current.Id);
                if (previous == null)
                {
                    break;
                }
                depth++;
                current = previous;
            }
            return depth;
        }

        /// <summary>The least EXP any mon in this run should have: the most-experienced mon it owns,
        /// less <see cref="CatchUpExpGap"/>.</summary>
        public static int CatchUpExp(RunState state)
        {
            int top = 0;
            foreach (var mon in state.LineUp.Concat(state.Box))
            {
                top = Math.Max(top, mon.Exp);
            }
            return Math.Max(0, top - CatchUpExpGap);
        }

        /// <summary>Raises every mon in the line-up and the Box to <see cref="CatchUpExp"/>. Called
        /// after anything that grows the run's most-experienced mon, and whenever a mon joins.</summary>
        public static GrowthReport ApplyCatchUp(RunState state, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            int floor = CatchUpExp(state);
            foreach (var mon in state.LineUp.Concat(state.Box).ToList())
            {
                report.Merge(RaiseToExp(mon, floor, library));
            }
            return report;
        }

        /// <summary>Rebuilds CurrentStats (and tops HP back up) from the mon's species and the EXP it
        /// has earned on it.
        ///
        /// Anything written onto CurrentStats that doesn't follow from species + EXP is transient
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

            mon.CurrentStats = StatGrowth.AtExp(species, ExpSinceEvolution(mon));
            mon.CurrentHP = mon.CurrentStats.Health;
        }

        private static void Grow(PokemonInstance mon, Stats before, string nameBefore,
            PokemonSpeciesLibrary library, GrowthReport report)
        {
            // A loop, not an if: a big enough grant can cross more than one threshold at once.
            for (int i = 0; i < MaxEvolutionsPerGrant && TryEvolve(mon, library, out var evolution); i++)
            {
                report.Evolutions.Add(evolution);
            }
            Recompute(mon, library);

            var after = mon.CurrentStats;
            if (after.Attack != before.Attack || after.Health != before.Health || after.Speed != before.Speed)
            {
                report.Gains.Add(new GrowthReport.StatGain(mon, nameBefore, before, after));
            }
        }

        /// <summary>One evolution step, if the mon has earned the EXP and its species has somewhere
        /// to go. The passive is re-resolved at the new species' stage — a passive persists through
        /// evolution and only its magnitude scales (design doc §8, content-schema.md §1).</summary>
        private static bool TryEvolve(PokemonInstance mon, PokemonSpeciesLibrary library, out Evolution evolution)
        {
            evolution = default;
            if (ExpSinceEvolution(mon) < ExpPerEvolution)
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

        private static string NameOf(PokemonInstance mon, PokemonSpeciesLibrary library)
        {
            if (!string.IsNullOrEmpty(mon.Nickname))
            {
                return mon.Nickname;
            }
            var species = library?.GetById(mon.SpeciesId);
            return species != null ? species.DisplayName : mon.SpeciesId.ToString();
        }
    }
}
