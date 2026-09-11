using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Plain-C# run state (design doc §4, §7) — no MonoBehaviour/scene dependency so it
    /// can be unit-tested and, later, serialized straight to local save. Owned by RunBootstrapper
    /// in Gameplay, mutated by the Meta resolvers.</summary>
    public sealed class RunState
    {
        /// <summary>Ordered active line-up — front (index 0) is the Lead, index 1 is the Support.
        /// Only these two are ever mechanically active (design doc §7); reordering is Phase 1.</summary>
        public List<PokemonInstance> LineUp = new List<PokemonInstance>();

        /// <summary>Caught/adopted mons not currently in the active line-up.</summary>
        public List<PokemonInstance> Box = new List<PokemonInstance>();

        public int Money;

        /// <summary>The run's life total (design doc §4). Hitting 0 ends the run.</summary>
        public int Morale = 3;

        /// <summary>Current Location's node sequence, in order.</summary>
        public List<LocationNodeState> Nodes = new List<LocationNodeState>();

        public int CurrentNodeIndex;

        /// <summary>Fixed per-run seed feeding both encounter generation and battle simulation, so
        /// a run is fully reproducible end to end (design doc §10.5).</summary>
        public int RunSeed;

        /// <summary>Set by a Camp node, consumed by the next PvE fight's line-up assembly
        /// (design doc §5.1: "temporary buff for the next fight"). 0 = no active buff.</summary>
        public float NextBattleAttackBonusPercent;

        public bool IsRunOver => Morale <= 0;

        public LocationNodeState CurrentNode =>
            CurrentNodeIndex >= 0 && CurrentNodeIndex < Nodes.Count ? Nodes[CurrentNodeIndex] : null;

        public bool HasNextNode => CurrentNodeIndex + 1 < Nodes.Count;

        public void AdvanceToNextNode()
        {
            if (CurrentNode != null)
            {
                CurrentNode.Cleared = true;
            }
            if (HasNextNode)
            {
                CurrentNodeIndex++;
            }
        }
    }
}
