using System.Collections.Generic;

namespace Pets.Simulation
{
    public sealed class BattleLog
    {
        public List<BattleEvent> Events = new List<BattleEvent>();
        public BattleOutcome Outcome;
    }
}
