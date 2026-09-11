using UnityEngine;

namespace Pets.Data
{
    /// <summary>Single place that turns a species' `SpriteSource` (a Resources-relative path, see
    /// content-schema.md §2) into a loaded `Sprite`. Every place a Pokémon appears on screen
    /// (Character Select today; battle screen/node-map/hub later) should go through this rather
    /// than calling Resources.Load directly, so the sprite lookup/caching only lives in one
    /// place.</summary>
    public static class PokemonSprites
    {
        public static Sprite Load(PokemonSpeciesDefinitionAsset species)
        {
            if (species == null || string.IsNullOrEmpty(species.SpriteSource))
            {
                return null;
            }

            return Resources.Load<Sprite>(species.SpriteSource);
        }
    }
}
