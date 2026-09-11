using System.Collections.Generic;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// For Gym/PvP fights (docs/battle-sim-spec.md §7) — no catching is possible, so the whole
    /// fight can be computed up front and simply played back. This is also exactly what a
    /// server-authoritative async-PvP result needs (design doc §16): the same function, called
    /// server-side once the TypeScript port exists.
    /// </summary>
    public static class PrecomputedStepLogRunner
    {
        public static (List<StepEvent> Log, BattleOutcome Outcome) Run(BattleState state)
        {
            var fullLog = new List<StepEvent>();

            while (!state.IsOver)
            {
                if (state.StepNumber >= BattleConfig.StepCap || fullLog.Count >= BattleConfig.EventCap)
                {
                    fullLog.Add(new StepEvent { StepNumber = state.StepNumber, Kind = StepEventKind.BattleEnd, Outcome = BattleOutcome.Draw });
                    return (fullLog, BattleOutcome.Draw);
                }

                fullLog.AddRange(BattleSimulator.AdvanceStep(state));
            }

            var outcome = state.DetermineOutcomeIfOver() ?? BattleOutcome.Draw;
            fullLog.Add(new StepEvent { StepNumber = state.StepNumber, Kind = StepEventKind.BattleEnd, Outcome = outcome });
            return (fullLog, outcome);
        }
    }

    /// <summary>
    /// For PvE fights (docs/battle-sim-spec.md §7) — a successful catch can remove a mon from the
    /// enemy line-up between Steps, so Steps are generated one at a time rather than all up front.
    /// The catching interaction itself (drag-and-drop, catch-chance formula) lives in Gameplay and
    /// is expected to mutate <c>state.SideB</c> (or whichever side is the wild encounter) between
    /// calls to <see cref="AdvanceOnce"/> — this runner has no opinion about that, it just exposes
    /// "give me the next Step."
    /// </summary>
    public static class OnDemandStepRunner
    {
        public static (List<StepEvent> Events, bool IsOver, BattleOutcome? Outcome) AdvanceOnce(BattleState state)
        {
            if (state.IsOver)
            {
                return (new List<StepEvent>(), true, state.DetermineOutcomeIfOver());
            }

            var events = BattleSimulator.AdvanceStep(state);
            bool isOver = state.IsOver || state.StepNumber >= BattleConfig.StepCap;
            BattleOutcome? outcome = null;
            if (isOver)
            {
                outcome = state.DetermineOutcomeIfOver() ?? BattleOutcome.Draw;
                events.Add(new StepEvent { StepNumber = state.StepNumber, Kind = StepEventKind.BattleEnd, Outcome = outcome });
            }
            return (events, isOver, outcome);
        }
    }
}
