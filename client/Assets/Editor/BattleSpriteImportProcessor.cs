using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Applies the import settings the battle sprites under
    /// Assets/Art/Pokemon-Sprites/animated_sprites need, the same way PokemonSpriteImportProcessor
    /// drives Assets/Art/Pokemon from code rather than per-file Inspector settings.
    ///
    /// Unity's default for a newly-added texture is a plain Texture with no sprite, which is what
    /// these were until this existed: nothing could reference one as a Sprite, so assigning them to
    /// a species asset silently did nothing. That's the setting that actually matters here.
    ///
    /// The rest are the pixel-art settings, and they're the opposite of the ones
    /// PokemonSpriteImportProcessor uses for the official artwork next door — deliberately, because
    /// these are a different kind of image:
    ///
    /// - **Point filtering**, not Bilinear. These are ~50x46 game sprites drawn several times their
    ///   native size on a battle slot. Bilinear would blur every hard pixel edge into mush; Point
    ///   keeps them crisp, which is the entire reason to use this art instead of the artwork.
    /// - **Uncompressed.** Block compression works on the smooth artwork because it has no hard 1px
    ///   colour bands. Pixel art is nothing but hard 1px colour bands, and DXT artefacts on a 50px
    ///   sprite are visible at the scale these are drawn at. The whole set is small enough that this
    ///   costs little: 366 sprites averaging ~50x50 is a few MB uncompressed.
    /// - **Mipmaps off**, for the same reason as the artwork: these are never minified.
    ///
    /// **On the animation.** These are animated GIFs, and Unity imports the first frame only —
    /// there is no built-in animated-GIF support. So the sprites are correctly-posed stills, not
    /// animations. Making them move needs the frames extracted into a sheet and an animator
    /// component to play them; the GIFs are kept as the source for exactly that, but nothing here
    /// does it yet.</summary>
    public sealed class BattleSpriteImportProcessor : AssetPostprocessor
    {
        private const string FolderPrefix = "Assets/Art/Pokemon-Sprites/animated_sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FolderPrefix) || Path.GetExtension(assetPath) != ".gif")
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
