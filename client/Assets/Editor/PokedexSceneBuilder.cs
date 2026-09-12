using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;
using Pets.Simulation;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Pokédex scene, reached from Home: the whole curated roster in the same
    /// scrollable five-column card grid Character Select uses, with the same Type filter and
    /// Attack/Speed/Health sort toggles, but no picking — a card press fills a detail line in the
    /// bottom bar instead.
    ///
    /// Laid out against the same numbers as CharacterSelectSceneBuilder (see its comments for why
    /// the grid derives its cell width from the column count and pins its chrome in pixels), and
    /// its cards come from the same <see cref="SpeciesGridView"/>, so the two screens stay
    /// recognisably the same screen. Re-run via Pets &gt; Build Pokedex Scene (or
    /// Pets &gt; Build All Scenes) after changing PokedexController's serialized fields.</summary>
    public static class PokedexSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Pokedex + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float SideMargin = 76f;
        private const float TitleHeight = 44f;
        private const float ToolbarHeight = 68f;
        private const float BottomBarHeight = 84f;
        private const float ToolbarButtonWidth = 110f;
        private const float BackButtonWidth = 200f;
        private const int GridColumns = 5;
        private const float GridSpacing = 24f;
        private const float GridPadding = 16f;

        private static float CellWidth =>
            (ReferenceResolution.x - 2f * SideMargin - 2f * GridPadding - (GridColumns - 1) * GridSpacing) / GridColumns;

        [MenuItem("Pets/Build Pokedex Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Pokedex", TitleHeight);

            // Sits in the title bar's right end rather than in the toolbar, which is already full:
            // it's a readout, not a control, and the title bar is where the eye lands first.
            var countText = CreatePlainText(canvasRect, "CountText", string.Empty,
                Theme.FontSizeHeading, TextAnchor.MiddleRight, Theme.TextLight);
            countText.fontStyle = FontStyle.Bold;
            countText.raycastTarget = false;
            var countRect = countText.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 1f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(0.5f, 1f);
            countRect.offsetMin = new Vector2(0f, -TitleHeight);
            countRect.offsetMax = new Vector2(-SideMargin, 0f);

            var toolbar = CreatePanel(canvasRect, "ToolbarBar", Theme.ChromeBg, Vector2.zero, Vector2.one);
            PinToTop(toolbar, TitleHeight, ToolbarHeight);
            AddHorizontalLayout(toolbar, expandHeight: true,
                padding: new RectOffset((int)SideMargin, (int)SideMargin, 2, 2), controlWidth: true);

            var typeOptions = new List<string> { "All Types" };
            typeOptions.AddRange(Enum.GetNames(typeof(PokemonType)));
            var typeFilterDropdown = CreateDropdown(toolbar, "TypeFilterDropdown", typeOptions);

            var sortAttackButton = CreateButton(toolbar, "SortAttackButton", "ATK", Theme.ButtonStyle.Secondary, useSprite: true);
            var sortSpeedButton = CreateButton(toolbar, "SortSpeedButton", "SPD", Theme.ButtonStyle.Secondary, useSprite: true);
            var sortHealthButton = CreateButton(toolbar, "SortHealthButton", "HP", Theme.ButtonStyle.Secondary, useSprite: true);
            var resetButton = CreateButton(toolbar, "ResetFiltersButton", "Reset", Theme.ButtonStyle.Danger, useSprite: true);
            foreach (var button in new[] { sortAttackButton, sortSpeedButton, sortHealthButton, resetButton })
            {
                var layout = button.GetComponent<LayoutElement>();
                layout.flexibleWidth = 0f;
                layout.preferredWidth = ToolbarButtonWidth;
            }

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
            grid.cellSize = new Vector2(CellWidth, SpeciesGridView.CardHeight);
            grid.spacing = new Vector2(GridSpacing, GridSpacing);
            grid.padding = new RectOffset((int)GridPadding, (int)GridPadding, (int)GridPadding, (int)GridPadding);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var backButton = CreateBottomBarButton(canvasRect, "BackButton", "Back", Theme.ButtonStyle.Secondary,
                BottomBarHeight, SideMargin, BackButtonWidth);
            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoHome);

            // Beside the Back button in the bottom bar, the same arrangement DevRoster's status
            // line uses: squeezed into the toolbar there'd be no room left to read it.
            var detailText = CreatePlainText(backButton.transform.parent, "DetailText", string.Empty,
                Theme.FontSizeHeading, TextAnchor.MiddleRight, Theme.TextDark);
            detailText.fontStyle = FontStyle.Bold;
            detailText.raycastTarget = false;
            detailText.resizeTextForBestFit = true;
            detailText.resizeTextMinSize = Theme.FontSizeSmall;
            detailText.resizeTextMaxSize = Theme.FontSizeHeading;
            var detailRect = detailText.GetComponent<RectTransform>();
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.offsetMin = new Vector2(SideMargin + BackButtonWidth + 24f, 0f);
            detailRect.offsetMax = new Vector2(-SideMargin, 0f);

            var controller = new GameObject("Pokedex").AddComponent<PokedexController>();
            SetField(controller, "speciesLibrary",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset"));
            SetField(controller, "gridContainer", content);
            SetField(controller, "countText", countText);
            SetField(controller, "detailText", detailText);
            SetField(controller, "typeFilterDropdown", typeFilterDropdown);
            SetField(controller, "sortAttackButton", sortAttackButton);
            SetField(controller, "sortSpeedButton", sortSpeedButton);
            SetField(controller, "sortHealthButton", sortHealthButton);
            SetField(controller, "typeIconPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath));
            SetField(controller, "healthBarPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.HealthBarPrefabPath));
            SetField(controller, "speedBarPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.SpeedBarPrefabPath));

            // Dropdown.onValueChanged is a UnityEvent<int>, which UnityEventTools can only bake as
            // a fixed constant — so it's wired at runtime by SpeciesRosterToolbar instead.
            UnityEventTools.AddVoidPersistentListener(sortAttackButton.onClick, controller.OnSortAttackClicked);
            UnityEventTools.AddVoidPersistentListener(sortSpeedButton.onClick, controller.OnSortSpeedClicked);
            UnityEventTools.AddVoidPersistentListener(sortHealthButton.onClick, controller.OnSortHealthClicked);
            UnityEventTools.AddVoidPersistentListener(resetButton.onClick, controller.OnResetClicked);

            // Same two kept live as Character Select's grid, and for the same reasons: the content
            // rect is filled with cards at runtime, and ScrollRect itself is an ILayoutController
            // that the bake pass would otherwise destroy along with the scrolling.
            ForceLayoutRebuild(canvasRect, content, scrollRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Pokedex scene rebuilt at {ScenePath}");
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
