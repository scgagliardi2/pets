using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What winning a node's fight gives the run (design doc §7: mons "grow via EXP/level").
    /// Only the mons still standing at the end are rewarded, which is why this takes the battle's
    /// surviving combatants rather than the run's line-up — a combatant's Source is the run-level
    /// mon to write the EXP onto (see BattleCombatant).
    ///
    /// The amounts are flat placeholders in the same sense CampResolver's are; no design-doc
    /// formula exists yet (PLAN.md §10, balance is Phase 4).</summary>
    public static class BattleRewardResolver
    {
        public const int PvEWinExp = 20;

        /// <summary>A Gym is the Location's finale, so it pays out like several PvE nodes.</summary>
        public const int GymWinExp = 60;

        public static void GrantWinRewards(IEnumerable<BattleCombatant> survivors, bool isGym)
        {
            int exp = isGym ? GymWinExp : PvEWinExp;
            foreach (var survivor in survivors)
            {
                if (survivor.Source != null)
                {
                    ExperienceResolver.GrantExp(survivor.Source, exp);
                }
            }
        }
    }
}
