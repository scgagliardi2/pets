using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Applies consistent Sprite import settings to the 9-sliced button/panel art under
    /// Assets/Resources/Sprites/UI, the same way the rest of this project drives asset setup from
    /// code rather than hand-tuning import settings per file in the Inspector (see
    /// SceneBuilderUtils/the scene builders). Runs automatically whenever one of these textures is
    /// (re)imported. Border values (in source pixels) come from measuring where each button's flat
    /// fill color begins/ends after the shared 3x3 tile grid the art was delivered in was stitched
    /// into one seamless texture — see the button/text-box PNGs' own history for that grid.</summary>
    public sealed class UiSpriteImportProcessor : AssetPostprocessor
    {
        private const string FolderPrefix = "Assets/Resources/Sprites/UI/";

        // Sprite pixels-per-unit is intentionally low (rather than the default 100): these are
        // chunky pixel-art 9-slices whose border art is a few hundred texture pixels wide, and a
        // low PPU is what makes that border render at a sane on-screen thickness (roughly 8-11px)
        // on a ~44-72px-tall button without every call site needing its own
        // Image.pixelsPerUnitMultiplier tweak.
        private const float PixelsPerUnit = 20f;

        // Border order matches TextureImporter.spriteBorder: (left, bottom, right, top), in the
        // stitched texture's own pixels.
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "ButtonBlue", new Vector4(166, 228, 161, 207) },
            { "ButtonGreen", new Vector4(155, 193, 151, 182) },
            { "ButtonRed", new Vector4(165, 227, 164, 208) },
            { "TextBox", new Vector4(136, 176, 136, 180) },
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FolderPrefix) || !Borders.TryGetValue(Path.GetFileNameWithoutExtension(assetPath), out var border))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
        }
    }
}
