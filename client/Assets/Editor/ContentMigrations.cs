using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Pets.Data;

namespace Pets.EditorTools
{
    /// <summary>One-off content migrations, kept in the repo rather than run-and-deleted so the
    /// history explains how the authored assets got their current shape (and so a migration can be
    /// re-run if a branch merge resurrects pre-migration assets).
    ///
    /// Each entry here is expected to become a no-op once it has run — a migration that still
    /// finds work to do on a clean tree means something regressed.</summary>
    public static class ContentMigrations
    {
        /// <summary>Species sprites moved from Assets/Resources/Sprites/Pokemon (loaded by the
        /// string path in a since-removed `SpriteSource` field) to Assets/Art/Pokemon, referenced
        /// directly by `PokemonSpeciesDefinitionAsset.Sprite`. This walks the species assets and
        /// fills in the new reference from the species Id, which is what the old path encoded.
        ///
        /// Idempotent: a species that already has a Sprite is left alone.</summary>
        [MenuItem("Pets/Migrations/Assign Species Sprites From Art Folder")]
        public static void AssignSpeciesSprites()
        {
            var assigned = new List<string>();
            var missing = new List<string>();
            var alreadySet = 0;

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(PokemonSpeciesDefinitionAsset)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var species = AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>(path);
                if (species == null)
                {
                    continue;
                }
                if (species.Sprite != null)
                {
                    alreadySet++;
                    continue;
                }

                string spritePath = $"Assets/Art/Pokemon/{species.Id}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    missing.Add($"{species.DisplayName} (id {species.Id}) — no sprite at {spritePath}");
                    continue;
                }

                species.Sprite = sprite;
                EditorUtility.SetDirty(species);
                assigned.Add($"{species.DisplayName} -> {Path.GetFileName(spritePath)}");
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"Species sprite migration: {assigned.Count} assigned, {alreadySet} already set, " +
                      $"{missing.Count} missing.\n" +
                      string.Join("\n", assigned) +
                      (missing.Count > 0 ? "\nMISSING:\n" + string.Join("\n", missing) : string.Empty));

            if (missing.Count > 0)
            {
                Debug.LogError($"{missing.Count} species could not resolve a sprite — see the list above.");
            }
        }
    }
}
