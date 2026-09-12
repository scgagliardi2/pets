using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Applies the Sprite import settings the type-badge icons under
    /// Assets/Resources/Sprites/Types need, the same way UiSpriteImportProcessor drives the
    /// 9-sliced button art from code rather than hand-tuning per-file Inspector settings. Simpler
    /// than that one: each icon here is a single, self-contained badge (a colored circle baked
    /// into the art itself, per-type — see Theme.TypeIconSprite) rather than a 9-sliced shape
    /// meant to stretch, so there's no border to configure. Runs automatically whenever one of
    /// these textures is (re)imported.</summary>
    public sealed class TypeIconImportProcessor : AssetPostprocessor
    {
        private const string FolderPrefix = "Assets/Resources/Sprites/Types/";

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

            // Bilinear, not Point: this is smooth anti-aliased badge art (unlike the UI 9-slice
            // sprites' blown-up pixel art), so Point sampling would only add unwanted jaggies.
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
        }
    }
}
