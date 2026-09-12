using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Home screen — the game's first scene (Build Settings index 0) and the
    /// only one reachable without a run in progress. The two ways into a run sit in a centered
    /// column (New Game, plus Continue Run when ActiveRun holds one — HomeScreenController decides
    /// that at runtime); History, Credits and Quit sit in a row along the bottom, since they lead
    /// away from playing rather than into it.
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
        private const float FooterHeight = 88f;
        private const float FooterButtonWidth = 210f;
        private const float SideMargin = 76f;
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
            var continueButton = CreateButton(menu, "ContinueButton", "Continue Run", Theme.ButtonStyle.Primary, useSprite: true);
            var newGameButton = CreateButton(menu, "NewGameButton", "New Game", Theme.ButtonStyle.Confirm, useSprite: true);

            var footer = CreateFooterRow(canvasRect);
            var historyButton = CreateFooterButton(footer, "HistoryButton", "History", Theme.ButtonStyle.Secondary);
            var creditsButton = CreateFooterButton(footer, "CreditsButton", "Credits", Theme.ButtonStyle.Secondary);
            var settingsButton = CreateFooterButton(footer, "SettingsButton", "Settings", Theme.ButtonStyle.Secondary);
            var quitButton = CreateFooterButton(footer, "QuitButton", "Quit", Theme.ButtonStyle.Danger);

            var home = new GameObject("HomeScreen").AddComponent<HomeScreenController>();
            SetField(home, "continueButton", continueButton);

            UnityEventTools.AddVoidPersistentListener(continueButton.onClick, navigator.ContinueRun);
            UnityEventTools.AddVoidPersistentListener(newGameButton.onClick, navigator.StartNewGame);
            UnityEventTools.AddVoidPersistentListener(historyButton.onClick, navigator.GoToHistory);
            UnityEventTools.AddVoidPersistentListener(creditsButton.onClick, navigator.GoToCredits);
            UnityEventTools.AddVoidPersistentListener(settingsButton.onClick, navigator.GoToSettings);
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, navigator.QuitGame);

            // The menu column stays live: HomeScreenController hides Continue Run when there's no
            // run to go back to, and a baked-and-destroyed VerticalLayoutGroup would leave that
            // slot as a hole above New Game instead of recentering the column around it.
            ForceLayoutRebuild(canvasRect, menu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Home scene rebuilt at {ScenePath}");
        }

        /// <summary>A centered, exactly-sized column for the menu's sprite buttons. The height is
        /// derived from the button count rather than hand-typed so adding another primary action
        /// stays a one-line change, and the column is center-aligned so the stack stays centered
        /// whether or not Continue Run is showing.</summary>
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
            menu.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            return menu;
        }

        /// <summary>The bottom row of secondary destinations. Pinned to the bottom edge in pixels
        /// (like the other screens' bars) rather than placed under the menu column, so a shorter
        /// canvas eats the gap between the two instead of pushing buttons off-screen.</summary>
        private static RectTransform CreateFooterRow(RectTransform canvasRect)
        {
            var footer = CreatePanel(canvasRect, "FooterButtons", Color.clear, Vector2.zero, new Vector2(1f, 0f));
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, FooterHeight);
            AddHorizontalLayout(footer, expandHeight: false,
                padding: new RectOffset((int)SideMargin, (int)SideMargin, 12, 12), controlWidth: true);
            var layout = footer.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            return footer;
        }

        private static Button CreateFooterButton(RectTransform footer, string name, string label, Theme.ButtonStyle style)
        {
            var button = CreateButton(footer, name, label, style, useSprite: true);
            var layoutElement = button.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 0f;
            layoutElement.preferredWidth = FooterButtonWidth;
            return button;
        }
    }
}
