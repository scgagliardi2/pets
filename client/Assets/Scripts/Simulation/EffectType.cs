namespace Pets.Simulation
{
    /// <summary>Passive/synergy effect vocabulary. See content-schema.md §4 — extend this list
    /// (don't special-case a species) if a Type-flavor seed genuinely can't be expressed.</summary>
    public enum EffectType
    {
        DealDamage,
        Heal,
        Shield,
        ApplyStatus,
        ClearStatus,
        BuffAttack,
        BuffSpeed,
        ModifyChargeRate,
        DamageReduction,
        Lifesteal
    }
}
