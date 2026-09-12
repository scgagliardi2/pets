using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;
using Pets.UI;

namespace Pets.EditorTools
{
    /// <summary>Builds the reusable TypeIcon prefab (Pets.UI.TypeIconView) that renders one of the
    /// 18 type-badge icons under Assets/Resources/Sprites/Types — generated from code like the
    /// rest of this project's widget prefabs (see UiPrefabBuilder), and instantiated at runtime by
    /// CharacterSelectController rather than baked into a scene, since the species grid is built
    /// dynamically. Re-run via Pets &gt; Build Type Icon Prefab any time TypeIconView's serialized
    /// fields change shape.</summary>
    public static class TypeIconPrefabBuilder
    {
        private const string FolderPath = "Assets/Prefabs/UI";
        public const string PrefabPath = FolderPath + "/TypeIcon.prefab";

        private static readonly Vector2 IconSize = new Vector2(28f, 28f);

        [MenuItem("Pets/Build Type Icon Prefab")]
        public static void Build()
        {
            EnsureFolder();

            var go = new GameObject("TypeIcon", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = IconSize;

            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            // Non-interactive badge — shouldn't steal clicks meant for the card Button it sits on.
            image.raycastTarget = false;

            var view = go.AddComponent<TypeIconView>();
            SceneBuilderUtils.SetField(view, "icon", image);
            // Also applies Theme.TypeIconSprite(Normal) so the prefab previews correctly rather
            // than showing a blank Image when opened in Prefab Mode.
            view.Type = PokemonType.Normal;

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log($"Type icon prefab rebuilt at {PrefabPath}");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
        }
    }
}
