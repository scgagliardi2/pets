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

        /// <summary>A status that would have applied was stopped by a Fairy synergy ward.</summary>
        StatusBlocked,

        /// <summary>A side has a type synergy active this battle (TeamSynergy). One per side per type
        /// present, stamped Step 0; Amount is how many of that side's mons carry
        /// <see cref="StepEvent.SynergyType"/>.</summary>
        TypeSynergy,

        /// <summary>Escalating true damage dealt to both Leads once a fight has run past
        /// BattleConfig.SuddenDeathStep without resolving (battle-sim-spec.md §9).</summary>
        SuddenDeath,
        BuffAttack,
        BuffSpeed,
        ChargeRateModified,
        DamageReductionApplied,
        Lifesteal,
        LifestealHeal,
        PassiveTriggered,
        Faint,

        /// <summary>A wild mon was caught and left the fight (design doc §12.1). Its own kind
        /// rather than a Faint, because the two mean opposite things to everything downstream: a
        /// faint offers the mon to the post-fight "pick 1 from defeated" stub
        /// (Meta/CatchResolver.GetDefeated), while a catch has already put it in the Box and must
        /// not offer it twice. The simulator never raises this itself — the catching layer does,
        /// through BattleSimulator.RemoveCaught, so a catch lands in the same event stream as
        /// everything else that happened in the fight.</summary>
        Caught,
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

        /// <summary>Only set on a TypeSynergy event.</summary>
        public PokemonType? SynergyType;

        public override string ToString()
        {
            return $"Step {Step}: {Kind} src={SourceInstanceId} tgt={TargetInstanceId} amount={Amount} status={Status} outcome={Outcome} synergy={SynergyType}";
        }
    }
}
