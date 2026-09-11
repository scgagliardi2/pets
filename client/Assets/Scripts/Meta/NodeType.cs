namespace Pets.Meta
{
    /// <summary>Location node-map node kinds (design doc §5.1). Event/PvP/Gym are modeled by
    /// RegionMapGenerator's visual-only branching map prototype (PLAN.md Phase 1, item 5); only
    /// PvE and Camp are actually resolvable today, via ForestLocationFactory's linear map.</summary>
    public enum NodeType
    {
        PvE,
        Event,
        PvP,
        Camp,
        Gym
    }
}
