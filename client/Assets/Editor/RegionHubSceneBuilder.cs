using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Region Hub (design doc §5.2): the screen between Locations, showing the
    /// three Locations on offer and what's inside each of them. It's the seam a multi-Location run
    /// is made of — before it, beating a Gym ended the run at Home (ADR 0003).
    ///
    /// A row of three cards rather than a grid: three is the offer (RegionHubGenerator.OfferCount),
    /// and they're built at runtime by RegionHubController because their contents depend on the run
    /// — which is why the row's HorizontalLayoutGroup is kept live through ForceLayoutRebuild
    /// instead of being baked and destroyed like static chrome.
    ///
    /// This scene owns a RunBootstrapper for the same reason the Map scene does: it is now the
    /// first screen a new run lands on after Character Select, so it has to be able to turn the
    /// chosen pair into a RunState.
    ///
    /// Re-run via Pets &gt; Build Region Hub Scene (or Pets &gt; Build All Scenes).</summary>
    public static class RegionHubSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.RegionHub + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float HeaderHeight = 96f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 76f;
        private const float CardSpacing = 24f;
        private const float BottomBarButtonWidth = 220f;

        [MenuItem("Pets/Build Region Hub Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Region", TitleHeight);

            var header = CreatePanel(canvasRect, "Header", Color.clear, Vector2.zero, Vector2.one);
            PinToTop(header, TitleHeight, HeaderHeight);
            var headlineText = CreatePlainText(header, "HeadlineText", "Where to first?",
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);
            var headlineRect = headlineText.GetComponent<RectTransform>();
            headlineRect.anchorMin = new Vector2(0f, 0.5f);
            headlineRect.anchorMax = new Vector2(1f, 1f);
            headlineRect.offsetMin = new Vector2(SideMargin, 0f);
            headlineRect.offsetMax = new Vector2(-SideMargin, 0f);

            var progressText = CreatePlainText(header, "ProgressText", string.Empty,
                Theme.FontSizeBody, TextAnchor.MiddleCenter, Theme.TextMuted);
            var progressRect = progressText.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 0f);
            progressRect.anchorMax = new Vector2(1f, 0.5f);
            progressRect.offsetMin = new Vector2(SideMargin, 0f);
            progressRect.offsetMax = new Vector2(-SideMargin, 0f);

            var offerRow = CreatePanel(canvasRect, "OfferRow", Color.clear, Vector2.zero, Vector2.one);
            offerRow.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            offerRow.offsetMax = new Vector2(-SideMargin, -(TitleHeight + HeaderHeight));
            var rowLayout = offerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = CardSpacing;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            // Team on the left through the shared helper; Home anchored to the right edge of the
            // same bar by hand, since CreateBottomBarButton only knows how to place one button and
            // calling it twice would stack them on top of each other.
            var teamButton = CreateBottomBarButton(canvasRect, "TeamButton", "Team",
                Theme.ButtonStyle.Secondary, BottomBarHeight, SideMargin, buttonWidth: BottomBarButtonWidth);
            UnityEventTools.AddVoidPersistentListener(teamButton.onClick, navigator.GoToTeam);

            var homeButton = CreateButton((RectTransform)teamButton.transform.parent, "HomeButton",
                "Quit to Home", Theme.ButtonStyle.Danger, useSprite: true);
            AnchorToBarRight(homeButton, BottomBarButtonWidth, SideMargin);
            UnityEventTools.AddVoidPersistentListener(homeButton.onClick, navigator.GoHome);

            var hub = new GameObject("RegionHubScreen").AddComponent<RegionHubController>();
            SetField(hub, "headlineText", headlineText);
            SetField(hub, "progressText", progressText);
            SetField(hub, "offerRow", offerRow);

            CreateBootstrapper();

            // The offer row stays live: its cards are instantiated by the controller at runtime, so
            // a baked-and-destroyed layout group would leave them all stacked at the origin.
            ForceLayoutRebuild(canvasRect, offerRow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Region Hub scene rebuilt at {ScenePath}");
        }

        /// <summary>Stretches a rect across the canvas width and pins it below whatever chrome sits
        /// above it. Each scene builder keeps its own copy of this (see CharacterSelectSceneBuilder)
        /// rather than sharing one, since they differ in what they pin under.</summary>
        private static void PinToTop(RectTransform rect, float offsetFromTop, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(offsetFromTop + height));
            rect.offsetMax = new Vector2(0f, -offsetFromTop);
        }

        /// <summary>Places a button against the right edge of the bottom bar, mirroring what
        /// CreateBottomBarButton does on the left.</summary>
        private static void AnchorToBarRight(Button button, float width, float sideMargin)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 64f);
            rect.anchoredPosition = new Vector2(-sideMargin, 0f);
        }

        /// <summary>Starting Lead/Support for a run begun without going through Character Select
        /// (opening this scene directly in the Editor). A real hand-off overrides these via
        /// PendingRunSelection — see RunBootstrapper. Same pair as the Map scene's.</summary>
        private static void CreateBootstrapper()
        {
            var bootstrapper = new GameObject("RunBootstrapper").AddComponent<RunBootstrapper>();
            SetField(bootstrapper, "speciesLibrary",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset"));
            SetField(bootstrapper, "starterLead",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/charmander.asset"));
            SetField(bootstrapper, "starterSupport",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/squirtle.asset"));
        }
    }
}
