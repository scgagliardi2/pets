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
    /// <summary>Builds the Settings screen: one row per setting (label on the left, an On/Off
    /// button on the right) with a line of explanation under it, and Back in the bottom bar.
    /// Re-run via Pets &gt; Build Settings Scene (or Pets &gt; Build All Scenes) after changing
    /// SettingsScreenController's serialized fields.</summary>
    public static class SettingsSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Settings + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 76f;
        private static readonly Vector2 RowSize = new Vector2(640f, 64f);
        private const float ToggleWidth = 180f;

        [MenuItem("Pets/Build Settings Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Settings", TitleHeight);

            var row = CreatePanel(canvasRect, "AutoplayRow", Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            row.sizeDelta = RowSize;
            row.anchoredPosition = new Vector2(0f, 40f);

            var label = CreatePlainText(row, "AutoplayLabel", "Auto-play battles", Theme.FontSizeTitle, TextAnchor.MiddleLeft, Theme.TextDark);
            label.fontStyle = FontStyle.Bold;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = new Vector2(-(ToggleWidth + 20f), 0f);

            var toggle = CreateButton(row, "AutoplayToggleButton", "On", Theme.ButtonStyle.Confirm, useSprite: true);
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.sizeDelta = new Vector2(ToggleWidth, RowSize.y);
            toggleRect.anchoredPosition = Vector2.zero;

            var hint = CreatePlainText(canvasRect, "AutoplayHint",
                "When on, battles start playing by themselves. You can pause or step through at any time.",
                Theme.FontSizeBody, TextAnchor.UpperLeft, Theme.TextMuted);
            hint.fontStyle = FontStyle.Bold;
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(RowSize.x, 48f);
            hintRect.anchoredPosition = new Vector2(0f, -20f);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var backButton = CreateBottomBarButton(canvasRect, "BackButton", "Back",
                Theme.ButtonStyle.Secondary, BottomBarHeight, SideMargin);

            var controller = new GameObject("SettingsScreen").AddComponent<SettingsScreenController>();
            SetField(controller, "autoplayToggleButton", toggle);

            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.ReturnFromSettings);
            UnityEventTools.AddVoidPersistentListener(toggle.onClick, controller.OnAutoplayToggleClicked);

            ForceLayoutRebuild(canvasRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Settings scene rebuilt at {ScenePath}");
        }
    }
}
