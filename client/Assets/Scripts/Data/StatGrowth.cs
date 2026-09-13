using System;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>The one place a level becomes stats (design doc §7, ADR 0006). Everything with
    /// stats goes through here — the player's mons via Pets.Meta.ExperienceResolver.Recompute, and
    /// every wild/Gym/PvP mon via PokemonInstanceFactory.Create — so both sides of a fight grow by
    /// the same rule and "is this fair?" is a question about levels rather than about which code
    /// path built the mon.
    ///
    /// It lives in Data rather than Meta only because the factory needs it and Data can't reference
    /// Meta; the *pacing* (what a level costs, when a mon evolves) is Meta's, in LevelCurve and
    /// ExperienceResolver.
    ///
    /// Three rules, each deliberate:
    ///
    /// **Growth is proportional to the species' own base, not flat.** A level adds
    /// <see cref="GrowthPerLevel"/> of the species' base stat plus a small flat
    /// <see cref="FlatGainPerLevel"/>. The previous model added the same flat +10 to everyone
    /// (ADR 0005), which converged the whole roster on identical numbers within a few points — a
    /// Caterpie and a Charizard ended a run indistinguishable, which is the opposite of what
    /// collecting is for. The flat term is what keeps the bottom of the roster from being left
    /// behind entirely by the percentage.
    ///
    /// **Speed doesn't grow with level at all.** It's a species trait, changed only by evolving.
    /// Speed drives the charge meter (battle-sim-spec.md §3–§4) and the roster's median Speed of 60
    /// against a threshold of 100 already fires a passive every other Step — scale it 2x over a run
    /// and every mon on the board fires every Step, which erases the difference between a fast mon
    /// and a slow one exactly when a run has the most mons to tell apart.
    ///
    /// **Health carries a flat <see cref="HealthScalar"/> multiplier.** The roster's median
    /// Health/Attack ratio is 0.88, and damage is flat Attack per Step, so on the sheet's raw
    /// numbers 63% of Leads one-shot the opposing Lead: a median fight lasted 2 Steps and a fifth of
    /// them ended in a mutual wipe. That's too short for a passive to fire twice or for a level of
    /// advantage to show up as anything but a coin flip. x3 puts a median fight at ~5 Steps. It is a
    /// battle-balance constant living in the stat calculation rather than in the simulator on
    /// purpose — Pets.Simulation stays a pure function of the stats it's handed
    /// (docs/battle-sim-spec.md), and the golden fixtures, which carry explicit stats, are
    /// unaffected by it.</summary>
    public static class StatGrowth
    {
        /// <summary>The level every mon starts at. Level 1 is the species' sheet stats (with
        /// <see cref="HealthScalar"/> applied), so the roster sheet stays readable as "what a
        /// fresh one of these is".</summary>
        public const int StartingLevel = 1;

        /// <summary>Fraction of the species' base Attack/Health added per level above
        /// <see cref="StartingLevel"/>. At the level cap (LevelCurve.MaxLevel = 12) that's
        /// base x2.1, before whatever the mon's evolutions did to its base.</summary>
        public const float GrowthPerLevel = 0.10f;

        /// <summary>Flat Attack/Health added per level on top of the percentage, so the weakest
        /// species in the roster (Attack 10, Health 1) still gains something a player can see.</summary>
        public const int FlatGainPerLevel = 3;

        /// <summary>Every mon's Health pool is this multiple of the roster sheet's number. See the
        /// class remarks — the sheet's Health is roughly one hit, and a fight needs to last long
        /// enough for passives and levels to matter.</summary>
        public const int HealthScalar = 3;

        /// <summary>The stats a mon of <paramref name="baseStats"/> has at
        /// <paramref name="level"/>. Pure and total: the same species at the same level always has
        /// the same stats, which is what lets stats be derived rather than stored (ADR 0005).</summary>
        public static Stats StatsFor(Stats baseStats, int level)
        {
            int steps = Math.Max(0, level - StartingLevel);
            float multiplier = 1f + GrowthPerLevel * steps;
            int flat = FlatGainPerLevel * steps;

            return new Stats
            {
                Attack = Grow(baseStats.Attack, multiplier, flat),
                Health = Grow(baseStats.Health, multiplier, flat) * HealthScalar,
                Speed = baseStats.Speed
            };
        }

        private static int Grow(int baseValue, float multiplier, int flat) =>
            (int)Math.Round(baseValue * multiplier, MidpointRounding.AwayFromZero) + flat;
    }
}
