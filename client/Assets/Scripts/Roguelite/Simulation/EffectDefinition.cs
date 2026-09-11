namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// One effect within a PassiveDefinition (or, later, an item). See docs/content-schema.md §3.
    /// Deliberately a flat, non-polymorphic shape — same rationale as the old schema's EffectData.
    /// </summary>
    public struct EffectDefinition
    {
        public EffectType Type;
        public TargetSelector Target;

        /// <summary>Magnitude. Ignored by effects that don't take one (ClearStatus).</summary>
        public int Amount;

        /// <summary>Only meaningful for ApplyStatus.</summary>
        public StatusType? Status;

        public static EffectDefinition DealDamage(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.DealDamage, Target = target, Amount = amount };

        public static EffectDefinition Heal(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.Heal, Target = target, Amount = amount };

        public static EffectDefinition Shield(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.Shield, Target = target, Amount = amount };

        public static EffectDefinition ApplyStatus(TargetSelector target, StatusType status) =>
            new EffectDefinition { Type = EffectType.ApplyStatus, Target = target, Status = status };

        public static EffectDefinition ClearStatus(TargetSelector target) =>
            new EffectDefinition { Type = EffectType.ClearStatus, Target = target };

        public static EffectDefinition BuffAttack(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.BuffAttack, Target = target, Amount = amount };

        public static EffectDefinition BuffSpeed(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.BuffSpeed, Target = target, Amount = amount };

        public static EffectDefinition DamageReduction(TargetSelector target, int amount) =>
            new EffectDefinition { Type = EffectType.DamageReduction, Target = target, Amount = amount };

        public static EffectDefinition Lifesteal(TargetSelector target, int amountPercent) =>
            new EffectDefinition { Type = EffectType.Lifesteal, Target = target, Amount = amountPercent };

        public static EffectDefinition ModifyChargeRate(TargetSelector target, int amountPercent) =>
            new EffectDefinition { Type = EffectType.ModifyChargeRate, Target = target, Amount = amountPercent };
    }
}
