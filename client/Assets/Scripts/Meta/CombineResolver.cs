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
    ///   consumed. It gains **one point of EXP** (<see cref="ExpFor"/>) — the same as winning a fight,
    ///   and whatever the duplicate had earned is *not* carried over: a combine is a dupe sink, not a
    ///   way to launder a second mon's growth. Two fresh Charmanders make a 3/5/1 or a 4/4/1,
    ///   depending on the species' growth draw.
    /// - A combine can't empty the line-up, the same rule every other way of losing a mon obeys
    ///   (RunState.CanReleaseMon).
    ///
    /// Separate from RunState because it grants EXP, which needs the species library to recompute
    /// stats and follow an evolution — RunState is deliberately library-free.</summary>
    public static class CombineResolver
    {
        /// <summary>EXP a combine is worth to the survivor: one point, the same as a win
        /// (BattleRewardResolver.ExpPerWin), so a duplicate is worth exactly the fight you didn't have
        /// to have. Takes the survivor because whether a dupe should be worth more to a mon that has
        /// already grown is a balancing question, not a settled one.</summary>
        public static int ExpFor(PokemonInstance survivor) => BattleRewardResolver.ExpPerWin;

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

            // What the survivor will actually be afterwards. The preview has to follow the
            // evolution when this point is the one that earns it, rather than quote a stat line the
            // mon will never wear — and it can't just say "+1 Attack", since which stat the point
            // buys is the species' own draw (Pets.Data.StatGrowth).
            int expAfter = survivor.Exp + ExpFor(survivor);
            var evolvesInto = ExperienceResolver.NextEvolution(survivor, library);
            bool evolves = expAfter >= ExperienceResolver.ExpAtNextEvolution(survivor) && evolvesInto != null;
            var statsAfter = ExperienceResolver.StatsFor(survivor, expAfter,
                survivor.TimesEvolved + (evolves ? 1 : 0), library);
            string outcome = evolves
                ? $"the other evolves into {evolvesInto.DisplayName} at {GrowthReport.Line(statsAfter)}"
                : $"the other grows to {GrowthReport.Line(statsAfter)}";
            return new Eligibility(true,
                $"Combine two {name}?\nOne is consumed; {outcome}. This cannot be undone.");
        }

        /// <summary>Consumes the mon at <paramref name="fromIndex"/> into the one at
        /// <paramref name="toIndex"/>, paying it a point of EXP. Returns what grew. Does nothing and returns
        /// an empty report if <see cref="CanCombine"/> wouldn't allow it.</summary>
        public static GrowthReport Combine(RunState state, RosterGroup fromGroup, int fromIndex,
            RosterGroup toGroup, int toIndex, PokemonSpeciesLibrary library)
        {
            if (!CanCombine(state, fromGroup, fromIndex, toGroup, toIndex, library).Allowed)
            {
                return new GrowthReport();
            }

            TryResolve(state, fromGroup, fromIndex, toGroup, toIndex, out _, out var survivor);

            // Removed before the EXP is granted, so the run is never momentarily holding both the
            // consumed mon and a grown survivor — the Team screen redraws off this state.
            state.CollectionFor(fromGroup).RemoveAt(fromIndex);
            var report = ExperienceResolver.GrantExp(survivor, ExpFor(survivor), library);
            ExperienceResolver.ApplyCatchUp(state, library);
            return report;
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
