using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pets.EditorTools
{
    /// <summary>Import settings for the Location-map node icons under
    /// Assets/Resources/Sprites/Nodes, alongside the UI/type-badge/species processors.
    ///
    /// These arrived as ~1312x1199 renders and were importing at full size and uncompressed —
    /// about 6 MB of texture each, ~31 MB for the five of them, for art that
    /// RegionMapController draws into a 52-unit node (78 for the Gym's 1.5x scale). Capping the
    /// imported size is the entire point of this processor: 256 leaves headroom for a high-DPI
    /// screen, where the CanvasScaler's ~1.9x factor puts the Gym icon at roughly 146 real pixels,
    /// and still cuts each texture to well under a tenth of a megabyte.
    ///
    /// The source PNGs are deliberately left at full resolution on disk — re-exporting art to
    /// match a layout decision is how you end up unable to change the layout.</summary>
    public sealed class NodeIconImportProcessor : AssetPostprocessor
    {
        private const string FolderPrefix = "Assets/Resources/Sprites/Nodes/";

        /// <summary>Biggest the node art is ever drawn, plus headroom for a high-DPI canvas scale
        /// — see RegionMapController.NodeSize/GymNodeScale.</summary>
        private const int MaxSize = 256;

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
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
