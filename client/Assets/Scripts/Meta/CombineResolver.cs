using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Combining duplicates (design doc §12.3): "Combine 2 of the same mon to instantly
    /// grant EXP to one of them (consumes the duplicate)" — the thing a run does with the dupes
    /// catching inevitably produces.
    ///
    /// Rules, all enforced here rather than in the Team screen that offers the gesture:
    /// - Both mons must be the **same species**. Not "the same line" — an Ivysaur and a Bulbasaur
    ///   are different mons with different stats, and merging across stages would need a rule for
    ///   which one survives that the design doc doesn't give.
    /// - The mon that **survives is the target** (the one dropped onto); the one dragged is
    ///   consumed. Its own EXP is not carried over — a combine is worth a flat
    ///   <see cref="ExpGranted"/>, the same whether the duplicate was fresh or veteran. That's the
    ///   simple reading of "grant EXP to one of them", and it stays a dupe sink rather than a way
    ///   to launder a whole second mon's growth.
    /// - A combine can't empty the line-up, the same rule every other way of losing a mon obeys
    ///   (RunState.CanReleaseMon).
    ///
    /// Separate from RunState because it grants EXP, which needs the species library to recompute
    /// stats and follow an evolution — RunState is deliberately library-free.</summary>
    public static class CombineResolver
    {
        /// <summary>EXP the surviving mon gains. Two battle wins' worth: enough that feeding a
        /// duplicate is clearly better than carrying it, without being a shortcut past playing.</summary>
        public const int ExpGranted = 2;

        /// <summary>What a combine would do, or why it can't happen — so the Team screen can put
        /// the reason in front of the player instead of a card that snaps back with no
        /// explanation.</summary>
        public readonly struct Eligibility
        {
            public readonly bool Allowed;

            /// <summary>Player-facing sentence: what will happen, or why it won't.</summary>
            public readonly string Reason;

            public Eligibility(bool allowed, string reason)
            {
                Allowed = allowed;
                Reason = reason;
            }
        }

        public static Eligibility CanCombine(RunState state, RosterGroup fromGroup, int fromIndex,
            RosterGroup toGroup, int toIndex, PokemonSpeciesLibrary library)
        {
            if (!TryResolve(state, fromGroup, fromIndex, toGroup, toIndex, out var consumed, out var survivor))
            {
                return new Eligibility(false, "Those two can't be combined.");
            }

            var species = library?.GetById(survivor.SpeciesId);
            string name = species != null ? species.DisplayName : survivor.SpeciesId.ToString();

            if (consumed.SpeciesId != survivor.SpeciesId)
            {
                var other = library?.GetById(consumed.SpeciesId);
                string otherName = other != null ? other.DisplayName : consumed.SpeciesId.ToString();
                return new Eligibility(false, $"{otherName} and {name} aren't the same Pokemon.\nOnly duplicates can be combined.");
            }

            // The consumed mon leaves the run for good, so this is the release rule, applied to the
            // slot it's leaving from.
            if (!state.CanReleaseMon(fromGroup, fromIndex))
            {
                return new Eligibility(false, $"{name} is the last mon in your party.\nYou can't be left with none.");
            }

            return new Eligibility(true,
                $"Combine two {name}?\nOne is consumed; the other gains {ExpGranted} EXP. This cannot be undone.");
        }

        /// <summary>Consumes the mon at <paramref name="fromIndex"/> into the one at
        /// <paramref name="toIndex"/>, granting it <see cref="ExpGranted"/>. Returns the evolutions
        /// that EXP set off (see ExperienceResolver.GrantExp) — combining is the fastest way to
        /// reach an evolution threshold, so this is the common way a player sees one. Does nothing
        /// and returns an empty list if <see cref="CanCombine"/> wouldn't allow it.</summary>
        public static List<ExperienceResolver.Evolution> Combine(RunState state, RosterGroup fromGroup, int fromIndex,
            RosterGroup toGroup, int toIndex, PokemonSpeciesLibrary library)
        {
            if (!CanCombine(state, fromGroup, fromIndex, toGroup, toIndex, library).Allowed)
            {
                return new List<ExperienceResolver.Evolution>();
            }

            TryResolve(state, fromGroup, fromIndex, toGroup, toIndex, out _, out var survivor);

            // Removed before the EXP is granted, so the run is never momentarily holding both the
            // consumed mon and a grown survivor — the Team screen redraws off this state.
            state.CollectionFor(fromGroup).RemoveAt(fromIndex);
            return ExperienceResolver.GrantExp(survivor, ExpGranted, library);
        }

        /// <summary>Resolves two slot addresses to the two mons, or false if either doesn't point
        /// at one (or if both point at the same mon — combining a card with itself is a drop onto
        /// its own slot, which means "nothing happened").</summary>
        private static bool TryResolve(RunState state, RosterGroup fromGroup, int fromIndex,
            RosterGroup toGroup, int toIndex, out PokemonInstance consumed, out PokemonInstance survivor)
        {
            consumed = null;
            survivor = null;

            var from = state.CollectionFor(fromGroup);
            var to = state.CollectionFor(toGroup);
            if (fromIndex < 0 || fromIndex >= from.Count || toIndex < 0 || toIndex >= to.Count)
            {
                return false;
            }
            if (fromGroup == toGroup && fromIndex == toIndex)
            {
                return false;
            }

            consumed = from[fromIndex];
            survivor = to[toIndex];
            return true;
        }
    }
}
