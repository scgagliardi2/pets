namespace Pets.Roguelite.Simulation
{
    public enum StepEventKind
    {
        Damage,
        Heal,
        Shield,
        StatusApplied,
        StatusCleared,
        StatusTick,
        BuffAttack,
        BuffSpeed,
        DamageReductionApplied,
        LifestealHeal,
        LifestealModified,
        ChargeRateModified,
        PassiveTriggered,
        Faint,
        BattleEnd
    }

    /// <summary>
    /// One event within a Step's log — what golden fixtures will assert against once they exist,
    /// and what a future replay/animation layer would consume (docs/battle-sim-spec.md). Fields
    /// are nullable/unused where not applicable to a given Kind, same rationale as the old
    /// schema's BattleEvent: one flat shape beats a subtype per kind.
    /// </summary>
    public sealed class StepEvent
    {
        public int StepNumber;
        public StepEventKind Kind;
        public SideId? SourceSide;
        public string SourceName;
        public SideId? TargetSide;
        public string TargetName;
        public int Amount;
        public StatusType? Status;
        public string PassiveName;
        public BattleOutcome? Outcome;

        public override string ToString()
        {
            switch (Kind)
            {
                case StepEventKind.Damage:
                    return $"{SourceName} ({SourceSide}) hits {TargetName} ({TargetSide}) for {Amount}";
                case StepEventKind.Heal:
                    return $"{TargetName} ({TargetSide}) heals {Amount} HP";
                case StepEventKind.Shield:
                    return $"{TargetName} ({TargetSide}) gains a {Amount}-point shield";
                case StepEventKind.StatusApplied:
                    return $"{TargetName} ({TargetSide}) is now {Status}";
                case StepEventKind.StatusCleared:
                    return $"{TargetName} ({TargetSide})'s status is cleared";
                case StepEventKind.StatusTick:
                    return $"{TargetName} ({TargetSide}) takes {Amount} {Status} damage";
                case StepEventKind.BuffAttack:
                    return $"{TargetName} ({TargetSide})'s Attack rises by {Amount}";
                case StepEventKind.BuffSpeed:
                    return $"{TargetName} ({TargetSide})'s Speed rises by {Amount}";
                case StepEventKind.DamageReductionApplied:
                    return $"{TargetName} ({TargetSide}) gains {Amount} flat damage reduction";
                case StepEventKind.LifestealHeal:
                    return $"{SourceName} ({SourceSide}) drains {Amount} HP via lifesteal";
                case StepEventKind.LifestealModified:
                    return $"{TargetName} ({TargetSide})'s lifesteal rises by {Amount}%";
                case StepEventKind.ChargeRateModified:
                    return $"{TargetName} ({TargetSide})'s charge rate shifts by {Amount}%";
                case StepEventKind.PassiveTriggered:
                    return $"*** {SourceName} ({SourceSide})'s charge fills — {PassiveName} triggers! ***";
                case StepEventKind.Faint:
                    return $"{SourceName} ({SourceSide}) has fainted!";
                case StepEventKind.BattleEnd:
                    return $"=== Battle over: {Outcome} ===";
                default:
                    return $"{Kind}";
            }
        }
    }
}
