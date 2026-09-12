using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Region Map scene (design doc §5): a randomly generated branching
    /// node-map the player walks from a start node, through five layers of choices, to the
    /// mandatory Gym, over a placeholder solid-color background (real background art is a later
    /// pass). Each node shows its own icon — Battle/Encounter/Mystery Trainer/Pokémon Center/Gym,
    /// loaded from Assets/Resources/Sprites/Nodes (see that folder's README) — falling back to a
    /// flat color swatch for any type whose icon file isn't there yet.
    ///
    /// Nodes are walkable but not yet resolvable — arriving at one doesn't start a fight or an
    /// event (PLAN.md Phase 1), and this scene isn't wired into the Forest run loop. Re-run via
    /// Pets &gt; Build Region Map Scene after changing RegionMapController's serialized fields.</summary>
    public static class RegionMapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/RegionMap.unity";
        private static readonly Color PlaceholderBackground = new Color(0.29f, 0.42f, 0.29f);

        [MenuItem("Pets/Build Region Map Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas();

            CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, new Vector2(0, 0.92f), Vector2.one);
            var titleText = CreatePlainText(canvasRect, "TitleText", "Region Map", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            titleText.fontStyle = FontStyle.Bold;
            StretchTo(titleText.GetComponent<RectTransform>(), new Vector2(0, 0.92f), Vector2.one);

            // Footer: the walk's status line plus a re-roll, so a map can be judged across many
            // generations without leaving Play mode.
            CreatePanel(canvasRect, "FooterBar", Theme.ChromeBg, Vector2.zero, new Vector2(1f, 0.09f));
            var statusText = CreatePlainText(canvasRect, "StatusText", "", Theme.FontSizeBody, TextAnchor.MiddleLeft, Theme.TextLight);
            StretchTo(statusText.GetComponent<RectTransform>(), new Vector2(0.03f, 0f), new Vector2(0.74f, 0.09f));

            var newMapButton = CreateButton(canvasRect, "NewMapButton", "New Map", Theme.ButtonStyle.Confirm);
            StretchTo(newMapButton.GetComponent<RectTransform>(), new Vector2(0.76f, 0.015f), new Vector2(0.97f, 0.075f));

            var (scrollRect, _, content) = CreateScrollView(canvasRect, "MapScroll", new Vector2(0.02f, 0.09f), new Vector2(0.98f, 0.92f), horizontal: true, vertical: false);
            scrollRect.scrollSensitivity = 25f;

            // Left-anchored, vertically centered content: layer 0 sits at the left edge and the
            // Gym at the right, so the map reads left to right and each layer's branches stack
            // into a column around the middle. At RegionMapController's spacings a default
            // seven-layer map fits the viewport end to end; the scroll axis is there for a longer
            // map or a narrower window, and it follows the player when it's needed.
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(860, 580);

            var backgroundGO = new GameObject("Background", typeof(RectTransform));
            backgroundGO.transform.SetParent(content, false);
            var background = backgroundGO.AddComponent<Image>();
            background.color = PlaceholderBackground;
            background.raycastTarget = false;
            StretchTo(backgroundGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            var controller = new GameObject("RegionMap").AddComponent<RegionMapController>();
            SetField(controller, "content", content);
            SetField(controller, "scrollRect", scrollRect);
            SetField(controller, "statusText", statusText);
            SetField(controller, "newMapButton", newMapButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"Region Map scene rebuilt at {ScenePath}");
        }

        private static void StretchTo(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
