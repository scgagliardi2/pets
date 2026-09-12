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
    /// <summary>Builds the Team screen, reached from the Ingame Menu: the run's current line-up
    /// and Box, plus the one line-up edit that's unambiguous with two active slots — swapping
    /// which mon leads. Fuller team management (promoting out of the Box, reordering a longer
    /// bench) waits on the Box rules in PLAN.md Phase 1.
    ///
    /// Re-run via Pets &gt; Build Team Scene (or Pets &gt; Build All Scenes) after changing
    /// TeamScreenController's or TeamPanelController's serialized fields.</summary>
    public static class TeamSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Team + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 76f;

        [MenuItem("Pets/Build Team Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            var titleBar = CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, new Vector2(0f, 1f), Vector2.one);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.offsetMin = new Vector2(0f, -TitleHeight);
            titleBar.offsetMax = Vector2.zero;

            var titleText = CreatePlainText(canvasRect, "TitleText", "Team", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            titleText.fontStyle = FontStyle.Bold;
            var titleTextRect = titleText.GetComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0f, 1f);
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.pivot = new Vector2(0.5f, 1f);
            titleTextRect.offsetMin = new Vector2(0f, -TitleHeight);
            titleTextRect.offsetMax = Vector2.zero;

            // Stretched to the canvas then inset in pixels, so the readout absorbs whatever
            // vertical room a given aspect ratio leaves between the two fixed-height bars.
            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, Vector2.one);
            content.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            content.offsetMax = new Vector2(-SideMargin, -TitleHeight);

            var teamPanelRect = CreatePanel(content, "TeamPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(teamPanelRect);
            AddPanelHeader(teamPanelRect, "Line-up and Box");
            var lineUpText = CreateText(teamPanelRect, "LineUpText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, 160);
            var boxText = CreateText(teamPanelRect, "BoxText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, 160);
            var teamPanel = teamPanelRect.gameObject.AddComponent<TeamPanelController>();
            SetField(teamPanel, "lineUpText", lineUpText);
            SetField(teamPanel, "boxText", boxText);

            var emptyStateText = CreatePlainText(content, "EmptyStateText",
                "No run in progress.\nStart a New Game from the Home screen.",
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextMuted);
            var emptyStateRect = emptyStateText.GetComponent<RectTransform>();
            emptyStateRect.anchorMin = Vector2.zero;
            emptyStateRect.anchorMax = Vector2.one;
            emptyStateRect.offsetMin = Vector2.zero;
            emptyStateRect.offsetMax = Vector2.zero;

            var bottomBar = CreatePanel(canvasRect, "BottomBar", Color.clear, Vector2.zero, new Vector2(1f, 0f));
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.offsetMin = Vector2.zero;
            bottomBar.offsetMax = new Vector2(0f, BottomBarHeight);
            AddHorizontalLayout(bottomBar, expandHeight: false,
                padding: new RectOffset((int)SideMargin, (int)SideMargin, 10, 10), controlWidth: true);

            var backButton = CreateButton(bottomBar, "BackButton", "Back to Menu", Theme.ButtonStyle.Secondary, useSprite: true);
            var swapButton = CreateButton(bottomBar, "SwapButton", "Swap Lead / Support", Theme.ButtonStyle.Primary, useSprite: true);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();

            var screen = new GameObject("TeamScreen").AddComponent<TeamScreenController>();
            SetField(screen, "teamPanel", teamPanel);
            SetField(screen, "swapButton", swapButton);
            SetField(screen, "emptyStateText", emptyStateText);

            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoToIngameMenu);
            UnityEventTools.AddVoidPersistentListener(swapButton.onClick, screen.OnSwapLeadAndSupportClicked);

            // teamPanelRect stays live: TeamScreenController rewrites both Text bodies at runtime
            // from whatever the run's line-up and Box hold, so the column has to reflow after the
            // scene loads rather than keep the empty-string layout baked in here.
            ForceLayoutRebuild(canvasRect, teamPanelRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Team scene rebuilt at {ScenePath}");
        }
    }
}
