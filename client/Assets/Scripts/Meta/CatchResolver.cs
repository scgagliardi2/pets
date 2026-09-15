using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Stubbed catching (PLAN.md §6, Phase 0): "pick 1 from defeated" rather than the
    /// full drag-a-Pokéball Step-boundary throw system (design doc §12.1, Phase 1).</summary>
    public static class CatchResolver
    {
        /// <summary>The wild mons that fainted during the fight, as run-level instances.
        ///
        /// Taken from the log's Faint events rather than by testing the line-up's own HP: a battle
        /// runs on copies (Pets.Simulation.BattleCombatant), so the instances handed to the runner
        /// come back undamaged and "which of these is dead" is no longer a question the roster can
        /// answer. The events are the record of what happened, which is the right thing to ask.</summary>
        public static List<PokemonInstance> GetDefeated(
            IReadOnlyList<PokemonInstance> wildLineUp, StepLog log, Side wildSide) =>
            GetDefeated(wildLineUp, log.Events, wildSide);

        /// <summary>Same as the StepLog overload, against a raw event list — for a caller (the
        /// Battle screen) that accumulates a fight's events Step by Step rather than holding a
        /// full StepLog.</summary>
        public static List<PokemonInstance> GetDefeated(
            IReadOnlyList<PokemonInstance> wildLineUp, IEnumerable<StepEvent> events, Side wildSide)
        {
            var faintedIds = new HashSet<string>();
            foreach (var evt in events)
            {
                if (evt.Kind == StepEventKind.Faint && evt.SourceSide == wildSide)
                {
                    faintedIds.Add(evt.SourceInstanceId);
                }
            }

            var defeated = new List<PokemonInstance>();
            foreach (var mon in wildLineUp)
            {
                if (faintedIds.Contains(mon.InstanceId))
                {
                    defeated.Add(mon);
                }
            }
            return defeated;
        }

        /// <summary>Adds a fresh, full-health copy of the defeated mon to the Box, carrying its own
        /// EXP or the run's catch-up EXP, whichever is higher — a catch is meant to be usable straight
        /// away (ExperienceResolver.CatchUpExp).</summary>
        public static void Catch(RunState state, PokemonInstance defeated, PokemonSpeciesLibrary library)
        {
            AddToBox(state, defeated, library, expCap: null);
        }

        /// <summary>What a thrown ball did, for the screen to narrate.
        ///
        /// <see cref="NotThrown"/> is deliberately the zero value: a default ThrowResult is one
        /// nothing has happened to, and the alternative — a default that reads as a successful
        /// catch — is a trap for every caller that declares a result before a loop or a branch
        /// fills it in.</summary>
        public enum ThrowOutcome
        {
            /// <summary>Nothing happened and no ball was spent — no ball of that tier, or no valid
            /// target. Its own outcome so a caller never has to infer "did this cost me a ball".</summary>
            NotThrown,

            /// <summary>The mon is in the Box and out of the fight.</summary>
            Caught,

            /// <summary>It broke free. The ball is gone; the fight carries on.</summary>
            BrokeFree
        }

        public readonly struct ThrowResult
        {
            public readonly ThrowOutcome Outcome;

            /// <summary>The mon added to the Box, on a catch. Null otherwise.</summary>
            public readonly PokemonInstance Caught;

            /// <summary>Events the throw added to the fight — the Caught event and any promotion
            /// behind it (BattleSimulator.RemoveCaught). Empty unless the throw landed.</summary>
            public readonly List<StepEvent> Events;

            public bool Landed => Outcome == ThrowOutcome.Caught;

            /// <summary>Whether a ball was consumed. A break-free costs one; a refused throw
            /// doesn't (design doc §12.1: "on failure the ball is consumed").</summary>
            public bool SpentBall => Outcome != ThrowOutcome.NotThrown;

            public ThrowResult(ThrowOutcome outcome, PokemonInstance caught, List<StepEvent> events)
            {
                Outcome = outcome;
                Caught = caught;
                Events = events ?? new List<StepEvent>();
            }
        }

        /// <summary>Resolves a ball thrown at the wild Lead at a Step boundary (design doc §12.1).
        /// The whole mechanic in one place: spend the ball, roll against CatchOdds using the
        /// fight's own RNG, and on a hit take the mon out of the line-up and put it in the Box.
        ///
        /// Only ever targets the enemy **Lead**, per the design doc — Support and further-back mons
        /// aren't engaged and aren't valid targets until they're promoted. The caller passes the
        /// wild side rather than it being assumed, since which side is wild is the battle screen's
        /// knowledge, not this resolver's.
        ///
        /// Caller's responsibility, deliberately not done here: checking this is a PvE fight at all.
        /// Gym and PvP fights are trainer battles where catching isn't offered (design doc §12.1),
        /// and that's a property of the node, which Meta can't see from a BattleState.</summary>
        public static ThrowResult TryCatch(RunState state, BattleState battle, Side wildSide,
            BallTier tier, DeterministicRandom rng, PokemonSpeciesLibrary library)
        {
            var lineUp = battle.LineUp(wildSide);
            var target = lineUp.Count > 0 ? lineUp[0] : null;
            if (target == null || !target.IsAlive || !state.Balls.TrySpend(tier))
            {
                return new ThrowResult(ThrowOutcome.NotThrown, null, null);
            }

            if (!CatchOdds.Roll(tier, target, rng))
            {
                return new ThrowResult(ThrowOutcome.BrokeFree, null, null);
            }

            var events = BattleSimulator.RemoveCaught(battle, wildSide, target);
            // Source is the run-level record the combatant was copied from (BattleCombatant.Source).
            // A wild encounter always has one; a combatant built straight into a fixture may not, in
            // which case there's nothing to add and the mon still correctly leaves the fight.
            var caught = target.Source != null
                ? AddToBox(state, target.Source, library, BallCatalog.ExpCap(tier))
                : null;
            return new ThrowResult(ThrowOutcome.Caught, caught, events);
        }

        /// <summary>Adds a mon to the Box at the EXP a catch should yield, and returns it.
        ///
        /// The floor is the run's catch-up EXP so a catch is usable straight away
        /// (ExperienceResolver.CatchUpExp); the ceiling, when the ball tier has one, is design doc
        /// §12.1's under-levelled catch — a weak ball can land a strong mon but not deliver it at
        /// full strength. The cap is applied last so it genuinely binds: a Poké Ball yields a
        /// capped mon even in a run whose catch-up floor is higher, which is the whole point of
        /// carrying better balls.</summary>
        private static PokemonInstance AddToBox(RunState state, PokemonInstance source,
            PokemonSpeciesLibrary library, int? expCap)
        {
            var species = library.GetById(source.SpeciesId);
            if (species == null)
            {
                return null;
            }
            string instanceId = $"box-{species.Id}-{state.Box.Count}";
            int exp = System.Math.Max(source.Exp, ExperienceResolver.CatchUpExp(state));
            if (expCap.HasValue)
            {
                exp = System.Math.Min(exp, expCap.Value);
            }
            var caught = ExperienceResolver.CreateAtExp(species, instanceId, exp, library);
            state.Box.Add(caught);
            return caught;
        }
    }
}
