using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds a standalone Region Map preview scene (design doc §5, PLAN.md Phase 1 item
    /// 5): a scrollable, randomly-generated branching node-map ending in a mandatory Gym node, with
    /// a generic solid-color background. Not wired into the Forest run loop — this is a visual
    /// prototype scene only, per the current scoping for this pass. Re-run via
    /// Pets &gt; Build Region Map Scene after changing RegionMapController's fields.</summary>
    public static class RegionMapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/RegionMap.unity";
        private static readonly Color GenericBackground = new Color(0.29f, 0.42f, 0.29f);

        [MenuItem("Pets/Build Region Map Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas();

            CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, new Vector2(0, 0.94f), Vector2.one);
            var titleText = CreatePlainText(canvasRect, "TitleText", "Region Map (Preview)", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            titleText.fontStyle = FontStyle.Bold;
            AnchorFullRect(titleText.GetComponent<RectTransform>(), new Vector2(0, 0.94f), Vector2.one);

            var (_, _, content) = CreateScrollView(canvasRect, "MapScroll", new Vector2(0.02f, 0f), new Vector2(0.98f, 0.94f), horizontal: false, vertical: true);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(800, 1200);

            var backgroundGO = new GameObject("Background", typeof(RectTransform));
            backgroundGO.transform.SetParent(content, false);
            var background = backgroundGO.AddComponent<Image>();
            background.color = GenericBackground;
            background.raycastTarget = false;
            var backgroundRect = backgroundGO.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = Vector2.zero;

            var controller = new GameObject("RegionMap").AddComponent<RegionMapController>();
            SetField(controller, "content", content);
            SetField(controller, "background", background);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"Region Map scene rebuilt at {ScenePath}");
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
