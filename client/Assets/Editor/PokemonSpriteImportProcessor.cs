using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Applies the Sprite import settings the species artwork under Assets/Art/Pokemon
    /// needs, the same way UiSpriteImportProcessor and TypeIconImportProcessor drive their folders
    /// from code rather than per-file Inspector settings. Runs automatically whenever one of these
    /// textures is (re)imported.
    ///
    /// These 183 files are the project's entire texture budget in practice — 256x256 each — so the
    /// two settings that matter here are the two Unity gets wrong by default for UI art:
    ///
    /// - **Mipmaps off.** A species sprite is drawn on a card or a battle slot at roughly its
    ///   native size and is never minified, so the mip chain is never sampled — it just costs 33%
    ///   more memory per texture and lets the GPU pick a blurrier level when the card is a hair
    ///   under 1:1.
    /// - **Compressed.** Uncompressed RGBA32 is 256 KB per sprite; across the roster that was
    ///   ~62 MB of texture memory (with mips) for art that block compression handles cleanly. This
    ///   is smooth photographic-ish artwork with no hard 1px colour bands, so it has none of the
    ///   banding problem that makes UiSpriteImportProcessor opt out of compression for the 12x12
    ///   nine-sliced chrome.</summary>
    public sealed class PokemonSpriteImportProcessor : AssetPostprocessor
    {
        private const string FolderPrefix = "Assets/Art/Pokemon/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FolderPrefix) || Path.GetExtension(assetPath) != ".png")
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;

            // Bilinear like the type badges (and unlike the UI nine-slices): this is smooth,
            // anti-aliased artwork, so Point sampling would only add jaggies.
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
