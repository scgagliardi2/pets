using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Ordered creature slots, front = index 0. See battle-sim-spec.md §2.</summary>
    public sealed class TeamState
    {
        public List<CreatureState> Slots = new List<CreatureState>();

        public CreatureState Front => Slots.Count > 0 ? Slots[0] : null;

        public bool IsEmpty => Slots.Count == 0;

        /// <summary>Inserts at the front slot — used when a Summon effect fills a just-vacated slot.</summary>
        public void InsertFront(CreatureState creature)
        {
            Slots.Insert(0, creature);
        }
    }
}
