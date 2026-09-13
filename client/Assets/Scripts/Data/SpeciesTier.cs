using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>How a species' real Pokémon stats become the small tier line the game actually plays
    /// with — a Charmander's 52/39/65 becoming 2/2/1 (ADR 0008).
    ///
    /// **Every species sits in a tier, and every species in a tier spends the same number of
    /// points.** Tier 1 has <see cref="TierOneTotal"/> points to spend across Attack, Health and
    /// Speed; each tier above has <see cref="TotalIncreasePerTier"/> more. That increase is exactly
    /// what a mon gains over a full evolution cycle (<see cref="Pets.Meta.ExperienceResolver.ExpPerEvolution"/>
    /// points of EXP, each worth +1 Attack and +1 Health), so evolving one tier up is stat-neutral at
    /// the moment it happens and everything beyond that is a real gain.
    ///
    /// **Which tier comes from the roster sheet, not from judgement.** A species' band is read off
    /// its real base-stat total (<see cref="BaseStatTotalBands"/>) — the one number
    /// docs/pokemon_stats_unique.xlsx already supplies. The importer then forces every evolution to
    /// land at least one tier above what it came from, which is the only hand-applied rule and
    /// exists for the nine real chains whose middle stage is genuinely weaker than its base (a
    /// Metapod has worse stats than the Caterpie it came from).
    ///
    /// **Speed is scarce on purpose.** Most of the roster sits at 1; only a species whose real Speed
    /// clears <see cref="Speed2Threshold"/> gets 2, and <see cref="Speed3Threshold"/> gets 3, and
    /// neither is available below the tier that can afford it. Speed drives the charge meter
    /// (battle-sim-spec.md §4) against a threshold of three, so the difference between 1 and 2 is the
    /// difference between one passive a fight and two — it has to stay rare to stay meaningful.
    ///
    /// Integer arithmetic throughout, for the same reason StatGrowth uses it: these numbers feed the
    /// deterministic simulator, and float rounding is the part most likely to differ between a client
    /// and a future server.</summary>
    public static class SpeciesTier
    {
        /// <summary>Points a tier-1 species spends across all three stats.</summary>
        public const int TierOneTotal = 5;

        /// <summary>Extra points each tier gets over the one below — equal to a full evolution
        /// cycle's worth of growth (5 EXP × +1 Attack and +1 Health), so an evolution one tier up
        /// neither gains nor loses at the moment it lands.</summary>
        public const int TotalIncreasePerTier = 10;

        public const int MinTier = 1;
        public const int MaxTier = 6;

        /// <summary>Upper bound of each tier's real base-stat-total band, for tiers 1..5; anything
        /// above the last entry is <see cref="MaxTier"/>. The edges are placed against the real
        /// roster (totals run 75–350), not on an even split: the low end is wide because that's where
        /// the base forms bunch up, and the top two bands are thin because only a handful of species
        /// live there.</summary>
        public static readonly int[] BaseStatTotalBands = { 165, 205, 265, 295, 325 };

        /// <summary>Real Speed at or above which a species is worth 2 Speed, and 3. Also gated by
        /// tier — a tier-1 species has five points and can't spend three of them on Speed.</summary>
        public const int Speed2Threshold = 80;
        public const int Speed3Threshold = 110;

        /// <summary>Lowest tier that can afford each Speed value; index 0 is Speed 1.</summary>
        private static readonly int[] MinTierForSpeed = { 1, 2, 3 };

        /// <summary>The highest Speed any species can have — what a Speed bar is drawn against.</summary>
        public const int MaxSpeed = 3;

        public static int Clamp(int tier) => Math.Max(MinTier, Math.Min(MaxTier, tier));

        /// <summary>Points a species in <paramref name="tier"/> spends across its three stats.</summary>
        public static int TotalFor(int tier) => TierOneTotal + TotalIncreasePerTier * (Clamp(tier) - 1);

        /// <summary>The tier a species' real base-stat total puts it in, before the importer's
        /// "an evolution is at least one tier up" pass.</summary>
        public static int ForBaseStatTotal(int realBaseStatTotal)
        {
            for (int i = 0; i < BaseStatTotalBands.Length; i++)
            {
                if (realBaseStatTotal <= BaseStatTotalBands[i])
                {
                    return i + 1;
                }
            }
            return MaxTier;
        }

        /// <summary>Speed points for a species with this real Speed, in this tier.</summary>
        public static int SpeedFor(int realSpeed, int tier)
        {
            int clamped = Clamp(tier);
            int speed = 1;
            if (realSpeed >= Speed2Threshold && clamped >= MinTierForSpeed[1])
            {
                speed = 2;
            }
            if (realSpeed >= Speed3Threshold && clamped >= MinTierForSpeed[2])
            {
                speed = 3;
            }
            return speed;
        }

        /// <summary>The tier line for a species: Speed off <see cref="SpeedFor"/>, then whatever the
        /// tier has left split between Attack and Health in the ratio of the species' own real
        /// Attack and Health, so a bulky mon stays bulky and a glass cannon stays one. Both land at
        /// 1 or more — a 0-Attack mon can never win and a 0-Health mon is already dead.</summary>
        public static Stats Distribute(int realAttack, int realHealth, int realSpeed, int tier)
        {
            int clamped = Clamp(tier);
            int speed = SpeedFor(realSpeed, clamped);
            int rest = TotalFor(clamped) - speed;

            int sum = Math.Max(1, realAttack + realHealth);
            // Round half away from zero without floating point: (2ax + s) / 2s.
            int attack = (2 * rest * Math.Max(0, realAttack) + sum) / (2 * sum);
            attack = Math.Max(1, Math.Min(rest - 1, attack));

            return new Stats { Attack = attack, Health = rest - attack, Speed = speed };
        }
    }
}
