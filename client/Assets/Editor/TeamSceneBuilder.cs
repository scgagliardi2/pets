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
    /// six slots, the Box's first six as a second row below it, and the Lead/Support swap button.
    /// Each filled slot shows the mon's sprite, types and stats, built by the same
    /// Pets.UI.PokemonCardBuilder that draws Character Select's roster cards. Mons are rearranged
    /// by dragging a card onto another slot in either row, and let go for good by dragging one
    /// onto the release zone in the bottom bar — see TeamPanelController for the gesture,
    /// RunState.MoveMon/ReleaseMon for what each drop means, and CreateReleaseConfirm below for
    /// the confirmation a release has to pass. Paging a Box past six slots is still PLAN.md
    /// Phase 1 work.
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
        private const float ReleaseZoneHeight = 64f;
        private static readonly Vector2 DialogSize = new Vector2(560f, 260f);
        private const float DialogButtonWidth = 210f;
        // Three buttons plus their 24-unit edge insets have to fit the 560-wide dialog:
        // 2*24 + 3*width <= 560 caps this at ~170.
        private const float CombineDialogButtonWidth = 160f;
        private const float DialogButtonHeight = 64f;

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
            // Dev entry into the Battle screen until map nodes start fights (PLAN.md §11 item 2).
            // Left clickable with no run: the Battle screen's own no-party redirect is the answer.
            var devBattleButton = CreateButton(bottomBar, "DevBattleButton", "Dev: Random Battle", Theme.ButtonStyle.Confirm, useSprite: true);
            CreateReleaseZone(bottomBar, teamPanel);

            // Last child of the canvas, so a card being dragged draws over both rows and the
            // bottom bar. No Image of its own: it must never take the raycast away from the slot
            // the player is dropping onto.
            var dragLayer = CreatePanel(canvasRect, "DragLayer", Color.clear, Vector2.zero, Vector2.one);
            SetField(teamPanel, "dragLayer", dragLayer);

            var (releaseConfirmPanel, releaseConfirmText, releaseConfirmButton, releaseCancelButton) =
                CreateReleaseConfirm(canvasRect);
            var (combineConfirmPanel, combineConfirmText, combineConfirmButton, combineSwapButton, combineCancelButton) =
                CreateCombineConfirm(canvasRect);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();

            var screen = new GameObject("TeamScreen").AddComponent<TeamScreenController>();
            SetField(screen, "teamPanel", teamPanel);
            SetField(screen, "swapButton", swapButton);
            SetField(screen, "emptyStateText", emptyStateText);
            SetField(screen, "releaseConfirmPanel", releaseConfirmPanel.gameObject);
            SetField(screen, "releaseConfirmText", releaseConfirmText);
            SetField(screen, "releaseConfirmButton", releaseConfirmButton);
            SetField(screen, "combineConfirmPanel", combineConfirmPanel.gameObject);
            SetField(screen, "combineConfirmText", combineConfirmText);
            SetField(screen, "combineConfirmButton", combineConfirmButton);

            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoToIngameMenu);
            UnityEventTools.AddVoidPersistentListener(swapButton.onClick, screen.OnSwapLeadAndSupportClicked);
            UnityEventTools.AddVoidPersistentListener(devBattleButton.onClick, navigator.GoToBattle);
            UnityEventTools.AddVoidPersistentListener(releaseConfirmButton.onClick, screen.OnConfirmReleaseClicked);
            UnityEventTools.AddVoidPersistentListener(releaseCancelButton.onClick, screen.OnCancelReleaseClicked);
            UnityEventTools.AddVoidPersistentListener(combineConfirmButton.onClick, screen.OnConfirmCombineClicked);
            UnityEventTools.AddVoidPersistentListener(combineSwapButton.onClick, screen.OnSwapInsteadOfCombineClicked);
            UnityEventTools.AddVoidPersistentListener(combineCancelButton.onClick, screen.OnCancelCombineClicked);

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

        /// <summary>The drop target that lets a mon go: red 9-sliced art with a label saying what
        /// to do with it, sitting in the bottom bar beside the other two controls. It carries its
        /// own Image, which is what makes it a raycast target and therefore a legal place to drop
        /// a dragged card.</summary>
        private static void CreateReleaseZone(RectTransform bottomBar, TeamPanelController panel)
        {
            var zone = new GameObject("ReleaseZone", typeof(RectTransform));
            zone.transform.SetParent(bottomBar, false);
            // Explicit height: the bottom bar's HorizontalLayoutGroup controls width but not
            // height (see AddHorizontalLayout's expandHeight), so a fresh RectTransform would keep
            // its 100-unit default and hang out of the 84-unit bar. The sprite buttons beside it
            // get their 64 from the prefab instead.
            zone.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, ReleaseZoneHeight);

            var image = zone.AddComponent<Image>();
            image.sprite = Theme.ButtonRedSprite;
            image.type = Image.Type.Sliced;

            var layoutElement = zone.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = ReleaseZoneHeight;
            layoutElement.flexibleWidth = 1f;

            var label = CreatePlainText(zone.transform, "Label", "Drag here to Release",
                Theme.FontSizeBody, TextAnchor.MiddleCenter, Theme.TextLight);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);

            SetField(zone.AddComponent<ReleaseZoneView>(), "panel", panel);
        }

        /// <summary>The release confirmation: a dimmed full-screen backdrop with a small dialog
        /// centred on it. Releasing can't be undone, so it's the one thing on this screen that
        /// asks before acting.
        ///
        /// The backdrop keeps its Image — it has to take the raycast, so the rows underneath can't
        /// be dragged while the question is open — and everything inside is anchored by hand
        /// rather than laid out by a group, since the message text changes at runtime and there'd
        /// be no surviving LayoutGroup to reflow it (see ForceLayoutRebuild).</summary>
        private static (RectTransform panel, Text message, Button confirm, Button cancel) CreateReleaseConfirm(RectTransform canvasRect)
        {
            var backdrop = CreatePanel(canvasRect, "ReleaseConfirm", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one);

            var dialog = new GameObject("Dialog", typeof(RectTransform));
            dialog.transform.SetParent(backdrop, false);
            var dialogImage = dialog.AddComponent<Image>();
            dialogImage.sprite = Theme.TextBoxSprite;
            dialogImage.type = Image.Type.Sliced;
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = DialogSize;

            var message = CreatePlainText(dialog.transform, "MessageText", string.Empty,
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);
            message.fontStyle = FontStyle.Bold;
            var messageRect = message.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(24f, DialogButtonHeight + 36f);
            messageRect.offsetMax = new Vector2(-24f, -24f);

            var confirm = CreateDialogButton(dialogRect, "ReleaseConfirmButton", "Release",
                Theme.ButtonStyle.Danger, anchorX: 1f);
            var cancel = CreateDialogButton(dialogRect, "ReleaseCancelButton", "Cancel",
                Theme.ButtonStyle.Secondary, anchorX: 0f);

            return (backdrop, message, confirm, cancel);
        }

        /// <summary>The duplicate question: dropping a mon onto another of the same species could
        /// mean either thing, so the dialog offers both rather than the screen picking one.
        /// Combining consumes a mon for good, which is why it gets the same modal treatment as a
        /// release — and why Combine is the Danger-styled button of the three.
        ///
        /// Three buttons across a dialog this size need narrower ones than the two-button release
        /// dialog uses, hence the explicit width.</summary>
        private static (RectTransform panel, Text message, Button combine, Button swap, Button cancel)
            CreateCombineConfirm(RectTransform canvasRect)
        {
            var backdrop = CreatePanel(canvasRect, "CombineConfirm", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one);

            var dialog = new GameObject("Dialog", typeof(RectTransform));
            dialog.transform.SetParent(backdrop, false);
            var dialogImage = dialog.AddComponent<Image>();
            dialogImage.sprite = Theme.TextBoxSprite;
            dialogImage.type = Image.Type.Sliced;
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = DialogSize;

            var message = CreatePlainText(dialog.transform, "MessageText", string.Empty,
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);
            message.fontStyle = FontStyle.Bold;
            var messageRect = message.GetComponent<RectTransform>();
            messageRect.anchorMin = Vector2.zero;
            messageRect.anchorMax = Vector2.one;
            messageRect.offsetMin = new Vector2(24f, DialogButtonHeight + 36f);
            messageRect.offsetMax = new Vector2(-24f, -24f);

            var combine = CreateDialogButton(dialogRect, "CombineConfirmButton", "Combine",
                Theme.ButtonStyle.Danger, anchorX: 1f, buttonWidth: CombineDialogButtonWidth);
            var swap = CreateDialogButton(dialogRect, "CombineSwapButton", "Swap",
                Theme.ButtonStyle.Primary, anchorX: 0.5f, buttonWidth: CombineDialogButtonWidth);
            var cancel = CreateDialogButton(dialogRect, "CombineCancelButton", "Cancel",
                Theme.ButtonStyle.Secondary, anchorX: 0f, buttonWidth: CombineDialogButtonWidth);

            return (backdrop, message, combine, swap, cancel);
        }

        /// <summary>A button along the bottom of a dialog, anchored at <paramref name="anchorX"/>
        /// across it (0 left, 0.5 centre, 1 right) and inset from that edge. An explicit anchor
        /// rather than a layout group for the same reason the message text is hand-anchored: the
        /// dialog's contents change at runtime and the bake pass leaves no LayoutGroup behind to
        /// reflow them.</summary>
        private static Button CreateDialogButton(RectTransform dialog, string name, string label,
            Theme.ButtonStyle style, float anchorX, float buttonWidth = DialogButtonWidth)
        {
            var button = CreateButton(dialog, name, label, style, useSprite: true);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0f);
            rect.pivot = new Vector2(anchorX, 0f);
            rect.sizeDelta = new Vector2(buttonWidth, DialogButtonHeight);
            // Only the edge-anchored buttons need the inset; a centred one is already clear of both.
            float inset = Mathf.Approximately(anchorX, 1f) ? -24f : Mathf.Approximately(anchorX, 0f) ? 24f : 0f;
            rect.anchoredPosition = new Vector2(inset, 24f);
            return button;
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
