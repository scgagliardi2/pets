using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Run History screen, reached from Home: where finished runs will be
    /// listed once there's anything to list them from.
    ///
    /// Deliberately a real screen with an honest empty state rather than a fake list — nothing
    /// records a finished run yet (a run doesn't end anywhere yet, and nothing outlives the
    /// session without the local save layer in PLAN.md Phase 2), so inventing sample rows here is
    /// the one thing this screen must not do. It exists now so Home's navigation is complete and
    /// there's somewhere for the save layer to write into later.
    ///
    /// Re-run via Pets &gt; Build History Scene (or Pets &gt; Build All Scenes).</summary>
    public static class HistorySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.History + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 76f;
        private const float PanelHeaderHeight = 36f;

        [MenuItem("Pets/Build History Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Run History", TitleHeight);

            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, Vector2.one);
            content.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            content.offsetMax = new Vector2(-SideMargin, -TitleHeight);

            var panel = CreatePanel(content, "HistoryPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(panel, new RectOffset(24, 24, 20, 20), 12);
            AddPanelHeader(panel, "Past runs");

            // Stretched over the panel rather than added as a layout child: the panel's
            // VerticalLayoutGroup is baked and destroyed at save time (see ForceLayoutRebuild), so
            // only an anchored rect keeps filling the panel on whatever canvas height the device
            // turns out to have. Inset from the top to clear the panel header.
            var emptyStateText = CreatePlainText(content, "EmptyStateText",
                "No finished runs yet.\nRuns are recorded here once a run can end and be saved.",
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextMuted);
            var emptyStateRect = emptyStateText.GetComponent<RectTransform>();
            emptyStateRect.anchorMin = Vector2.zero;
            emptyStateRect.anchorMax = Vector2.one;
            emptyStateRect.offsetMin = new Vector2(24f, 20f);
            emptyStateRect.offsetMax = new Vector2(-24f, -(PanelHeaderHeight + 20f));

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var backButton = CreateBottomBarButton(canvasRect, "BackButton", "Back to Home",
                Theme.ButtonStyle.Secondary, BottomBarHeight, SideMargin);
            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoHome);

            ForceLayoutRebuild(canvasRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"History scene rebuilt at {ScenePath}");
        }
    }
}
