using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Home screen — the game's first scene (Build Settings index 0) and the
    /// only one reachable without a run in progress. Two buttons for now, New Game and Quit; the
    /// menu is a vertical layout column sized from MenuButtons' own list precisely so that adding
    /// Continue/Options/Collection later is a one-line change here rather than a re-layout.
    ///
    /// Built from code like every other scene in the project — re-run via Pets &gt; Build Home
    /// Scene (or Pets &gt; Build All Scenes) after changing SceneNavigator's methods.</summary>
    public static class HomeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Home + ".unity";

        // Landscape reference canvas, matching CharacterSelectSceneBuilder — see its
        // ReferenceResolution comment for why width-matched units make horizontal numbers exact.
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float MenuButtonWidth = 360f;
        private const float MenuButtonHeight = 64f;
        private const int MenuSpacing = 20;
        // Placeholder working title: "Monster Trails" is the name the UI style guide this
        // project's palette comes from uses (see Pets.UI.Theme) — there's no settled game title
        // yet, and this is the one place a player would read one.
        private const string GameTitle = "Monster Trails";

        [MenuItem("Pets/Build Home Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            var titleText = CreatePlainText(canvasRect, "TitleText", GameTitle, 64, TextAnchor.MiddleCenter, Theme.ChromeBg);
            titleText.fontStyle = FontStyle.Bold;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(0f, -180f);
            titleRect.offsetMax = new Vector2(0f, -80f);

            var subtitleText = CreatePlainText(canvasRect, "SubtitleText",
                "A Pokemon roguelite auto-battler", Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextMuted);
            var subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.offsetMin = new Vector2(0f, -214f);
            subtitleRect.offsetMax = new Vector2(0f, -182f);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();

            var menu = CreateMenuColumn(canvasRect, buttonCount: 2);
            var newGameButton = CreateButton(menu, "NewGameButton", "New Game", Theme.ButtonStyle.Confirm, useSprite: true);
            var quitButton = CreateButton(menu, "QuitButton", "Quit", Theme.ButtonStyle.Danger, useSprite: true);

            UnityEventTools.AddVoidPersistentListener(newGameButton.onClick, navigator.StartNewGame);
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, navigator.QuitGame);

            ForceLayoutRebuild(canvasRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Home scene rebuilt at {ScenePath}");
        }

        /// <summary>A centered, exactly-sized column for the menu's sprite buttons. The height is
        /// derived from the button count rather than hand-typed because the column's
        /// VerticalLayoutGroup gets baked and destroyed at save time (see ForceLayoutRebuild) —
        /// a column left taller than its contents bakes the buttons against its top edge instead
        /// of centered on screen, and nothing at runtime would ever correct it.</summary>
        private static RectTransform CreateMenuColumn(RectTransform canvasRect, int buttonCount)
        {
            var menu = CreatePanel(canvasRect, "MenuButtons", Color.clear,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            menu.sizeDelta = new Vector2(
                MenuButtonWidth,
                buttonCount * MenuButtonHeight + (buttonCount - 1) * MenuSpacing);
            // Sits below the centre line so the title above it isn't crowded.
            menu.anchoredPosition = new Vector2(0f, -70f);
            AddVerticalLayout(menu, new RectOffset(0, 0, 0, 0), MenuSpacing);
            return menu;
        }
    }
}
