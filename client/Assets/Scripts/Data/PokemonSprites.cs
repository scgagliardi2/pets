using UnityEngine;

namespace Pets.Data
{
    /// <summary>Single place a species' artwork is read, so the screens that draw a Pokémon
    /// (Character Select, Team, the battle screen later) don't each reach into the asset's fields.
    ///
    /// Trivial now that `PokemonSpeciesDefinitionAsset.Sprite` is a direct reference: the sprite is
    /// loaded by Unity as part of loading the species asset, so there's nothing to look up or
    /// cache. It used to call Resources.Load on a path string per call — which the sprites' move
    /// out of Assets/Resources (see Assets/Art/README.md) removed along with the per-call lookup.
    /// Kept as the one accessor anyway, so a future change (an item-granted alternate sprite, a
    /// shiny variant) has one place to land.</summary>
    public static class PokemonSprites
    {
        public static Sprite Load(PokemonSpeciesDefinitionAsset species) => species?.Sprite;
    }
}
