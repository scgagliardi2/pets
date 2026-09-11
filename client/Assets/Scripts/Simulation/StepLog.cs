using System.Collections.Generic;

namespace Pets.Simulation
{
    public sealed class StepLog
    {
        public List<StepEvent> Events = new List<StepEvent>();
        public BattleOutcome Outcome;
    }
}
