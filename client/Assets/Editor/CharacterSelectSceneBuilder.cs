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
    /// <summary>Builds the Character Select scene (design doc §3): a scrollable grid of the
    /// starter-eligible species with their stats, picked once as a Starter and once as a Secondary.
    /// Which species those are is CharacterSelectController's rule, not this builder's — the scene
    /// is the same grid the Pokédex uses over the whole roster. Built from code like every other
    /// scene in the project — re-run via Pets &gt; Build Character Select Scene after changing
    /// CharacterSelectController's fields.
    ///
    /// Laid out for a phone held horizontally: a landscape reference canvas, chrome pinned to the
    /// top/bottom edges in pixels, and a five-column grid of small cards so a usable chunk of the
    /// roster is visible at once in the little vertical room landscape leaves.</summary>
    public static class CharacterSelectSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

        // Landscape ("horizontal mobile") reference canvas. CanvasScaler matches width by default,
        // so at runtime the canvas is always exactly 1280 units wide and only its height varies
        // with the device aspect (720 at 16:9, ~590 on a 19.5:9 phone). That makes every
        // horizontal number below exact — which is what lets the grid divide into exactly
        // GridColumns columns with no leftover gutter — while vertical chrome is pinned in pixels
        // so a shorter canvas eats into the scrollable grid instead of squashing the toolbar.
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        // Generous side margins: they keep the grid clear of a landscape phone's notch/rounded
        // corners and leave somewhere to rest thumbs without covering cards.
        private const float SideMargin = 76f;
        private const float BottomMargin = 12f;
        private const float TitleHeight = 44f;
        private const float ToolbarHeight = 68f; // 64-high sprite buttons + 2px padding top/bottom
        private const float ToolbarButtonWidth = 110f;
        private const int GridColumns = 5;
        private const float GridSpacing = 24f;
        private const float GridPadding = 16f;

        /// <summary>Card width that divides the grid viewport into exactly GridColumns columns
        /// (200 at the current numbers). Derived rather than hand-typed so changing a margin or the
        /// column count can't silently leave the last column half off-screen — the old hardcoded
        /// 170 cell was how the grid ended up fitting only four columns with a ~180-unit dead gutter
        /// on the right.</summary>
        private static float CellWidth =>
            (ReferenceResolution.x - 2f * SideMargin - 2f * GridPadding - (GridColumns - 1) * GridSpacing) / GridColumns;

        [MenuItem("Pets/Build Character Select Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            // TitleBar and PromptText are separate Canvas-level siblings (rather than PromptText
            // nested inside TitleBar) so CharacterSelectScenePlayModeTests.cs's
            // canvas.Find("PromptText") keeps resolving.
            PinToTop(CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, Vector2.zero, Vector2.one), 0f, TitleHeight);
            var promptText = CreatePlainText(canvasRect, "PromptText", "Choose your Starter", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            promptText.fontStyle = FontStyle.Bold;
            PinToTop(promptText.GetComponent<RectTransform>(), 0f, TitleHeight);

            var toolbar = CreatePanel(canvasRect, "ToolbarBar", Theme.ChromeBg, Vector2.zero, Vector2.one);
            PinToTop(toolbar, TitleHeight, ToolbarHeight);
            // controlWidth: true here (unlike the tab bar / other button rows) so the Dropdown's
            // and sort buttons' explicit LayoutElement widths below actually take effect instead
            // of falling back to each child's raw default RectTransform size. Side padding lines
            // the row up with the grid's own margins rather than with the screen edge.
            AddHorizontalLayout(toolbar, expandHeight: true, padding: new RectOffset((int)SideMargin, (int)SideMargin, 2, 2), controlWidth: true);

            var typeOptions = new List<string> { "All Types" };
            typeOptions.AddRange(Enum.GetNames(typeof(PokemonType)));
            var typeFilterDropdown = CreateDropdown(toolbar, "TypeFilterDropdown", typeOptions);

            var sortAttackButton = CreateButton(toolbar, "SortAttackButton", "ATK", Theme.ButtonStyle.Secondary, useSprite: true);
            var sortSpeedButton = CreateButton(toolbar, "SortSpeedButton", "SPD", Theme.ButtonStyle.Secondary, useSprite: true);
            var sortHealthButton = CreateButton(toolbar, "SortHealthButton", "HP", Theme.ButtonStyle.Secondary, useSprite: true);
            var resetButton = CreateButton(toolbar, "ResetFiltersButton", "Reset", Theme.ButtonStyle.Danger, useSprite: true);
            foreach (var sortButton in new[] { sortAttackButton, sortSpeedButton, sortHealthButton, resetButton })
            {
                var sortButtonLayout = sortButton.GetComponent<LayoutElement>();
                sortButtonLayout.flexibleWidth = 0f;
                sortButtonLayout.preferredWidth = ToolbarButtonWidth;
            }

            // Stretched to the whole canvas and then inset in pixels, so the grid keeps its exact
            // width (and therefore its exact column count) while absorbing whatever vertical room
            // the chrome leaves on a given aspect ratio.
            var (speciesScrollRect, _, content) = CreateScrollView(canvasRect, "SpeciesScroll", Vector2.zero, Vector2.one, horizontal: false, vertical: true);
            var scrollRect = (RectTransform)speciesScrollRect.transform;
            scrollRect.offsetMin = new Vector2(SideMargin, BottomMargin);
            scrollRect.offsetMax = new Vector2(-SideMargin, -(TitleHeight + ToolbarHeight));

            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            // FixedColumnCount rather than the default Flexible: Flexible derives the column count
            // from the cell size, so it silently dropped to four columns (plus a dead gutter) as
            // soon as the cells didn't happen to divide the viewport evenly. Pinning the count and
            // deriving CellWidth from it makes five-per-row the invariant and the cell size the
            // consequence.
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            // Cell height comes from SpeciesGridView, which owns the card's metrics and is shared
            // with the Pokédex — a card that grows must not fit one screen's cell and overflow the
            // other's. At 16:9 it leaves room for two full rows plus over half of a third in the
            // grid viewport (the third row starts at 16 + 2*205 + 2*24 = 474 of the 596 units the
            // chrome leaves); the rest scrolls, same as any longer roster page would.
            grid.cellSize = new Vector2(CellWidth, SpeciesGridView.CardHeight);
            grid.spacing = new Vector2(GridSpacing, GridSpacing);
            grid.padding = new RectOffset((int)GridPadding, (int)GridPadding, (int)GridPadding, (int)GridPadding);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Floats over the bottom of the grid area rather than reserving its own band above it:
            // CharacterSelectController only shows this button once both picks are in, and it
            // clears the grid at the same moment, so the two are never visible together — and the
            // ~95 units a reserved band would cost is a whole extra row of cards in landscape.
            var confirmButton = CreateButton(canvasRect, "ConfirmButton", "Begin Adventure", Theme.ButtonStyle.Confirm, useSprite: true);
            var confirmRect = confirmButton.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.sizeDelta = new Vector2(320f, 64f);
            confirmRect.anchoredPosition = new Vector2(0f, BottomMargin + 16f);
            var confirmLabel = confirmButton.GetComponentInChildren<Text>();

            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var typeIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath);
            var healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.HealthBarPrefabPath);

            var controller = new GameObject("CharacterSelect").AddComponent<CharacterSelectController>();
            SetField(controller, "speciesLibrary", library);
            SetField(controller, "promptText", promptText);
            SetField(controller, "gridContainer", content);
            SetField(controller, "confirmButton", confirmButton);
            SetField(controller, "confirmButtonLabel", confirmLabel);
            SetField(controller, "typeFilterDropdown", typeFilterDropdown);
            SetField(controller, "sortAttackButton", sortAttackButton);
            SetField(controller, "sortSpeedButton", sortSpeedButton);
            SetField(controller, "sortHealthButton", sortHealthButton);
            SetField(controller, "typeIconPrefab", typeIconPrefab);
            SetField(controller, "healthBarPrefab", healthBarPrefab);
            SetField(controller, "speedBarPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.SpeedBarPrefabPath));

            // Dropdown.onValueChanged is a UnityEvent<int> — UnityEventTools only exposes
            // baked-constant persistent listeners (AddIntPersistentListener requires a fixed
            // int), not a dynamic passthrough, so this one is wired at runtime in
            // CharacterSelectController.Start() instead of here.
            UnityEventTools.AddVoidPersistentListener(confirmButton.onClick, controller.OnConfirmClicked);
            UnityEventTools.AddVoidPersistentListener(sortAttackButton.onClick, controller.OnSortAttackClicked);
            UnityEventTools.AddVoidPersistentListener(sortSpeedButton.onClick, controller.OnSortSpeedClicked);
            UnityEventTools.AddVoidPersistentListener(sortHealthButton.onClick, controller.OnSortHealthClicked);
            UnityEventTools.AddVoidPersistentListener(resetButton.onClick, controller.OnResetClicked);

            // Bakes the toolbar's Dropdown/sort/reset controls into an actual row (see
            // ForceLayoutRebuild's doc comment). Two things kept live, not baked-and-destroyed:
            // `content` (GridLayoutGroup) since CharacterSelectController repopulates it with
            // species cards at runtime, and `speciesScrollRect` — ScrollRect itself implements
            // ILayoutGroup (confirmed in UGUI source), so without listing it explicitly it gets
            // silently destroyed by the same bake pass, which is exactly what broke scrolling the
            // starter/secondary grid the first time this shipped.
            ForceLayoutRebuild(canvasRect, content, scrollRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Character Select scene rebuilt at {ScenePath}");
        }

        /// <summary>Stretches a rect across the full canvas width and pins it to the top edge with
        /// a fixed pixel height, <paramref name="offsetFromTop"/> units down. Chrome is sized in
        /// pixels instead of as a fraction of canvas height (which is what this scene did before
        /// going landscape) because the CanvasScaler matches width: on a 19.5:9 phone the canvas is
        /// only ~590 units tall, and a fractional band would shrink the toolbar's 64-high buttons
        /// along with it.</summary>
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
