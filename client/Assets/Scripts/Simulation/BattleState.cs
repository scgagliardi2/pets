using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Two ordered line-ups of battle combatants. Position 0 of a line-up is its Lead,
    /// position 1 its Support, everyone else dormant (battle-sim-spec.md §2).
    ///
    /// Holds BattleCombatants, not PokemonInstances: this whole object is throwaway per-battle
    /// state, and the simulator mutates it freely. Build one through a runner (BattleRunner.cs)
    /// rather than by hand, so the copy from the run's roster actually happens.</summary>
    public sealed class BattleState
    {
        public List<BattleCombatant> LineUpA = new List<BattleCombatant>();
        public List<BattleCombatant> LineUpB = new List<BattleCombatant>();
        public int StepNumber;

        public BattleCombatant LeadA => LineUpA.Count > 0 ? LineUpA[0] : null;
        public BattleCombatant SupportA => LineUpA.Count > 1 ? LineUpA[1] : null;
        public BattleCombatant LeadB => LineUpB.Count > 0 ? LineUpB[0] : null;
        public BattleCombatant SupportB => LineUpB.Count > 1 ? LineUpB[1] : null;

        public List<BattleCombatant> LineUp(Side side) => side == Side.A ? LineUpA : LineUpB;
    }
}
