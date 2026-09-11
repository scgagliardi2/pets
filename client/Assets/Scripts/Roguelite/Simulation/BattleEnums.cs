namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// A mon has at most one status at a time — applying a new one overwrites the old one.
    /// See docs/battle-sim-spec.md §5.
    /// </summary>
    public enum StatusType
    {
        Poisoned,
        Burned,
        Paralyzed,
        Asleep
    }

    /// <summary>Which side of the battle a mon/line-up belongs to.</summary>
    public enum SideId
    {
        A,
        B
    }

    public enum BattleOutcome
    {
        SideAWins,
        SideBWins,
        Draw
    }
}
