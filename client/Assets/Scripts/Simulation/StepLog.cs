using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>The whole result of a precomputed fight: what happened, who won, and the state it
    /// ended in.</summary>
    public sealed class StepLog
    {
        public List<StepEvent> Events = new List<StepEvent>();
        public BattleOutcome Outcome;

        /// <summary>The battle's combatants as the last Step left them.
        ///
        /// Needed because the simulator works on copies (see BattleCombatant): a caller that used
        /// to read final HP straight off the PokemonInstances it passed in now has to be told.
        /// This is what a Gym result is written back from — who survived, at what HP, for whatever
        /// the run layer decides to carry forward — and each combatant's Source points at the mon
        /// it came from.
        ///
        /// Note the line-ups here hold only the mons still standing; the fainted ones were removed
        /// as they fell, in the order the Faint events record.</summary>
        public BattleState FinalState;
    }
}
