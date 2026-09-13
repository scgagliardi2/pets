namespace Pets.Simulation
{
    /// <summary>Balance constants, not stat-driven — tune freely during Phase 0/1 balancing
    /// (battle-sim-spec.md §3-§4, §9).</summary>
    public static class BattleConfig
    {
        /// <summary>A charge meter triggers its passive once it reaches this value. Global,
        /// not per-mon or per-type — Speed is what varies triggering frequency.
        ///
        /// Three, because Speed is now a number between 1 and 3 (Pets.Data.SpeciesTier) rather than a
        /// real Pokémon stat in the 20–130 range: a mon accrues its Speed in charge each Step, so
        /// Speed 1 fires on the third Step, Speed 2 on the second and Speed 3 every Step. Against the
        /// old 100 a Speed-1 mon would have needed a hundred-Step fight to use its passive once.</summary>
        public const int ChargeThreshold = 3;

        /// <summary>Fixed pacing window per Step. Named "Ms" to match battle-sim-spec.md §3's
        /// charge formula (charge += speed * stepDurationMs), but the *value* is a small
        /// placeholder scalar, not literal milliseconds. At 1, a mon accrues exactly its Speed in
        /// charge per Step, so <see cref="ChargeThreshold"/> reads directly as "Steps per trigger at
        /// Speed 1". Length doesn't depend on Speed — only how much charge each mon accrues during
        /// it does. Rescale this constant, not the formula, if pacing needs to change.</summary>
        public const int DefaultStepDurationMs = 1;

        /// <summary>Engineering safeguard against a pathological passive/status combo creating an
        /// effectively infinite fight — forces a draw if hit. Not from the design doc.</summary>
        public const int StepCap = 200;

        /// <summary>Same safeguard, bounding total logged events instead of Steps.</summary>
        public const int EventCap = 10000;

        /// <summary>Charge accrual multiplier while Paralyzed (battle-sim-spec.md §5).</summary>
        public const float ParalyzedChargeMultiplier = 0.5f;
    }
}
