namespace Pets.Meta
{
    /// <summary>Location node-map node kinds. Design doc §5.1 also defines Event, PvP, and Gym —
    /// out of scope until Phase 1's full run loop (PLAN.md §6), so not modeled yet.</summary>
    public enum NodeType
    {
        PvE,
        Camp
    }
}
