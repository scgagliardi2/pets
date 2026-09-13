using Pets.Data;

namespace Pets.Meta
{
    /// <summary>Camp / Pokémon Center node resolution (design doc §5.1): grants EXP to the active
    /// line-up and a temporary Attack buff for the next fight.</summary>
    public static class CampResolver
    {
        private const float NextBattleAttackBonusPercent = 0.2f;

        /// <summary>A rest is worth what a wild win at the Location's baseline level would pay — about
        /// a fight's worth of EXP, so choosing the Center over a Battle node isn't a sacrifice.</summary>
        public static int ExpFor(RunState state) =>
            BattleRewardResolver.BaseExpPerWin + RunProgression.BaselineLevel(state.BadgeCount);

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
