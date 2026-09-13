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
    /// **EXP is a plain count of wins, and every point is +1 Attack *or* +1 Health.** A Charmander is
    /// 3/4/1; win a fight and it's 3/5/1 or 4/4/1, drawn against its own growth value
    /// (Pets.Data.StatGrowth). There is no level, no curve and no rising cost — the species' tier
    /// (Pets.Data.SpeciesTier) says how strong it starts, and EXP says how far it has come since.
    ///
    /// **Twelve points is an evolution**, at <see cref="ExpPerEvolution"/>, and an evolution is worth
    /// a flat +3 Attack and +3 Health (StatGrowth.AttackPerEvolution) — **the species evolved into
    /// contributes nothing to the mon's stats**. A mon keeps growing from the tier line of the
    /// species it started as, for its whole life. At about four points a Location, that puts a
    /// three-stage line's first evolution in the third Location and its last in the sixth, which is
    /// the mainline rhythm (RunProgression).
    ///
    /// **EXP is lifetime, and never resets.** The nth evolution lands the moment it reaches
    /// `n × ExpPerEvolution`. Keeping one running total means a mon's whole history is one number —
    /// which is what <see cref="ApplyCatchUp"/> compares, and what a save layer can rebuild a mon
    /// from.
    ///
    /// **Stats are derived, never accumulated** — ADR 0005's rule, unchanged.
    /// `StatGrowth.AtExp(baseFormOf(mon), mon.InstanceId, mon.Exp, mon.TimesEvolved)` is always the
    /// whole story, so the same EXP gives the same stats however it arrived.
    ///
    /// **Nobody in the party falls hopelessly behind.** <see cref="ApplyCatchUp"/> keeps every mon in
    /// the *line-up* within <see cref="CatchUpExpGap"/> points of the run's most-experienced, so a mon
    /// caught late and put straight to work is still worth using. It levels experience, not power: a
    /// caught Caterpie gets the same EXP as the Charizard beside it and is still a Caterpie, so tiers
    /// keep meaning something.
    ///
    /// **The Box is not caught up, because a mon that didn't fight doesn't grow.** EXP is only ever
    /// earned by the mons the player fielded (BattleRewardResolver, CampResolver), and catch-up used
    /// to quietly hand the same growth to everything in storage — which made benching a mon free and
    /// the party choice mean nothing. A Box mon keeps the EXP it had; the moment it is moved into the
    /// line-up, the next grant's catch-up brings it back within <see cref="CatchUpExpGap"/>, so
    /// nothing in the Box is ever ruined by being left there.</summary>
    public static class ExperienceResolver
    {
        /// <summary>EXP between one evolution and the next. At RunProgression.ExpPerBadge a
        /// Location, a three-stage line evolves in the third Location and finishes in the sixth —
        /// deliberately most of a run, so a line-up is still changing shape at the seventh badge.</summary>
        public const int ExpPerEvolution = 12;

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

        /// <summary>EXP this mon has earned since it last evolved — its lifetime total less what the
        /// evolutions behind it cost. A progress readout, not a stat input: stats come from the
        /// lifetime total (see the class doc).</summary>
        public static int ExpSinceEvolution(PokemonInstance mon) =>
            Math.Max(0, mon.Exp - ExpPerEvolution * Math.Max(0, mon.TimesEvolved));

        /// <summary>The lifetime EXP at which this mon's next evolution lands.</summary>
        public static int ExpAtNextEvolution(PokemonInstance mon) =>
            ExpPerEvolution * (Math.Max(0, mon.TimesEvolved) + 1);

        /// <summary>EXP still to earn before this mon's next evolution, or null if it has nowhere to
        /// go — what an "Evo in 3" readout needs.</summary>
        public static int? ExpToNextEvolution(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            CanEverEvolve(mon, library) ? Math.Max(0, ExpAtNextEvolution(mon) - mon.Exp) : (int?)null;

        /// <summary>The lifetime EXP a mon of this species must have simply to exist: an evolved
        /// species is the record of the evolutions that produced it, each of which cost
        /// <see cref="ExpPerEvolution"/>. A Charmeleon can never be below 12.</summary>
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

        /// <summary>The species at the root of <paramref name="species"/>' evolution chain — what a
        /// mon of it started life as, and therefore the only species that contributes anything to its
        /// stats (Pets.Data.StatGrowth). Counted within the roster: Pikachu is its own base form here
        /// even though Pichu exists, because Pichu isn't curated.</summary>
        public static PokemonSpeciesDefinitionAsset BaseFormOf(PokemonSpeciesDefinitionAsset species,
            PokemonSpeciesLibrary library)
        {
            if (library == null || species == null)
            {
                return species;
            }

            var current = species;
            // Bounded: a malformed chain that loops must not hang the game.
            for (int depth = 0; depth < MaxEvolutionsPerGrant; depth++)
            {
                var previous = library.AllSpecies.FirstOrDefault(
                    s => s != null && s.EvolvesInto != null && s.EvolvesInto.Id == current.Id);
                if (previous == null)
                {
                    break;
                }
                current = previous;
            }
            return current;
        }

        /// <summary>What <paramref name="mon"/>'s stats would be carrying <paramref name="exp"/>
        /// lifetime EXP with <paramref name="evolutions"/> behind it — for a screen that wants to
        /// show what a choice would do before the player commits to it.</summary>
        public static Stats StatsFor(PokemonInstance mon, int exp, int evolutions, PokemonSpeciesLibrary library)
        {
            var baseForm = BaseFormOf(library?.GetById(mon.SpeciesId), library);
            return baseForm == null
                ? mon.CurrentStats
                : StatGrowth.AtExp(baseForm, mon.InstanceId, exp, evolutions);
        }

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
        /// <see cref="MinExpFor"/>): a Charmeleon handed out at 2 EXP is really a 12-EXP mon that
        /// spent all of it getting here, so it is raised to 12 rather than being made a Charmeleon
        /// that has somehow never earned anything.</summary>
        public static PokemonInstance CreateAtExp(PokemonSpeciesDefinitionAsset species, string instanceId,
            int exp, PokemonSpeciesLibrary library)
        {
            // Built from the root of the chain and evolved forward, even when an evolved species was
            // asked for: a mon's stats are its *base form's* line plus what it earned (ADR 0009), so
            // a Charmeleon handed straight to the factory would wear stats it could never have grown
            // into. Rooting it here is what keeps "caught as a Charmeleon" and "raised into one" the
            // same mon.
            var baseForm = BaseFormOf(species, library) ?? species;
            var mon = PokemonInstanceFactory.Create(baseForm, instanceId);
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

        /// <summary>The least EXP any mon *fighting for* this run should have: the most-experienced
        /// mon it owns anywhere, less <see cref="CatchUpExpGap"/>.
        ///
        /// Measured across the Box as well as the line-up even though only the line-up is raised to
        /// it: benching the run's best mon shouldn't lower the bar a newcomer joins at, which is what
        /// this number is also used for (CatchResolver.Catch).</summary>
        public static int CatchUpExp(RunState state)
        {
            int top = 0;
            foreach (var mon in state.LineUp.Concat(state.Box))
            {
                top = Math.Max(top, mon.Exp);
            }
            return Math.Max(0, top - CatchUpExpGap);
        }

        /// <summary>Raises every mon **in the line-up** to <see cref="CatchUpExp"/>. Called after
        /// anything that grows the run's most-experienced mon.
        ///
        /// The Box is deliberately left out: only the party earns from a fight (see the class doc),
        /// and raising storage alongside it is the same thing as paying mons that never battled.</summary>
        public static GrowthReport ApplyCatchUp(RunState state, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            int floor = CatchUpExp(state);
            foreach (var mon in state.LineUp)
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

            mon.CurrentStats = StatGrowth.AtExp(BaseFormOf(species, library), mon.InstanceId, mon.Exp, mon.TimesEvolved);
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
            if (mon.Exp < ExpAtNextEvolution(mon))
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
