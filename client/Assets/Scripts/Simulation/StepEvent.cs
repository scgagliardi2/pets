namespace Pets.Simulation
{
    public enum StepEventKind
    {
        Damage,
        Heal,
        Shield,
        ShieldAbsorbed,
        StatusApplied,
        StatusCleared,
        StatusTick,
        BuffAttack,
        BuffSpeed,
        ChargeRateModified,
        DamageReductionApplied,
        Lifesteal,
        LifestealHeal,
        PassiveTriggered,
        Faint,
        Promotion,
        BattleEnd
    }

    /// <summary>One entry in a battle's Step-event stream — what golden fixtures assert against
    /// and what a future replay/animation layer would consume. Fields are unused/default where
    /// not applicable to a given Kind rather than having a subtype per kind, to keep this a
    /// plain, easily-comparable data shape (same rationale as EffectDefinition).</summary>
    public sealed class StepEvent
    {
        public int Step;
        public StepEventKind Kind;
        public Side? SourceSide;
        public string SourceInstanceId;
        public Side? TargetSide;
        public string TargetInstanceId;
        public int Amount;
        public StatusType? Status;
        public BattleOutcome? Outcome;

        public override string ToString()
        {
            return $"Step {Step}: {Kind} src={SourceInstanceId} tgt={TargetInstanceId} amount={Amount} status={Status} outcome={Outcome}";
        }
    }
}
