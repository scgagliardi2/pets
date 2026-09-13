using System;
using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>EXP, levels, growth and evolution (design doc §7 "grow them via EXP/level", §12.3)
    /// — the one place a mon's stats change outside a battle. See ADR 0007, which replaces ADR 0005's
    /// flat counter with this.
    ///
    /// **EXP buys levels on a rising curve.** Going from level L to L+1 costs
    /// <see cref="BaseExpPerLevel"/> + <see cref="ExpPerLevelIncrease"/> × L. What a fight pays scales
    /// with the level of what was beaten (BattleRewardResolver), so the curve and the rewards rise
    /// together and a run gains roughly RunProgression.LevelsPerBadge levels per Location all the way
    /// to the eighth badge. The constants were fitted by simulating whole runs, not picked by hand.
    ///
    /// **Level is derived, never stored.** A mon stores only its total EXP; its level is read off the
    /// curve, and its stats off the level (Pets.Data.StatGrowth). `StatGrowth.AtLevel(species,
    /// LevelOf(mon))` is always the whole story, so the same EXP gives the same stats however it
    /// arrived, and an evolution's new base stats apply by simply recomputing.
    ///
    /// **Evolution is gated on level**, at <see cref="EvolutionLevels"/>, indexed by how many times
    /// this mon has evolved — so a curated base form whose real pre-evolution isn't in the roster
    /// (Pikachu) still evolves at the first threshold.
    ///
    /// **Nobody falls hopelessly behind.** <see cref="ApplyCatchUp"/> keeps every mon the run owns
    /// within <see cref="CatchUpLevelGap"/> levels of its strongest, so a mon caught late or left in
    /// the Box is still worth using.</summary>
    public static class ExperienceResolver
    {
        /// <summary>EXP to go from level 1 to 2 is this plus <see cref="ExpPerLevelIncrease"/>.</summary>
        public const int BaseExpPerLevel = 2;

        /// <summary>How much more each successive level costs than the one before.</summary>
        public const int ExpPerLevelIncrease = 2;

        /// <summary>A ceiling so the curve can't be walked forever by a runaway grant. An eight-badge
        /// run finishes in the mid-20s.</summary>
        public const int MaxLevel = 100;

        /// <summary>The level a mon evolves at, by how many times it has already evolved: a
        /// three-stage line evolves at 8 and again at 17 — around the third and sixth Locations of an
        /// eight-badge run (RunProgression.BaselineLevel).</summary>
        public static readonly int[] EvolutionLevels = { 8, 17 };

        /// <summary>How far below the run's strongest mon any other mon it owns may fall before
        /// <see cref="ApplyCatchUp"/> raises it.</summary>
        public const int CatchUpLevelGap = 2;

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

        /// <summary>Total EXP at which a mon reaches <paramref name="level"/>.</summary>
        public static int ExpToReachLevel(int level)
        {
            int steps = Math.Max(0, Math.Min(level, MaxLevel) - 1);
            return BaseExpPerLevel * steps + ExpPerLevelIncrease * steps * (steps + 1) / 2;
        }

        /// <summary>EXP it costs to go from <paramref name="level"/> to the next one.</summary>
        public static int ExpForLevelUp(int level) => BaseExpPerLevel + ExpPerLevelIncrease * level;

        public static int LevelForExp(int exp)
        {
            int level = 1;
            while (level < MaxLevel && exp >= ExpToReachLevel(level + 1))
            {
                level++;
            }
            return level;
        }

        public static int LevelOf(PokemonInstance mon) => LevelForExp(mon.Exp);

        /// <summary>EXP earned since the mon reached its current level — with ExpForLevelUp, what a
        /// "12 / 18" progress readout needs.</summary>
        public static int ExpIntoLevel(PokemonInstance mon) => mon.Exp - ExpToReachLevel(LevelOf(mon));

        /// <summary>Whether this mon has somewhere to evolve to at all — false for a final form, and
        /// for the branching lines the content layer leaves unresolved
        /// (PokemonSpeciesDefinitionAsset.EvolvesInto).</summary>
        public static bool CanEverEvolve(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            library != null && library.GetById(mon.SpeciesId)?.EvolvesInto != null
            && mon.TimesEvolved < EvolutionLevels.Length;

        /// <summary>The level this mon next evolves at, or null if it won't.</summary>
        public static int? NextEvolutionLevel(PokemonInstance mon, PokemonSpeciesLibrary library) =>
            CanEverEvolve(mon, library) ? EvolutionLevels[mon.TimesEvolved] : (int?)null;

        /// <summary>Grants EXP and applies the levels and evolutions it earns, in place.</summary>
        public static GrowthReport GrantExp(PokemonInstance mon, int amount, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            if (mon == null || amount <= 0)
            {
                return report;
            }

            int levelBefore = LevelOf(mon);
            string nameBefore = NameOf(mon, library);
            mon.Exp += amount;
            report.ExpGranted = amount;
            Grow(mon, levelBefore, nameBefore, library, report);
            return report;
        }

        /// <summary>Raises a mon to <paramref name="level"/> if it's below it; never lowers one.</summary>
        public static GrowthReport RaiseToLevel(PokemonInstance mon, int level, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            int levelBefore = LevelOf(mon);
            if (levelBefore >= level)
            {
                return report;
            }

            string nameBefore = NameOf(mon, library);
            mon.Exp = ExpToReachLevel(level);
            Grow(mon, levelBefore, nameBefore, library, report);
            return report;
        }

        /// <summary>A mon of <paramref name="species"/> at <paramref name="level"/>, already evolved as
        /// far as that level allows — what a wild encounter, a Gym Leader's team, a catch and a new run
        /// are all built from.
        ///
        /// An evolved species starts with its evolutions already counted (see
        /// <see cref="EvolutionsBefore"/>): a Charmeleon handed out at level 10 has used its first
        /// threshold, so it next evolves at 17, not straight away at 8.</summary>
        public static PokemonInstance CreateAtLevel(PokemonSpeciesDefinitionAsset species, string instanceId,
            int level, PokemonSpeciesLibrary library)
        {
            var mon = PokemonInstanceFactory.Create(species, instanceId);
            mon.TimesEvolved = EvolutionsBefore(species, library);
            mon.Exp = ExpToReachLevel(Math.Max(1, level));
            while (TryEvolve(mon, library, out _))
            {
            }
            Recompute(mon, library);
            return mon;
        }

        /// <summary>How many roster species come before <paramref name="species"/> in its evolution
        /// chain — counted within the roster, not PokeAPI's chain depth, for the same reason
        /// TimesEvolved is: Pikachu is stage 1 in the real chain but has no roster pre-evolution.</summary>
        public static int EvolutionsBefore(PokemonSpeciesDefinitionAsset species, PokemonSpeciesLibrary library)
        {
            if (library == null || species == null)
            {
                return 0;
            }

            int depth = 0;
            var current = species;
            // Bounded: a malformed chain that loops must not hang the game.
            while (depth <= EvolutionLevels.Length)
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

        /// <summary>The lowest level any mon in this run should be at: the strongest mon it owns, less
        /// <see cref="CatchUpLevelGap"/>.</summary>
        public static int CatchUpLevel(RunState state)
        {
            int top = 1;
            foreach (var mon in state.LineUp.Concat(state.Box))
            {
                top = Math.Max(top, LevelOf(mon));
            }
            return Math.Max(1, top - CatchUpLevelGap);
        }

        /// <summary>Raises every mon in the line-up and the Box to <see cref="CatchUpLevel"/>. Called
        /// after anything that grows the run's strongest mon, and whenever a mon joins.</summary>
        public static GrowthReport ApplyCatchUp(RunState state, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            int floor = CatchUpLevel(state);
            foreach (var mon in state.LineUp.Concat(state.Box).ToList())
            {
                report.Merge(RaiseToLevel(mon, floor, library));
            }
            return report;
        }

        /// <summary>Rebuilds CurrentStats (and tops HP back up) from the mon's species and level.
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

            mon.CurrentStats = StatGrowth.AtLevel(species, LevelOf(mon));
            mon.CurrentHP = mon.CurrentStats.Health;
        }

        private static void Grow(PokemonInstance mon, int levelBefore, string nameBefore,
            PokemonSpeciesLibrary library, GrowthReport report)
        {
            // A loop, not an if: a big enough grant can cross both thresholds at once.
            while (TryEvolve(mon, library, out var evolution))
            {
                report.Evolutions.Add(evolution);
            }
            Recompute(mon, library);

            int levelAfter = LevelOf(mon);
            if (levelAfter > levelBefore)
            {
                report.LevelUps.Add(new GrowthReport.LevelUp(mon, nameBefore, levelBefore, levelAfter));
            }
        }

        /// <summary>One evolution step, if the mon has reached the level and its species has somewhere
        /// to go. The passive is re-resolved at the new species' stage — a passive persists through
        /// evolution and only its magnitude scales (design doc §8, content-schema.md §1).</summary>
        private static bool TryEvolve(PokemonInstance mon, PokemonSpeciesLibrary library, out Evolution evolution)
        {
            evolution = default;
            if (mon.TimesEvolved >= EvolutionLevels.Length || LevelOf(mon) < EvolutionLevels[mon.TimesEvolved])
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
