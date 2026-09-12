using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
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
        /// <summary>Matches every other scene builder. Leaving this to CreateCanvas's own default
        /// (960x720) is what made the Map's chrome ~33% bigger than the rest of the game and
        /// visibly jump size when navigating to the Ingame Menu or Team.</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        public const string ScenePath = "Assets/Scenes/" + SceneNames.Map + ".unity";
        private static readonly Color PlaceholderBackground = new Color(0.29f, 0.42f, 0.29f);

        // Chrome pinned in pixels rather than as a fraction of canvas height, so this scene's
        // buttons are the same 9-sliced prefab art at the same size as every other screen's.
        // Fractional bands were what previously ruled the prefab out here: the title bar came out
        // 57 units tall at 16:9 and less on a taller phone, where the art needs ~64 plus padding.
        private const float TitleHeight = 76f;
        private const float FooterHeight = 84f;
        private const float SideMargin = 20f;
        private const float ButtonHeight = 64f;
        private const float MenuButtonWidth = 150f;
        private const float NewMapButtonWidth = 200f;

        [MenuItem("Pets/Build Region Map Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            var titleBar = CreatePanel(canvasRect, "TitleBar", Theme.ChromeBg, new Vector2(0f, 1f), Vector2.one);
            PinToTop(titleBar, TitleHeight);
            var titleText = CreatePlainText(canvasRect, "TitleText", "Region Map", Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextLight);
            titleText.fontStyle = FontStyle.Bold;
            // The title label stretches the whole bar, including over the Menu button in its left
            // corner — as a raycast target it would quietly swallow that button's clicks.
            titleText.raycastTarget = false;
            PinToTop(titleText.GetComponent<RectTransform>(), TitleHeight);

            // Footer: the walk's status line plus a re-roll, so a map can be judged across many
            // generations without leaving Play mode.
            var footerBar = CreatePanel(canvasRect, "FooterBar", Theme.ChromeBg, Vector2.zero, new Vector2(1f, 0f));
            PinToBottom(footerBar, FooterHeight);
            var statusText = CreatePlainText(footerBar, "StatusText", "", Theme.FontSizeBody, TextAnchor.MiddleLeft, Theme.TextLight);
            statusText.raycastTarget = false;
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.offsetMin = new Vector2(SideMargin, 0f);
            statusRect.offsetMax = new Vector2(-(SideMargin + NewMapButtonWidth + 16f), 0f);

            var newMapButton = CreateButton(footerBar, "NewMapButton", "New Map", Theme.ButtonStyle.Confirm, useSprite: true);
            AnchorInBar(newMapButton, NewMapButtonWidth, toRight: true);

            // The way out of the run: the Map is the screen a player spends a run on, so it owns
            // the entry point to the Ingame Menu (Team, Home) rather than the menu being something
            // they can only reach between Locations. Parked in the title bar's left corner, clear
            // of the footer's map controls so "leave the run" can't be mistaken for "re-roll it".
            var menuButton = CreateButton(titleBar, "MenuButton", "Menu", Theme.ButtonStyle.Secondary, useSprite: true);
            AnchorInBar(menuButton, MenuButtonWidth, toRight: false);

            // Vertical scrolling as well as horizontal: the taller chrome these sprite buttons
            // need leaves the map area 560 units high, and the widest map the generator can
            // produce (4 branches in a layer — RegionMapGenerator.MaxNodesPerLayer) lays out 572
            // units tall, so the top and bottom node of such a column would otherwise be clipped
            // by a few units with no way to reach them. With ScrollRect.movementType Clamped a map
            // that does fit can't be dragged off-centre, so this costs nothing in the common case.
            var (scrollRect, _, content) = CreateScrollView(canvasRect, "MapScroll", Vector2.zero, Vector2.one, horizontal: true, vertical: true);
            // Stretched to the canvas and then inset in pixels, so the map area absorbs whatever
            // vertical room the two fixed-height bars leave on a given aspect ratio instead of
            // scaling with it.
            var mapRect = (RectTransform)scrollRect.transform;
            mapRect.offsetMin = new Vector2(SideMargin, FooterHeight);
            mapRect.offsetMax = new Vector2(-SideMargin, -TitleHeight);
            scrollRect.scrollSensitivity = 25f;

            // Left-anchored, vertically centered content: layer 0 sits at the left edge and the
            // Gym at the right, so the map reads left to right and each layer's branches stack
            // into a column around the middle. At RegionMapController's spacings a default
            // seven-layer map fits the viewport end to end; the scroll axis is there for a longer
            // map or a narrower window, and it follows the player when it's needed.
            // (RegionMapController recomputes this size per generated map — see LayOutNodes.)
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(860, 560);

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

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            UnityEventTools.AddVoidPersistentListener(menuButton.onClick, navigator.GoToIngameMenu);

            // The Map is where a run starts once Character Select hands off, so this scene owns
            // the bootstrapper that turns the chosen pair into a RunState and publishes it to
            // ActiveRun for the Team screen to read. Re-entering the Map from the Ingame Menu
            // resumes that same run rather than rebuilding it — see RunBootstrapper.Awake.
            CreateBootstrapper();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Region Map scene rebuilt at {ScenePath}");
        }

        /// <summary>Starting Lead/Support for a run begun without going through Character Select
        /// (opening this scene directly in the Editor). A real hand-off overrides these via
        /// PendingRunSelection — see RunBootstrapper.</summary>
        private static void CreateBootstrapper()
        {
            var bootstrapper = new GameObject("RunBootstrapper").AddComponent<RunBootstrapper>();
            SetField(bootstrapper, "speciesLibrary",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset"));
            SetField(bootstrapper, "starterLead",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/charmander.asset"));
            SetField(bootstrapper, "starterSupport",
                AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/squirtle.asset"));
        }

        /// <summary>Centers a fixed-size sprite button vertically in the bar it belongs to, a
        /// SideMargin in from the bar's left or right edge. Anchored rather than laid out by a
        /// group: each bar holds one button, so there's nothing to arrange, and it keeps the map's
        /// chrome clear of ForceLayoutRebuild's bake-and-destroy behaviour — which is also why
        /// this scene doesn't call it at all.</summary>
        private static void AnchorInBar(Button button, float width, bool toRight)
        {
            var rect = button.GetComponent<RectTransform>();
            float x = toRight ? 1f : 0f;
            rect.anchorMin = new Vector2(x, 0.5f);
            rect.anchorMax = new Vector2(x, 0.5f);
            rect.pivot = new Vector2(x, 0.5f);
            rect.sizeDelta = new Vector2(width, ButtonHeight);
            rect.anchoredPosition = new Vector2(toRight ? -SideMargin : SideMargin, 0f);
        }

        private static void PinToTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        private static void PinToBottom(RectTransform rect, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, height);
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
