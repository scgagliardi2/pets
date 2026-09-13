namespace Pets.Meta
{
    /// <summary>Location node-map node kinds (design doc §5.1) — shown to the player as Battle
    /// (PvE), Encounter (Event), Mystery Trainer (PvP), Pokémon Center (Camp), and Gym (see
    /// RegionMapController's NodeDisplayNames for that flavor mapping). Arriving at a node resolves
    /// it (Gameplay/NodeResolutionController, ADR 0003): PvE and Gym hand a fight to the Battle
    /// screen and Camp rests the team, while Event and PvP still show an honest "not built yet"
    /// modal.</summary>
    public enum NodeType
    {
        PvE,
        Event,
        PvP,
        Camp,
        Gym
    }
}
