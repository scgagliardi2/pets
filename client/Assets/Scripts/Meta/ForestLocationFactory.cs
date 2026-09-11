using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Builds Phase 0's one hand-authored Location (PLAN.md §6, Phase 0): a Forest, biased
    /// toward Grass/Bug/Flying wild encounters (design doc §4 table), as a linear node sequence.
    /// Branching paths and the mandatory Gym node are Phase 1 (design doc §5, §14).</summary>
    public static class ForestLocationFactory
    {
        public static readonly PokemonType[] TypeBias = { PokemonType.Grass, PokemonType.Bug, PokemonType.Flying };

        public static List<LocationNodeState> BuildNodes()
        {
            return new List<LocationNodeState>
            {
                new LocationNodeState { Id = "forest-1", Type = NodeType.PvE },
                new LocationNodeState { Id = "forest-2", Type = NodeType.PvE },
                new LocationNodeState { Id = "forest-3", Type = NodeType.Camp },
                new LocationNodeState { Id = "forest-4", Type = NodeType.PvE },
                new LocationNodeState { Id = "forest-5", Type = NodeType.PvE }
            };
        }
    }
}
