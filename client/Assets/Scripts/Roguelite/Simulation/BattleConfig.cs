namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// Balance constants for the Step loop. Every value here is an explicit placeholder — none
    /// of them are pinned by the design doc, which leaves the numbers as tuning questions
    /// (PLAN.md §10). Pull these into a proper tunable asset (mirroring the old schema's
    /// ShopConfig pattern) once there's a real config-authoring need; plain constants are enough
    /// for a sandbox.
    /// </summary>
    public static class BattleConfig
    {
        /// <summary>A mon's passive fires once its charge meter reaches this value.</summary>
        public const float ChargeThreshold = 100f;

        /// <summary>Fixed pacing window per Step — NOT stat-driven (docs/battle-sim-spec.md §3).</summary>
        public const float StepDurationSeconds = 0.2f;

        /// <summary>Charge accrual multiplier while Paralyzed.</summary>
        public const float ParalysisChargeMultiplier = 0.5f;

        /// <summary>Charge accrual multiplier while Asleep — fully zeroed.</summary>
        public const float AsleepChargeMultiplier = 0f;

        /// <summary>Flat HP lost per Step to Poison or Burn (bypasses Shield/DamageReduction).</summary>
        public const int StatusTickDamage = 3;

        /// <summary>Safety cap — forces a draw if a fight runs this long (docs/battle-sim-spec.md §9).</summary>
        public const int StepCap = 200;

        /// <summary>Safety cap — forces a draw if a single fight logs this many events.</summary>
        public const int EventCap = 10000;
    }
}
