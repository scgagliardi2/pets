using UnityEngine;

namespace Pets.Data
{
    /// <summary>Single place a species' artwork is read, so the screens that draw a Pokémon don't
    /// each reach into the asset's fields and each make their own decision about which of the three
    /// images to use.
    ///
    /// There are three, and which one is right depends on where the Pokémon is standing:
    /// - <see cref="LoadBack"/> — the player's own Lead and Support on the battlefield. The
    ///   over-the-shoulder view the main-series games use: your mon faces away, the foe faces you.
    /// - <see cref="LoadFront"/> — everywhere else. Cards, the Pokédex, the party strip, catch
    ///   offers, the evolution overlay, and the foe's side of the battlefield.
    /// - <see cref="Load"/> — the original PokeAPI official artwork. No longer drawn by default; it
    ///   stays as the fallback beneath the other two, and as the image for anywhere that genuinely
    ///   wants the illustration rather than the game sprite.
    ///
    /// Both sprite accessors fall back to the artwork, because the sprite set doesn't quite cover
    /// the roster (see SpeciesRosterImporter) — a species with no back sprite drawing nothing would
    /// be a blank slot mid-battle, where drawing its artwork is merely inconsistent.</summary>
    public static class PokemonSprites
    {
        /// <summary>The PokeAPI official artwork — smooth illustration, not the game sprite.</summary>
        public static Sprite Load(PokemonSpeciesDefinitionAsset species) => species?.Sprite;

        /// <summary>The front-facing battle sprite, falling back to the artwork.</summary>
        public static Sprite LoadFront(PokemonSpeciesDefinitionAsset species) =>
            species == null ? null : (species.FrontSprite != null ? species.FrontSprite : species.Sprite);

        /// <summary>The back-facing battle sprite, for the player's own mons on the field.
        ///
        /// Falls back to the *front* sprite before the artwork: a species missing only its back
        /// sprite is far better served by its own front sprite (same art style, same scale, merely
        /// facing the wrong way) than by jumping to a piece of illustration that will look nothing
        /// like the mon standing opposite it.</summary>
        public static Sprite LoadBack(PokemonSpeciesDefinitionAsset species) =>
            species == null ? null
                : species.BackSprite != null ? species.BackSprite
                : species.FrontSprite != null ? species.FrontSprite
                : species.Sprite;
    }
}
