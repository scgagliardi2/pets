namespace Pets.Meta
{
    /// <summary>Location node-map node kinds (design doc §5.1) — shown to the player as Battle
    /// (PvE), Encounter (Event), Mystery Trainer (PvP), Pokémon Center (Camp), and Gym (see
    /// RegionMapController's NodeDisplayNames for that flavor mapping). The branching map itself
    /// (RegionMapGenerator/RegionMapTraversal, PLAN.md Phase 1) is walkable, but only PvE and Camp
    /// are actually resolvable today, via ForestLocationFactory's separate linear map — arriving at
    /// a node on the branching map doesn't yet start its fight/event/center visit.</summary>
    public enum NodeType
    {
        PvE,
        Event,
        PvP,
        Camp,
        Gym
    }
}
