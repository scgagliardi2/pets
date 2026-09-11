using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Minimal EXP/level-up handling (design doc §7: "grow them via EXP/level"). No
    /// design-doc formula exists yet — flat placeholder growth, to be replaced once balancing
    /// starts (PLAN.md §10).</summary>
    public static class ExperienceResolver
    {
        private const float ExpToNextLevelGrowth = 1.5f;
        private const float StatGrowthPerLevel = 0.1f;

        /// <summary>Grants EXP and applies any resulting level-ups in place.</summary>
        public static void GrantExp(PokemonInstance mon, int amount)
        {
            mon.Exp += amount;
            while (mon.Exp >= mon.ExpToNextLevel)
            {
                mon.Exp -= mon.ExpToNextLevel;
                mon.Level++;
                mon.ExpToNextLevel = (int)(mon.ExpToNextLevel * ExpToNextLevelGrowth);
                mon.CurrentStats = new Stats
                {
                    Attack = mon.CurrentStats.Attack + Growth(mon.CurrentStats.Attack),
                    Health = mon.CurrentStats.Health + Growth(mon.CurrentStats.Health),
                    Speed = mon.CurrentStats.Speed + Growth(mon.CurrentStats.Speed)
                };
            }
        }

        private static int Growth(int baseValue) => System.Math.Max(1, (int)(baseValue * StatGrowthPerLevel));
    }
}
