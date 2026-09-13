using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>What a species' stats are at a given level — the one formula every PokemonInstance's
    /// CurrentStats comes from (ADR 0006). Lives in Data rather than next to the EXP curve in
    /// Pets.Meta.ExperienceResolver because PokemonInstanceFactory has to build a level-1 mon with it,
    /// and Data can't see Meta.
    ///
    /// **Proportional, with a small flat floor.** Each level adds <see cref="GrowthPercentPerLevel"/>
    /// of the species' own base stat plus <see cref="FlatGainPerLevel"/>. The proportional part keeps
    /// a species' shape as it grows — a bulky mon stays bulky, a glass cannon stays one — where ADR
    /// 0005's flat +10 to everything converged every species on the same numbers. The flat part keeps
    /// the weakest base forms from falling hopelessly behind.
    ///
    /// **Health is multiplied by <see cref="HealthMultiplier"/>.** Damage is flat Attack per Step
    /// (battle-sim-spec.md §3), and the roster's median Health:Attack is 0.88, so at 1x most Leads
    /// one-shot each other, fights last two Steps and a fifth of them are draws. Tripling Health gives
    /// fights ~5 Steps, lets a passive fire more than once, and makes a level's advantage readable.
    ///
    /// Integer arithmetic on purpose: these numbers feed the deterministic simulator, and float
    /// rounding is the part most likely to differ between a client and a future server.</summary>
    public static class StatGrowth
    {
        /// <summary>Share of each base stat gained per level, in percent.</summary>
        public const int GrowthPercentPerLevel = 10;

        /// <summary>Added to each stat per level on top of the proportional gain.</summary>
        public const int FlatGainPerLevel = 2;

        /// <summary>Health, after growth, is multiplied by this. See the class doc for why.</summary>
        public const int HealthMultiplier = 3;

        public static Stats AtLevel(PokemonSpeciesDefinitionAsset species, int level)
        {
            return new Stats
            {
                Attack = Grow(species.BaseAttack, level),
                Health = Grow(species.BaseHealth, level) * HealthMultiplier,
                Speed = Grow(species.BaseSpeed, level),
            };
        }

        /// <summary>The Speed a species at the base-speed ceiling
        /// (PokemonSpeciesDefinitionAsset.MaxBaseSpeed) would have at this level — the scale to draw a
        /// grown mon's speed bar against, so a high-level mon doesn't just pin the bar full.</summary>
        public static int SpeedCeilingAtLevel(int level) => Grow(PokemonSpeciesDefinitionAsset.MaxBaseSpeed, level);

        private static int Grow(int baseStat, int level)
        {
            int steps = Math.Max(0, level - 1);
            // Rounded to nearest: (x * p + 50) / 100 for non-negative x.
            int proportional = (baseStat * (100 + GrowthPercentPerLevel * steps) + 50) / 100;
            return proportional + FlatGainPerLevel * steps;
        }
    }
}
