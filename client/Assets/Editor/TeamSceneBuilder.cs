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
    /// <summary>Builds the Team screen, reached from the Ingame Menu: the run's party as a row of
    /// six slots, the Box's first six as a second row below it, and the one line-up edit that's
    /// unambiguous with two active slots — swapping which mon leads. Each filled slot shows the
    /// mon's sprite, types and stats, built by the same Pets.UI.PokemonCardBuilder that draws
    /// Character Select's roster cards. Fuller team management (promoting out of the Box,
    /// reordering the reserves, paging a Box past six) waits on the Box rules in PLAN.md Phase 1.
    ///
    /// Six slots is a view of the line-up "train" (design doc §7), not six active mons —
    /// TeamPanelController labels slot 0 Lead, slot 1 Support and dims the rest as dormant.
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

        private const float SectionHeaderHeight = 26f;
        private const float SectionGap = 10f;
        /// <summary>Slot cell height — TeamPanelController's card budget is written against
        /// exactly this number, so the two move together.</summary>
        private const float SlotHeight = 186f;
        private const float SlotSpacing = 12f;

        /// <summary>Both sections stacked: header, row, gap, header, row.</summary>
        private static float SectionsHeight =>
            2f * (SectionHeaderHeight + SlotHeight) + SectionGap;

        /// <summary>Slot width that divides the content area into exactly
        /// TeamPanelController.SlotsPerRow columns, derived rather than hand-typed for the same
        /// reason CharacterSelectSceneBuilder derives its CellWidth: changing a margin can't then
        /// silently push the sixth slot off-screen.</summary>
        private static float SlotWidth =>
            (ReferenceResolution.x - 2f * SideMargin - (TeamPanelController.SlotsPerRow - 1) * SlotSpacing)
            / TeamPanelController.SlotsPerRow;

        [MenuItem("Pets/Build Team Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            CreateScreenTitleBar(canvasRect, "Team", TitleHeight);

            // Stretched to the canvas then inset in pixels, so the two slot rows keep their exact
            // width (and therefore their exact six columns) while whatever vertical room a given
            // aspect ratio leaves between the fixed-height bars is absorbed here.
            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, Vector2.one);
            content.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            content.offsetMax = new Vector2(-SideMargin, -TitleHeight);

            // An exactly-sized band centered in the content area, so the two rows sit in the
            // middle of whatever room the chrome leaves instead of hugging the title bar with the
            // leftovers dumped above the bottom bar.
            var sections = CreatePanel(content, "Sections", Color.clear, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            sections.sizeDelta = new Vector2(0f, SectionsHeight);

            // Both sections are pinned to the top of that band in pixels rather than laid out by a
            // vertical group: the rows themselves are grids a controller repopulates at runtime,
            // and a surviving parent layout group would fight the pixel budget the slot cards are
            // sized against.
            float partyTop = 0f;
            var partyHeader = CreateSectionHeader(sections, "PartyHeaderText", "Party", partyTop);
            var partySlots = CreateSlotRow(sections, "PartySlots", partyTop + SectionHeaderHeight);

            float boxTop = partyTop + SectionHeaderHeight + SlotHeight + SectionGap;
            var boxHeader = CreateSectionHeader(sections, "BoxHeaderText", "Box", boxTop);
            var boxSlots = CreateSlotRow(sections, "BoxSlots", boxTop + SectionHeaderHeight);

            var teamPanelGO = new GameObject("TeamPanel", typeof(RectTransform));
            teamPanelGO.transform.SetParent(content, false);
            var teamPanelRect = teamPanelGO.GetComponent<RectTransform>();
            teamPanelRect.anchorMin = Vector2.zero;
            teamPanelRect.anchorMax = Vector2.one;
            teamPanelRect.offsetMin = Vector2.zero;
            teamPanelRect.offsetMax = Vector2.zero;
            // The sections band is built on Content and then moved under TeamPanel, which the
            // screen toggles off for the no-run empty state — see ReparentUnder below.
            ReparentUnder(teamPanelRect, sections);

            var teamPanel = teamPanelGO.AddComponent<TeamPanelController>();
            SetField(teamPanel, "partySlots", partySlots);
            SetField(teamPanel, "boxSlots", boxSlots);
            SetField(teamPanel, "partyHeaderText", partyHeader);
            SetField(teamPanel, "boxHeaderText", boxHeader);
            SetField(teamPanel, "typeIconPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath));

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

            // Both slot rows stay live: TeamPanelController fills them with slot cards at runtime
            // from whatever the run's party and Box hold, so their GridLayoutGroups have to be
            // around to arrange children that don't exist yet at build time.
            ForceLayoutRebuild(canvasRect, partySlots, boxSlots);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Team scene rebuilt at {ScenePath}");
        }

        /// <summary>A section label ("Party", "Box") pinned <paramref name="offsetFromTop"/> units
        /// below the top of the content area. TeamPanelController rewrites the text with the
        /// section's own count once a run is loaded.</summary>
        private static Text CreateSectionHeader(RectTransform parent, string name, string label, float offsetFromTop)
        {
            var text = CreatePlainText(parent, name, label, Theme.FontSizeHeading, TextAnchor.MiddleLeft, Theme.TextDark);
            text.fontStyle = FontStyle.Bold;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(offsetFromTop + SectionHeaderHeight));
            rect.offsetMax = new Vector2(0f, -offsetFromTop);
            return text;
        }

        /// <summary>A row of exactly TeamPanelController.SlotsPerRow fixed-size cells, pinned
        /// below its section header. Fixed column count (rather than a flexible grid) for the same
        /// reason Character Select's grid pins its own: it makes six-across the invariant and the
        /// cell size the consequence.</summary>
        private static RectTransform CreateSlotRow(RectTransform parent, string name, float offsetFromTop)
        {
            var row = CreatePanel(parent, name, Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f));
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(0f, -(offsetFromTop + SlotHeight));
            row.offsetMax = new Vector2(0f, -offsetFromTop);

            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = TeamPanelController.SlotsPerRow;
            grid.cellSize = new Vector2(SlotWidth, SlotHeight);
            grid.spacing = new Vector2(SlotSpacing, SlotSpacing);
            grid.childAlignment = TextAnchor.UpperLeft;
            return row;
        }

        /// <summary>Moves already-built rects under a new parent, keeping their anchoring. They're
        /// created directly on Content first so their pixel offsets are written against the same
        /// rect either way, and TeamPanel is stretched to Content so reparenting doesn't move
        /// anything.</summary>
        private static void ReparentUnder(RectTransform parent, params Transform[] children)
        {
            foreach (var child in children)
            {
                child.SetParent(parent, false);
            }
        }
    }
}
