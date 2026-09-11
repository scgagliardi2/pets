using Pets.Simulation;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// Everything one battle needs. Reuses Pets.Simulation.DeterministicRandom (the seeded
    /// xorshift32 generator) rather than duplicating it — the ADR for the pivot calls this out as
    /// one of the few pieces of the old implementation worth reusing as-is.
    /// </summary>
    public sealed class BattleState
    {
        public BattleSide SideA;
        public BattleSide SideB;
        public DeterministicRandom Rng;
        public int StepNumber;

        public BattleState(BattleSide sideA, BattleSide sideB, int seed)
        {
            SideA = sideA;
            SideB = sideB;
            Rng = new DeterministicRandom(seed);
            StepNumber = 0;
        }

        public bool IsOver => SideA.LineUp.Count == 0 || SideB.LineUp.Count == 0;

        /// <summary>Null while the battle is still going; the final result once IsOver is true.</summary>
        public BattleOutcome? DetermineOutcomeIfOver()
        {
            if (!IsOver)
            {
                return null;
            }
            bool aEmpty = SideA.LineUp.Count == 0;
            bool bEmpty = SideB.LineUp.Count == 0;
            if (aEmpty && bEmpty)
            {
                return BattleOutcome.Draw;
            }
            return aEmpty ? BattleOutcome.SideBWins : BattleOutcome.SideAWins;
        }
    }
}
