using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Pets.EditorTools
{
    /// <summary>Creates/refreshes the project's sprite atlases from code, same as the scene and
    /// prefab builders — an atlas is a serialized asset with a dozen settings that are easy to get
    /// subtly wrong by hand and impossible to review in a diff.
    ///
    /// Why atlases at all: uGUI batches draw calls by material+texture, and it can only merge
    /// elements that are adjacent in draw order. Character Select's grid interleaves a different
    /// Pokemon texture, a shared card background and two type badges per card, so without an atlas
    /// every card costs several draw calls and the grid runs to dozens. With the art packed, the
    /// whole screen collapses toward a handful.
    ///
    /// An atlas imposes one filter mode and one compression setting on everything inside it, so the
    /// split below follows the art's needs rather than its folder layout — packing Point-filtered
    /// pixel art together with smooth badge art would quietly undo one of the two:
    /// - **UIChrome** — the 12x12 nine-sliced button/panel art. Point-filtered and uncompressed,
    ///   matching UiSpriteImportProcessor: bilinear smears its 1px bevel and block compression
    ///   bleeds its hard colour bands into each other.
    /// - **Icons** — type badges and Location-map node icons. Bilinear, since these are smooth
    ///   anti-aliased art that Point sampling would only add jaggies to.
    /// - **Pokemon** — the species artwork, and the bulk of the texture budget. Bilinear and
    ///   compressed.
    ///
    /// All three are plain bound atlases (include-in-build, not variants), so an existing Sprite
    /// reference or Resources.Load transparently resolves to the packed texture — no
    /// SpriteAtlasManager late-binding callback needed.</summary>
    public static class SpriteAtlasBuilder
    {
        private const string AtlasFolder = "Assets/Art/Atlases";

        [MenuItem("Pets/Build Sprite Atlases")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(AtlasFolder);

            // Packing is off by default in a fresh project, which would leave every atlas below a
            // file nothing reads. AlwaysOnAtlas rather than BuildTimeOnlyAtlas so Play mode and the
            // PlayMode tests exercise the same atlased sprites a build will, instead of only
            // finding out at build time.
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
            {
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
                Debug.Log($"Sprite packer mode set to {EditorSettings.spritePackerMode}.");
            }

            Build("UIChrome", FilterMode.Point, compressed: false, padding: 2, new[]
            {
                "Assets/Resources/Sprites/UI",
            });

            Build("Icons", FilterMode.Bilinear, compressed: true, padding: 4, new[]
            {
                "Assets/Resources/Sprites/Types",
                "Assets/Resources/Sprites/Nodes",
            });

            // Padding 4 rather than the default 2: these are 256px sprites drawn smaller on a card,
            // so bilinear sampling at the edge of a cell can otherwise pull in a neighbour.
            Build("Pokemon", FilterMode.Bilinear, compressed: true, padding: 4, new[]
            {
                "Assets/Art/Pokemon",
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sprite atlases rebuilt.");
        }

        private static void Build(string name, FilterMode filterMode, bool compressed, int padding, string[] folders)
        {
            string path = $"{AtlasFolder}/{name}.spriteatlas";

            // Always authored fresh rather than loaded and edited: every setting below is set from
            // code, so there is no hand-made state worth preserving, and rebuilding from scratch
            // can't leave a stale packable behind. Saving over an existing path keeps the .meta,
            // and therefore the GUID anything referencing the atlas uses.
            var atlas = new SpriteAtlasAsset();

            atlas.SetIncludeInBuild(true);
            atlas.SetIsVariant(false);

            var packing = new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
                padding = padding,
                blockOffset = 1,
            };
            atlas.SetPackingSettings(packing);

            var texture = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = filterMode,
            };
            atlas.SetTextureSettings(texture);

            var platform = new TextureImporterPlatformSettings
            {
                maxTextureSize = 4096,
                format = TextureImporterFormat.Automatic,
                textureCompression = compressed
                    ? TextureImporterCompression.Compressed
                    : TextureImporterCompression.Uncompressed,
                crunchedCompression = false,
                overridden = true,
            };
            atlas.SetPlatformSettings(platform);

            // Packing whole folders rather than individual sprites: a species sprite added to
            // Assets/Art/Pokemon is then packed without anyone having to remember to touch this
            // file, which is the same reason ContentIntegrityTests walks the folder.
            var objects = new List<Object>();
            foreach (var folder in folders)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(folder);
                if (asset == null)
                {
                    Debug.LogWarning($"Sprite atlas '{name}': folder '{folder}' not found, skipping.");
                    continue;
                }
                objects.Add(asset);
            }
            atlas.Add(objects.ToArray());

            SpriteAtlasAsset.Save(atlas, path);
            Debug.Log($"Sprite atlas '{name}': {objects.Count} folder(s) packed -> {path}");
        }
    }
}
