using UnityEditor;
using UnityEngine;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>The set of scenes that make up the game, in Build Settings order — Home first so
    /// it's what a built player starts on. SceneManager.LoadScene(name) only resolves for scenes
    /// listed in Build Settings, so every scene builder ends by calling EnsureBuildScenes: running
    /// a single builder must not leave the list holding only the scenes that existed the last time
    /// someone ran a different one, which is how a "scene couldn't be loaded" on a button press
    /// would otherwise sneak in.</summary>
    public static class SceneCatalog
    {
        public static readonly string[] AllScenePaths =
        {
            HomeSceneBuilder.ScenePath,
            CharacterSelectSceneBuilder.ScenePath,
            RegionMapSceneBuilder.ScenePath,
            IngameMenuSceneBuilder.ScenePath,
            TeamSceneBuilder.ScenePath,
            HistorySceneBuilder.ScenePath,
            CreditsSceneBuilder.ScenePath,
            DevRosterSceneBuilder.ScenePath,
            BattleSceneBuilder.ScenePath,
            SettingsSceneBuilder.ScenePath,
        };

        public static void EnsureBuildScenes()
        {
            // Refresh first: EditorBuildSettingsScene resolves each path to a GUID through the
            // AssetDatabase, and a scene this same batch run just created isn't imported yet — it
            // would be recorded with an all-zero GUID, which survives only as long as nobody moves
            // or renames the file. Cheap, and makes a from-scratch "Build All Scenes" produce the
            // same Build Settings as a re-run over existing scenes.
            AssetDatabase.Refresh();
            SetBuildScenes(AllScenePaths);
        }

        /// <summary>Rebuilds the shared UI prefabs and then every scene, in one go. The scenes
        /// instantiate those prefabs (CreateSpriteButton / CreateTextBox, and the Map's two node
        /// overlays), so prefab changes only reach a saved scene after that scene is rebuilt — run
        /// this rather than a single scene builder after touching Assets/Prefabs/UI or
        /// Pets.UI.Theme. Every prefab a scene instantiates is built first, so this works on a
        /// checkout with no prefabs at all.</summary>
        [MenuItem("Pets/Build All Scenes")]
        public static void BuildAll()
        {
            UiPrefabBuilder.Build();
            CampOverlayPrefabBuilder.Build();
            NodeEventOverlayPrefabBuilder.Build();
            HomeSceneBuilder.Build();
            CharacterSelectSceneBuilder.Build();
            RegionMapSceneBuilder.Build();
            IngameMenuSceneBuilder.Build();
            TeamSceneBuilder.Build();
            HistorySceneBuilder.Build();
            CreditsSceneBuilder.Build();
            DevRosterSceneBuilder.Build();
            BattleSceneBuilder.Build();
            SettingsSceneBuilder.Build();
            EnsureBuildScenes();
            Debug.Log($"All {AllScenePaths.Length} scenes rebuilt.");
        }
    }
}
