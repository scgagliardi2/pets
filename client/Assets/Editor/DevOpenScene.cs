using UnityEditor;
using UnityEditor.SceneManagement;

namespace Pets.EditorTools
{
    /// <summary>Temporary dev helper to open a scene and enter Play mode from the command line
    /// (-executeMethod) for quick visual review. Not part of the game itself — safe to delete
    /// after use.</summary>
    public static class DevOpenScene
    {
        [MenuItem("Pets/Dev/Open Game Scene And Play")]
        public static void OpenGameAndPlay()
        {
            EditorSceneManager.OpenScene(ForestSceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Pets/Dev/Open Character Select Scene And Play")]
        public static void OpenCharacterSelectAndPlay()
        {
            EditorSceneManager.OpenScene(CharacterSelectSceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
