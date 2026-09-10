namespace Pets.Simulation
{
    /// <summary>See battle-sim-spec.md §5 for exact semantics of each selector.</summary>
    public enum TargetSelector
    {
        Self,
        RandomAlly,
        RandomEnemy,
        FrontEnemy
    }
}
