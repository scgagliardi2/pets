namespace Pets.Simulation
{
    public enum Team
    {
        A,
        B
    }

    public enum BattleOutcome
    {
        TeamAWins,
        TeamBWins,
        Draw
    }

    public enum BattleEventKind
    {
        AbilityTriggered,
        Damage,
        Heal,
        BuffAttack,
        BuffHealth,
        Faint,
        Summon,
        BattleEnd
    }

    /// <summary>
    /// One step of a battle's event stream — what golden fixtures assert against and what a
    /// future replay/animation layer would consume. Fields are nullable/default where not
    /// applicable to a given Kind rather than having a subtype per kind, to keep this a plain,
    /// easily-comparable data shape.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public Team? SourceTeam;
        public string SourceInstanceId;
        public Team? TargetTeam;
        public string TargetInstanceId;
        public TriggerType? Trigger;
        public int Amount;
        public BattleOutcome? Outcome;

        public override string ToString()
        {
            return $"{Kind} src={SourceInstanceId} tgt={TargetInstanceId} amount={Amount} trigger={Trigger} outcome={Outcome}";
        }
    }
}
