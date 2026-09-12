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
    /// <summary>Builds the dev roster screen, reached from the Ingame Menu: the same scrollable
    /// five-column card grid Character Select uses, but every card adds that species to the run in
    /// progress instead of picking a starter. A destination toggle chooses party or Box.
    ///
    /// Laid out against the same numbers as CharacterSelectSceneBuilder — see its comments for why
    /// the grid derives its cell width from the column count and pins its chrome in pixels — so
    /// the two screens stay recognisably the same screen. Re-run via
    /// Pets &gt; Build Dev Roster Scene (or Pets &gt; Build All Scenes) after changing
    /// DevRosterController's serialized fields.</summary>
    public static class DevRosterSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.DevRoster + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float SideMargin = 76f;
        private const float TitleHeight = 44f;
        private const float ToolbarHeight = 68f;
        private const float BottomBarHeight = 84f;
        private const float ToolbarButtonWidth = 170f;
        private const float BackButtonWidth = 220f;
        private const int GridColumns = 5;
        private const float GridSpacing = 24f;
        private const float GridPadding = 16f;
        private const float CardHeight = 186f;

        private static float CellWidth =>
            (ReferenceResolution.x - 2f * SideMargin - 2f * GridPadding - (GridColumns - 1) * GridSpacing) / GridColumns;

        [MenuItem("Pets/Build Dev Roster Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Dev: Add Pokemon", TitleHeight);

            var toolbar = CreatePanel(canvasRect, "ToolbarBar", Theme.ChromeBg, Vector2.zero, Vector2.one);
            PinToTop(toolbar, TitleHeight, ToolbarHeight);
            AddHorizontalLayout(toolbar, expandHeight: true,
                padding: new RectOffset((int)SideMargin, (int)SideMargin, 2, 2), controlWidth: true);

            var partyTargetButton = CreateTargetButton(toolbar, "TargetPartyButton", "To Party");
            var boxTargetButton = CreateTargetButton(toolbar, "TargetBoxButton", "To Box");

            var (speciesScrollRect, _, content) = CreateScrollView(canvasRect, "SpeciesScroll", Vector2.zero, Vector2.one, horizontal: false, vertical: true);
            var scrollRect = (RectTransform)speciesScrollRect.transform;
            scrollRect.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            scrollRect.offsetMax = new Vector2(-SideMargin, -(TitleHeight + ToolbarHeight));

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.cellSize = new Vector2(CellWidth, CardHeight);
            grid.spacing = new Vector2(GridSpacing, GridSpacing);
            grid.padding = new RectOffset((int)GridPadding, (int)GridPadding, (int)GridPadding, (int)GridPadding);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var emptyStateText = CreatePlainText(canvasRect, "EmptyStateText",
                "No run in progress.\nStart a run first — there's nothing to add mons to.",
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextMuted);
            var emptyStateRect = emptyStateText.GetComponent<RectTransform>();
            emptyStateRect.anchorMin = Vector2.zero;
            emptyStateRect.anchorMax = Vector2.one;
            emptyStateRect.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            emptyStateRect.offsetMax = new Vector2(-SideMargin, -(TitleHeight + ToolbarHeight));

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var backButton = CreateBottomBarButton(canvasRect, "BackButton", "Back to Menu",
                Theme.ButtonStyle.Secondary, BottomBarHeight, SideMargin, BackButtonWidth);
            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoToIngameMenu);

            // Carries the run's current counts and what the last click did, so the effect of a
            // card press is visible without leaving the screen to check the Team tab. In the
            // bottom bar beside the Back button rather than in the toolbar: squeezed in next to
            // the target toggles there was no room left to read it.
            var statusText = CreatePlainText(backButton.transform.parent, "StatusText", string.Empty,
                Theme.FontSizeHeading, TextAnchor.MiddleRight, Theme.TextDark);
            statusText.fontStyle = FontStyle.Bold;
            statusText.raycastTarget = false;
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(SideMargin + BackButtonWidth + 24f, 0f);
            statusRect.offsetMax = new Vector2(-SideMargin, 0f);

            var controller = new GameObject("DevRoster").AddComponent<DevRosterController>();
            SetField(controller, "speciesLibrary",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset"));
            SetField(controller, "gridContainer", content);
            SetField(controller, "statusText", statusText);
            SetField(controller, "partyTargetButton", partyTargetButton);
            SetField(controller, "boxTargetButton", boxTargetButton);
            SetField(controller, "emptyStateText", emptyStateText);
            SetField(controller, "typeIconPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath));

            UnityEventTools.AddVoidPersistentListener(partyTargetButton.onClick, controller.OnTargetPartyClicked);
            UnityEventTools.AddVoidPersistentListener(boxTargetButton.onClick, controller.OnTargetBoxClicked);

            // Same two kept live as Character Select's grid, and for the same reasons: the content
            // rect is filled with cards at runtime, and ScrollRect itself is an ILayoutController
            // that the bake pass would otherwise destroy along with the scrolling.
            ForceLayoutRebuild(canvasRect, content, scrollRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Dev Roster scene rebuilt at {ScenePath}");
        }

        private static Button CreateTargetButton(RectTransform toolbar, string name, string label)
        {
            var button = CreateButton(toolbar, name, label, Theme.ButtonStyle.Primary, useSprite: true);
            var layout = button.GetComponent<LayoutElement>();
            layout.flexibleWidth = 0f;
            layout.preferredWidth = ToolbarButtonWidth;
            return button;
        }

        /// <summary>Stretches a rect across the canvas width and pins it to the top edge with a
        /// fixed pixel height, <paramref name="offsetFromTop"/> units down — the same pixel-pinned
        /// chrome CharacterSelectSceneBuilder uses, for the same aspect-ratio reason.</summary>
        private static void PinToTop(RectTransform rect, float offsetFromTop, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(offsetFromTop + height));
            rect.offsetMax = new Vector2(0f, -offsetFromTop);
        }
    }
}
