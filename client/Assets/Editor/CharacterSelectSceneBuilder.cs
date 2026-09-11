using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Character Select scene (design doc §3): a scrollable grid of every
    /// curated species with its stats, picked once as a Starter and once as a Secondary. Built
    /// from code for the same reason as ForestSceneBuilder — re-run via
    /// Pets &gt; Build Character Select Scene after changing CharacterSelectController's fields.</summary>
    public static class CharacterSelectSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/CharacterSelect.unity";
        private static readonly Color BackgroundBg = new Color(0.9f, 0.92f, 0.86f);

        [MenuItem("Pets/Build Character Select Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(new Color(0.15f, 0.18f, 0.15f));
            CreateEventSystem();
            var canvasRect = CreateCanvas();

            CreatePanel(canvasRect, "Background", BackgroundBg, Vector2.zero, Vector2.one);

            var promptText = CreateText(canvasRect, "PromptText", "Choose your Starter", 26, TextAnchor.MiddleCenter, 60);
            AnchorFullRect(promptText.GetComponent<RectTransform>(), new Vector2(0, 0.9f), Vector2.one);

            var (_, _, content) = CreateScrollView(canvasRect, "SpeciesScroll", new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.9f), horizontal: false, vertical: true);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 230);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var confirmButton = CreateButton(canvasRect, "ConfirmButton", "Begin Adventure");
            AnchorFullRect(confirmButton.GetComponent<RectTransform>(), new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            var confirmLabel = confirmButton.GetComponentInChildren<Text>();

            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");

            var controller = new GameObject("CharacterSelect").AddComponent<CharacterSelectController>();
            SetField(controller, "speciesLibrary", library);
            SetField(controller, "promptText", promptText);
            SetField(controller, "gridContainer", content);
            SetField(controller, "confirmButton", confirmButton);
            SetField(controller, "confirmButtonLabel", confirmLabel);

            UnityEventTools.AddVoidPersistentListener(confirmButton.onClick, controller.OnConfirmClicked);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SetBuildScenes(ScenePath, ForestSceneBuilder.ScenePath);

            Debug.Log($"Character Select scene rebuilt at {ScenePath}");
        }

        private static void AnchorFullRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
