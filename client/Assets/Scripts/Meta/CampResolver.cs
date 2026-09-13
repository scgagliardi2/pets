using Pets.Data;

namespace Pets.Meta
{
    /// <summary>Camp / Pokémon Center node resolution (design doc §5.1): grants EXP to the active
    /// line-up and a temporary Attack buff for the next fight.
    ///
    /// The line-up and nothing else, on the same rule a won fight follows (BattleRewardResolver): the
    /// mons in the Box weren't there.</summary>
    public static class CampResolver
    {
        private const float NextBattleAttackBonusPercent = 0.2f;

        /// <summary>A rest is worth exactly what a fight is, so choosing the Center over a Battle
        /// node costs no growth — only the chance to catch something.</summary>
        public static int ExpFor(RunState state) => BattleRewardResolver.ExpPerWin;

        public static GrowthReport Resolve(RunState state, PokemonSpeciesLibrary library)
        {
            var report = new GrowthReport();
            int amount = ExpFor(state);
            report.ExpGranted = amount;
            foreach (var mon in state.LineUp)
            {
                report.Merge(ExperienceResolver.GrantExp(mon, amount, library));
            }
            ExperienceResolver.ApplyCatchUp(state, library);
            state.NextBattleAttackBonusPercent = NextBattleAttackBonusPercent;
            return report;
        }
    }
}
