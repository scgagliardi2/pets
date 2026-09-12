using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pets.Gameplay;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Credits screen, reached from Home. Carries the attribution this project
    /// actually owes — the Pokémon trademarks it borrows, PokeAPI as the data/sprite source, the
    /// OFL-licensed font — plus the non-commercial scope note, which is a hard constraint on the
    /// project rather than a footnote (PLAN.md §9, CLAUDE.md). Worth stating on screen and not
    /// only in the repo, since the screen is the part anyone playing it actually sees.
    ///
    /// Re-run via Pets &gt; Build Credits Scene (or Pets &gt; Build All Scenes).</summary>
    public static class CreditsSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Credits + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 76f;
        private const float LineHeight = 44f;
        private const float LineSpacing = 10f;
        private const float PanelPadding = 20f;
        private const float PanelHeaderHeight = 36f;

        /// <summary>Body lines, in order, each short enough to fit the panel's width in one or
        /// two lines so LineHeight can be one number for all of them. Avoid parentheses — Handjet
        /// draws them as square brackets.</summary>
        private static readonly string[] Lines =
        {
            "A personal, non-commercial fan project. Not for sale, not monetized, not for wide distribution.",
            "Pokémon and all related names and types are trademarks of Nintendo, Creatures Inc. and GAME FREAK Inc.",
            "This project is unaffiliated with them and unendorsed by them.",
            "Species data, evolution chains and sprites: PokeAPI — pokeapi.co",
            "Font: Handjet, used under the SIL Open Font License 1.1 — see Assets/Resources/Fonts/Handjet/OFL.txt",
            "UI palette and component shapes: the Monster Trails style guide.",
            "Design and code: Sam. Built with Unity."
        };

        [MenuItem("Pets/Build Credits Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Credits", TitleHeight);

            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, Vector2.one);
            content.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            content.offsetMax = new Vector2(-SideMargin, -TitleHeight);

            // Sized to its contents and pinned to the top of the content area, rather than
            // stretched: a full-height panel with six lines in it reads as a mostly-empty box.
            var panel = CreatePanel(content, "CreditsPanel", Theme.PanelBg, new Vector2(0f, 1f), new Vector2(1f, 1f));
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(0f, PanelHeaderHeight + 2f * PanelPadding
                + Lines.Length * LineHeight + (Lines.Length - 1) * LineSpacing);
            AddVerticalLayout(panel, new RectOffset(24, 24, (int)PanelPadding, (int)PanelPadding), (int)LineSpacing);
            AddPanelHeader(panel, "About this project");
            for (int i = 0; i < Lines.Length; i++)
            {
                // Theme.FontSizeHeading rather than FontSizeBody: this is a screen of nothing but
                // prose, and Handjet's thin strokes at body size came out as grey mush against the
                // cream panel in the first capture of it.
                CreateText(panel, $"Line{i}", Lines[i], Theme.FontSizeHeading, TextAnchor.UpperLeft, LineHeight);
            }

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var backButton = CreateBottomBarButton(canvasRect, "BackButton", "Back to Home",
                Theme.ButtonStyle.Secondary, BottomBarHeight, SideMargin);
            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoHome);

            ForceLayoutRebuild(canvasRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Credits scene rebuilt at {ScenePath}");
        }
    }
}
