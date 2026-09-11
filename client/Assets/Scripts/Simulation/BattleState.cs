using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Two ordered line-ups. Position 0 of a line-up is its Lead, position 1 its
    /// Support, everyone else dormant (battle-sim-spec.md §2).</summary>
    public sealed class BattleState
    {
        public List<PokemonInstance> LineUpA = new List<PokemonInstance>();
        public List<PokemonInstance> LineUpB = new List<PokemonInstance>();
        public int StepNumber;

        public PokemonInstance LeadA => LineUpA.Count > 0 ? LineUpA[0] : null;
        public PokemonInstance SupportA => LineUpA.Count > 1 ? LineUpA[1] : null;
        public PokemonInstance LeadB => LineUpB.Count > 0 ? LineUpB[0] : null;
        public PokemonInstance SupportB => LineUpB.Count > 1 ? LineUpB[1] : null;

        public List<PokemonInstance> LineUp(Side side) => side == Side.A ? LineUpA : LineUpB;
    }
}
