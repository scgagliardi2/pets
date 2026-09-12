using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Precomputed Step-log runner for Gym/PvP fights (battle-sim-spec.md §7) — no
    /// catching is possible in these fights, so the whole Step log can be computed up front and
    /// handed to the UI for playback. Calls the same AdvanceStep as OnDemandStepRunner.</summary>
    public static class PrecomputedStepLogRunner
    {
        /// <summary>Convenience entry from the run's own roster: copies each line-up into fresh
        /// combatants and runs. Use the combatant overload instead when anything has to be applied
        /// at line-up assembly — a Camp buff, type synergy (battle-sim-spec.md §8) — since those
        /// belong on the combatants and must not touch the roster.</summary>
        public static StepLog Run(IReadOnlyList<PokemonInstance> lineUpA, IReadOnlyList<PokemonInstance> lineUpB, int seed) =>
            Run(BattleCombatant.FromLineUp(lineUpA), BattleCombatant.FromLineUp(lineUpB), seed);

        public static StepLog Run(List<BattleCombatant> lineUpA, List<BattleCombatant> lineUpB, int seed)
        {
            var state = new BattleState { LineUpA = lineUpA, LineUpB = lineUpB };
            var rng = new DeterministicRandom(seed);
            var log = new StepLog();

            while (state.LineUpA.Count > 0 && state.LineUpB.Count > 0)
            {
                if (state.StepNumber >= BattleConfig.StepCap || log.Events.Count >= BattleConfig.EventCap)
                {
                    log.Outcome = BattleOutcome.Draw;
                    log.Events.Add(new StepEvent { Step = state.StepNumber, Kind = StepEventKind.BattleEnd, Outcome = BattleOutcome.Draw });
                    log.FinalState = state;
                    return log;
                }
                log.Events.AddRange(BattleSimulator.AdvanceStep(state, rng));
            }

            log.Outcome = BattleSimulator.DetermineOutcome(state);
            log.Events.Add(new StepEvent { Step = state.StepNumber, Kind = StepEventKind.BattleEnd, Outcome = log.Outcome });
            log.FinalState = state;
            return log;
        }
    }

    /// <summary>On-demand Step runner for PvE fights (battle-sim-spec.md §7) — advances one Step
    /// at a time so the catching interaction layer (Gameplay, not built yet) can mutate the enemy
    /// line-up at a Step boundary before the next call. Calls the same AdvanceStep as
    /// PrecomputedStepLogRunner; the simulator itself has no special "catch" concept.</summary>
    public sealed class OnDemandStepRunner
    {
        private readonly DeterministicRandom rng;

        public BattleState State { get; }

        /// <summary>Convenience entry from the run's own roster — see the note on the precomputed
        /// runner's equivalent for when to build the combatants yourself instead.</summary>
        public OnDemandStepRunner(IReadOnlyList<PokemonInstance> lineUpA, IReadOnlyList<PokemonInstance> lineUpB, int seed)
            : this(BattleCombatant.FromLineUp(lineUpA), BattleCombatant.FromLineUp(lineUpB), seed)
        {
        }

        public OnDemandStepRunner(List<BattleCombatant> lineUpA, List<BattleCombatant> lineUpB, int seed)
        {
            State = new BattleState { LineUpA = lineUpA, LineUpB = lineUpB };
            rng = new DeterministicRandom(seed);
        }

        public bool IsBattleOver =>
            State.LineUpA.Count == 0 || State.LineUpB.Count == 0 || State.StepNumber >= BattleConfig.StepCap;

        public BattleOutcome? Outcome => IsBattleOver ? BattleSimulator.DetermineOutcome(State) : (BattleOutcome?)null;

        /// <summary>Advances exactly one Step. Returns no events if the battle is already over —
        /// callers should check IsBattleOver first rather than relying on an empty result.</summary>
        public List<StepEvent> NextStep()
        {
            if (IsBattleOver)
            {
                return new List<StepEvent>();
            }
            return BattleSimulator.AdvanceStep(State, rng);
        }
    }
}
