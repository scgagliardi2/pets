using System;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>The catch-chance formula (design doc §12.1). Pure arithmetic over a ball tier and
    /// the target's current condition — no RNG, no battle state mutation, no Unity — so the odds
    /// can be unit-tested directly and, separately, shown to the player before they commit a ball.
    ///
    /// The three factors the design doc names, and how each is read here:
    /// - **Ball tier** sets the floor (<see cref="BallCatalog.BaseChance"/>).
    /// - **Current HP %** is the big lever: a target at 0% HP would add the whole of
    ///   <see cref="WeakenedBonus"/>, scaling linearly down to nothing at full health. This is what
    ///   makes "weaken it first" the loop rather than a suggestion.
    /// - **Status** adds a flat <see cref="StatusBonus"/> for any of the four conditions. Flat and
    ///   equal across them on purpose for a first pass: the design doc asks for "a meaningful
    ///   bonus" and says nothing about ranking them, and making sleep better than poison is a
    ///   balance decision that wants playtesting, not a guess baked into the formula now.
    ///
    /// The result is clamped to <see cref="MaxChance"/> so no throw is ever a certainty — the
    /// "will it break free" beat needs the possibility of breaking free.</summary>
    public static class CatchOdds
    {
        /// <summary>Added at 0% HP, scaled linearly to 0 at full HP.</summary>
        public const float WeakenedBonus = 0.45f;

        /// <summary>Added flat when the target has any status condition.</summary>
        public const float StatusBonus = 0.15f;

        /// <summary>No throw is ever certain, however weakened the target.</summary>
        public const float MaxChance = 0.95f;

        /// <summary>Odds as a fraction in [0, <see cref="MaxChance"/>].</summary>
        public static float ChanceFor(BallTier tier, int currentHP, int maxHP, StatusType? status)
        {
            float hpFraction = maxHP > 0 ? Math.Max(0f, Math.Min(1f, currentHP / (float)maxHP)) : 0f;
            float chance = BallCatalog.BaseChance(tier)
                + WeakenedBonus * (1f - hpFraction)
                + (status.HasValue ? StatusBonus : 0f);
            return Math.Max(0f, Math.Min(MaxChance, chance));
        }

        /// <summary>Odds against a live combatant — the overload a battle actually calls. Max HP is
        /// the combatant's stat line rather than its source instance, so a battle-only Health buff
        /// counts against the catch the same way it counts in the fight.</summary>
        public static float ChanceFor(BallTier tier, BattleCombatant target) =>
            target == null ? 0f : ChanceFor(tier, target.CurrentHP, target.CurrentStats.Health, target.Status);

        /// <summary>The odds as whole percent, for showing on a tray or a confirmation. Rounded
        /// rather than truncated so 0.649 reads as 65%, matching what the roll actually does.</summary>
        public static int PercentFor(BallTier tier, BattleCombatant target) =>
            (int)Math.Round(ChanceFor(tier, target) * 100f, MidpointRounding.AwayFromZero);

        /// <summary>Rolls the throw. Takes the battle's own <see cref="DeterministicRandom"/> so a
        /// fight stays reproducible end to end from its seed (design doc §10.5) — a catch is part of
        /// the fight's history, not a side channel with its own entropy.
        ///
        /// Resolution is one draw in 10,000, which is finer than any tuning these odds will plausibly
        /// get and keeps the draw an integer op, matching the rest of the simulator's RNG use.</summary>
        public static bool Roll(BallTier tier, BattleCombatant target, DeterministicRandom rng)
        {
            const int Resolution = 10000;
            int threshold = (int)Math.Round(ChanceFor(tier, target) * Resolution, MidpointRounding.AwayFromZero);
            return rng.NextInt(Resolution) < threshold;
        }
    }
}
