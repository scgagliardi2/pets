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
        /// effectively infinite fight — forces a draw if hit. Not from the design doc.
        ///
        /// The last resort, not the first: <see cref="SuddenDeathStep"/> is what actually ends a
        /// fight neither side can win, and it does so with a real result rather than a draw.</summary>
        public const int StepCap = 200;

        /// <summary>The Step from which both Leads start taking escalating true damage, so a fight
        /// neither side can finish ends anyway (battle-sim-spec.md §9).
        ///
        /// Standing modifiers accumulate for the whole battle (spec §12), which means a long enough
        /// fight can reach a state where nobody can lose: two mons whose Lifesteal has stacked past
        /// 100% each heal back exactly what they take, forever. That isn't hypothetical content — two
        /// Bulbasaurs with Vine Drain reach it on Step 12 — and before this existed such a fight
        /// ground out all 200 Steps of <see cref="StepCap"/> with the HP bars frozen and then called
        /// itself a draw, which on screen is indistinguishable from the game having hung.
        ///
        /// Thirty, because no fight real content produces runs anywhere near that long — the longest
        /// measured across wild, Gym and dev battles is 17 Steps — so this only ever fires on a fight
        /// that has genuinely stopped resolving itself.</summary>
        public const int SuddenDeathStep = 30;

        /// <summary>How much true damage sudden death deals on its first Step, growing by this much
        /// again every Step after. Escalating rather than flat so it must eventually outrun any
        /// amount of stacked sustain, however much a passive has piled up.</summary>
        public const int SuddenDeathDamagePerStep = 1;

        /// <summary>The least HP damage a connecting attack can do, however much
        /// DamageReductionFlat has stacked up. Blunting a hit is what that effect is for; nullifying
        /// every hit for the rest of the battle is how a fight stops being winnable.</summary>
        public const int MinimumAttackDamage = 1;

        /// <summary>Ceiling on accumulated Lifesteal. Draining back more life than the blow actually
        /// took is meaningless, so this is a definition rather than a balance number — but it is also
        /// the thing that kept two Vine Drain mons alive forever at 1980%.</summary>
        public const float MaxLifestealPercent = 1f;

        /// <summary>Same safeguard, bounding total logged events instead of Steps.</summary>
        public const int EventCap = 10000;

        /// <summary>Charge accrual multiplier while Paralyzed (battle-sim-spec.md §5).</summary>
        public const float ParalyzedChargeMultiplier = 0.5f;
    }
}
