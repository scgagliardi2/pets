namespace Pets.Meta
{
    /// <summary>Camp node resolution (design doc §5.1): grants EXP to the active line-up and a
    /// temporary Attack buff for the next fight.</summary>
    public static class CampResolver
    {
        private const int ExpGranted = 30;
        private const float NextBattleAttackBonusPercent = 0.2f;

        public static void Resolve(RunState state)
        {
            foreach (var mon in state.LineUp)
            {
                ExperienceResolver.GrantExp(mon, ExpGranted);
            }
            state.NextBattleAttackBonusPercent = NextBattleAttackBonusPercent;
        }
    }
}
