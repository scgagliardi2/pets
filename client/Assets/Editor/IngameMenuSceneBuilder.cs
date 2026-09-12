using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Ingame Menu — the screen the Map's Menu button opens mid-run, and the
    /// junction between the run's scenes: back to the Map, across to Team, into the dev roster
    /// screen, or out to Home.
    /// Replaces the old Forest Location Hub that used to occupy Game.unity (its tabbed
    /// Team/Map/Shop/Center screen predates the Region Map being the run's actual map; the hub's
    /// controllers are still in Gameplay for the Shop/Center work they'll be reused for).
    ///
    /// Re-run via Pets &gt; Build Ingame Menu Scene (or Pets &gt; Build All Scenes).</summary>
    public static class IngameMenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.IngameMenu + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float MenuButtonWidth = 360f;
        private const float MenuButtonHeight = 64f;
        private const int MenuSpacing = 20;

        [MenuItem("Pets/Build Ingame Menu Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            CreateScreenTitleBar(canvasRect, "Menu", TitleHeight);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();

            var menu = CreatePanel(canvasRect, "MenuButtons", Color.clear,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            const int buttonCount = 4;
            menu.sizeDelta = new Vector2(
                MenuButtonWidth,
                buttonCount * MenuButtonHeight + (buttonCount - 1) * MenuSpacing);
            AddVerticalLayout(menu, new RectOffset(0, 0, 0, 0), MenuSpacing);

            // "Back to Map" rather than "Map": from the player's side this menu is a pause screen
            // laid over the run, and the Map is what they were looking at when they opened it.
            // (Avoid parentheses in labels — Handjet draws them as square brackets.)
            var mapButton = CreateButton(menu, "MapButton", "Back to Map", Theme.ButtonStyle.Confirm, useSprite: true);
            var teamButton = CreateButton(menu, "TeamButton", "Team", Theme.ButtonStyle.Primary, useSprite: true);
            // Labelled as the dev tool it is, and parked below the real destinations rather than
            // among them, so it reads as a workshop door rather than part of the run.
            var devRosterButton = CreateButton(menu, "DevRosterButton", "Dev: Add Pokemon", Theme.ButtonStyle.Secondary, useSprite: true);
            var homeButton = CreateButton(menu, "HomeButton", "Quit to Home", Theme.ButtonStyle.Danger, useSprite: true);

            UnityEventTools.AddVoidPersistentListener(mapButton.onClick, navigator.GoToMap);
            UnityEventTools.AddVoidPersistentListener(teamButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(devRosterButton.onClick, navigator.GoToDevRoster);
            UnityEventTools.AddVoidPersistentListener(homeButton.onClick, navigator.GoHome);

            ForceLayoutRebuild(canvasRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Ingame Menu scene rebuilt at {ScenePath}");
        }
    }
}
