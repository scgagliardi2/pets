namespace Pets.Simulation
{
    /// <summary>Balance constants, not stat-driven — tune freely during Phase 0/1 balancing
    /// (battle-sim-spec.md §3-§4, §9).</summary>
    public static class BattleConfig
    {
        /// <summary>A charge meter triggers its passive once it reaches this value. Global,
        /// not per-mon or per-type — Speed is what varies triggering frequency.</summary>
        public const int ChargeThreshold = 100;

        /// <summary>Fixed pacing window per Step. Named "Ms" to match battle-sim-spec.md §3's
        /// charge formula (charge += speed * stepDurationMs), but the *value* is a small
        /// placeholder scalar, not literal milliseconds — with the curated roster's Speed stats
        /// in the ~20-130 range, 1 makes charge accrual (and therefore passive-trigger cadence)
        /// track Speed directly at a readable pace (2-5 Steps per trigger). Length doesn't depend
        /// on Speed — only how much charge each mon accrues during it does.</summary>
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
