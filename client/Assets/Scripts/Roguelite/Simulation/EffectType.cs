namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// See docs/content-schema.md §4. Covers the Type-flavor seeds in the design doc's §11.
    /// This is the vocabulary this first sandbox pass actually implements end-to-end in
    /// BattleSimulator — extend here (and in BattleSimulator's ApplyEffect switch) rather than
    /// special-casing a species if a new flavor genuinely needs a new verb.
    /// </summary>
    public enum EffectType
    {
        DealDamage,
        Heal,

        /// <summary>Absorb-shield, consumed by incoming damage before HP (Rock's seed).</summary>
        Shield,

        ApplyStatus,
        ClearStatus,

        /// <summary>Additive, permanent for the rest of the battle.</summary>
        BuffAttack,

        /// <summary>Additive, permanent for the rest of the battle.</summary>
        BuffSpeed,

        /// <summary>Flat reduction applied to incoming damage for the rest of the battle (Steel's seed).</summary>
        DamageReduction,

        /// <summary>
        /// Percentage-point buff to the target's own lifesteal, applying to all damage it deals
        /// for the rest of the battle — attack-exchange damage included, not just its own
        /// passive's DealDamage (Grass's seed). See docs/content-schema.md §4.
        /// </summary>
        Lifesteal,

        /// <summary>
        /// Additive percentage modifier to how fast the target's charge meter fills for the rest
        /// of the battle (Electric's speed-up seed, Ice's slow seed).
        /// </summary>
        ModifyChargeRate
    }
}
