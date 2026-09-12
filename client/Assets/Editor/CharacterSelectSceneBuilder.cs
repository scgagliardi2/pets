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
    /// <summary>Builds the Character Select scene (design doc §3): a scrollable grid of every
    /// curated species with its stats, picked once as a Starter and once as a Secondary. Built
    /// from code for the same reason as ForestSceneBuilder — re-run via
    /// Pets &gt; Build Character Select Scene after changing CharacterSelectController's fields.</summary>
    public static class CharacterSelectSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

        [MenuItem("Pets/Build Character Select Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas();

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            // TitleBar and PromptText are separate Canvas-level siblings (rather than PromptText
            // nested inside TitleBar) so CharacterSelectScenePlayModeTests.cs's
            // canvas.Find("PromptText") keeps resolving.
            CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, new Vector2(0f, 0.9f), Vector2.one);
            var promptText = CreatePlainText(canvasRect, "PromptText", "Choose your Starter", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            promptText.fontStyle = FontStyle.Bold;
            AnchorFullRect(promptText.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);

            var toolbar = CreatePanel(canvasRect, "ToolbarBar", Theme.ChromeBg, new Vector2(0f, 0.81f), new Vector2(1f, 0.9f));
            // controlWidth: true here (unlike the tab bar / other button rows) so the Dropdown's
            // and sort buttons' explicit LayoutElement widths below actually take effect instead
            // of falling back to each child's raw default RectTransform size.
            AddHorizontalLayout(toolbar, expandHeight: true, padding: new RectOffset(12, 12, 8, 8), controlWidth: true);

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
                sortButtonLayout.preferredWidth = 90f;
            }

            var (speciesScrollRect, _, content) = CreateScrollView(canvasRect, "SpeciesScroll", new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.81f), horizontal: false, vertical: true);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            // Height accounts for Sprite(96) + Name(24) + TypesRow(30) + ATK/HP/SPD(20 each) +
            // VerticalLayoutGroup spacing/padding (see CharacterSelectController.CreateCard) —
            // TypesRow replaced what used to be a single 20px type-name text line, so this is
            // taller than before by roughly that difference.
            grid.cellSize = new Vector2(170, 246);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var confirmButton = CreateButton(canvasRect, "ConfirmButton", "Begin Adventure", Theme.ButtonStyle.Confirm, useSprite: true);
            AnchorFullRect(confirmButton.GetComponent<RectTransform>(), new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            var confirmLabel = confirmButton.GetComponentInChildren<Text>();

            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            var typeIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath);

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
            ForceLayoutRebuild(canvasRect, content, (RectTransform)speciesScrollRect.transform);

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
