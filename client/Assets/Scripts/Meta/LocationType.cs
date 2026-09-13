namespace Pets.Meta
{
    /// <summary>The kinds of Location a Region can offer (design doc §4's table). Each biases the
    /// wild encounters inside it toward a few Pokémon types — see LocationCatalog, which owns the
    /// mapping and the flavor.</summary>
    public enum LocationType
    {
        Town,
        City,
        Dungeon,
        Cave,
        Forest,
        Sea,
        Plains,
        Desert,
        Mountain
    }
}
