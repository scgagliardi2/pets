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
    /// <summary>Builds the Battle screen: the player's Support and Lead on the left, the foe's Lead
    /// and Support on the right, so the two Leads face each other across the centre column (VS,
    /// Step counter, result); each side's dormant queue underneath; the battle log across the
    /// bottom; and Back / Step / Autoplay / Skip / Battle Again in the bottom bar.
    ///
    /// The builder lays out empty slots only — BattleScreenController fills them with cards at
    /// runtime, because what's in them is rolled when the scene loads. Re-run via
    /// Pets &gt; Build Battle Scene (or Pets &gt; Build All Scenes) after changing the controller's
    /// serialized fields, or its card budget alongside the slot sizes here.</summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Battle + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const float TitleHeight = 60f;
        private const float BottomBarHeight = 84f;
        private const float SideMargin = 40f;

        // Board layout, in units from the board's top-left corner. The board is the canvas less
        // the title bar, bottom bar and side margins: 1200 x 576 at the reference resolution.
        private const float SideHeaderHeight = 32f;
        private const float CardsTop = 40f;
        private const float ColumnGap = 20f;
        /// <summary>BattleScreenController's card budget is written against these two sizes.</summary>
        private static readonly Vector2 LeadCardSize = new Vector2(240f, 260f);
        private static readonly Vector2 SupportCardSize = new Vector2(200f, 230f);
        private const float ReserveLabelGap = 8f;
        private const float ReserveLabelHeight = 22f;
        private const float ReserveRowHeight = 48f;
        private const float LogTopGap = 12f;
        private const float BottomButtonWidth = 180f;

        private static float BoardWidth => ReferenceResolution.x - 2f * SideMargin;
        private static float SideWidth => SupportCardSize.x + ColumnGap + LeadCardSize.x;
        private static float CenterWidth => BoardWidth - 2f * SideWidth - 2f * ColumnGap;
        private static float CenterX => SideWidth + ColumnGap;
        private static float EnemySideX => CenterX + CenterWidth + ColumnGap;
        private static float CardsBottom => CardsTop + LeadCardSize.y;

        [MenuItem("Pets/Build Battle Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Battle", TitleHeight);

            var board = CreatePanel(canvasRect, "Board", Color.clear, Vector2.zero, Vector2.one);
            board.offsetMin = new Vector2(SideMargin, BottomBarHeight);
            board.offsetMax = new Vector2(-SideMargin, -TitleHeight);

            CreateLabel(board, "PlayerHeaderText", "Your Team", 0f, 0f, SideWidth, SideHeaderHeight,
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);
            CreateLabel(board, "EnemyHeaderText", "Foe", EnemySideX, 0f, SideWidth, SideHeaderHeight,
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextDark);

            // Supports sit on the outside and share the Leads' bottom edge, so the pair reads as
            // one formation with the Lead stepped forward.
            float supportTop = CardsBottom - SupportCardSize.y;
            var playerSupportSlot = CreateSlot(board, "PlayerSupport", 0f, supportTop, SupportCardSize);
            var playerLeadSlot = CreateSlot(board, "PlayerLead", SupportCardSize.x + ColumnGap, CardsTop, LeadCardSize);
            var enemyLeadSlot = CreateSlot(board, "EnemyLead", EnemySideX, CardsTop, LeadCardSize);
            var enemySupportSlot = CreateSlot(board, "EnemySupport", EnemySideX + LeadCardSize.x + ColumnGap, supportTop, SupportCardSize);

            CreateLabel(board, "VersusText", "VS", CenterX, CardsTop + 60f, CenterWidth, 60f,
                40, TextAnchor.MiddleCenter, Theme.TextDark);
            var stepText = CreateLabel(board, "StepText", "Step 0", CenterX, CardsTop + 125f, CenterWidth, 30f,
                Theme.FontSizeHeading, TextAnchor.MiddleCenter, Theme.TextMuted);
            var resultText = CreateLabel(board, "ResultText", string.Empty, CenterX, CardsTop + 165f, CenterWidth, 50f,
                36, TextAnchor.MiddleCenter, Theme.TextDark);

            float reserveLabelTop = CardsBottom + ReserveLabelGap;
            float reserveRowTop = reserveLabelTop + ReserveLabelHeight;
            CreateLabel(board, "PlayerReserveLabel", "Next up", 0f, reserveLabelTop, SideWidth, ReserveLabelHeight,
                Theme.FontSizeBody, TextAnchor.MiddleLeft, Theme.TextMuted);
            CreateLabel(board, "EnemyReserveLabel", "Next up", EnemySideX, reserveLabelTop, SideWidth, ReserveLabelHeight,
                Theme.FontSizeBody, TextAnchor.MiddleRight, Theme.TextMuted);
            // Next-up first, nearest its side's Support: leftmost for the player, rightmost for the foe.
            var playerReserveRow = CreateReserveRow(board, "PlayerReserve", 0f, reserveRowTop, TextAnchor.MiddleLeft, reverse: false);
            var enemyReserveRow = CreateReserveRow(board, "EnemyReserve", EnemySideX, reserveRowTop, TextAnchor.MiddleRight, reverse: true);

            var logText = CreateLog(board, reserveRowTop + ReserveRowHeight + LogTopGap);

            var bottomBar = CreatePanel(canvasRect, "BottomBar", Color.clear, Vector2.zero, new Vector2(1f, 0f));
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.offsetMin = Vector2.zero;
            bottomBar.offsetMax = new Vector2(0f, BottomBarHeight);
            AddHorizontalLayout(bottomBar, expandHeight: false,
                padding: new RectOffset((int)SideMargin, (int)SideMargin, 10, 10), controlWidth: true);

            var backButton = CreateBarButton(bottomBar, "BackButton", "Back to Team", Theme.ButtonStyle.Secondary);
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(bottomBar, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var stepButton = CreateBarButton(bottomBar, "StepButton", "Step", Theme.ButtonStyle.Primary);
            var autoplayButton = CreateBarButton(bottomBar, "AutoplayButton", "Autoplay", Theme.ButtonStyle.Primary);
            var skipButton = CreateBarButton(bottomBar, "SkipButton", "Skip", Theme.ButtonStyle.Secondary);
            var battleAgainButton = CreateBarButton(bottomBar, "BattleAgainButton", "Battle Again", Theme.ButtonStyle.Confirm);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();

            var controller = new GameObject("BattleScreen").AddComponent<BattleScreenController>();
            SetField(controller, "board", board.gameObject);
            SetField(controller, "playerLeadSlot", playerLeadSlot);
            SetField(controller, "playerSupportSlot", playerSupportSlot);
            SetField(controller, "enemyLeadSlot", enemyLeadSlot);
            SetField(controller, "enemySupportSlot", enemySupportSlot);
            SetField(controller, "playerReserveRow", playerReserveRow);
            SetField(controller, "enemyReserveRow", enemyReserveRow);
            SetField(controller, "stepText", stepText);
            SetField(controller, "resultText", resultText);
            SetField(controller, "logText", logText);
            SetField(controller, "stepButton", stepButton);
            SetField(controller, "autoplayButton", autoplayButton);
            SetField(controller, "skipButton", skipButton);
            SetField(controller, "battleAgainButton", battleAgainButton);
            SetField(controller, "typeIconPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath));
            SetField(controller, "healthBarPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.HealthBarPrefabPath));

            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(battleAgainButton.onClick, navigator.GoToBattle);
            UnityEventTools.AddVoidPersistentListener(stepButton.onClick, controller.OnStepClicked);
            UnityEventTools.AddVoidPersistentListener(autoplayButton.onClick, controller.OnAutoplayClicked);
            UnityEventTools.AddVoidPersistentListener(skipButton.onClick, controller.OnSkipClicked);

            // Kept live: the reserve rows are filled at runtime, and the bottom bar swaps
            // Step/Autoplay/Skip for Battle Again when the fight ends, which has to reflow.
            ForceLayoutRebuild(canvasRect, bottomBar, playerReserveRow, enemyReserveRow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Battle scene rebuilt at {ScenePath}");
        }

        /// <summary>Pins a rect by its top-left corner, in units from the board's top-left.</summary>
        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateSlot(RectTransform board, string name, float x, float y, Vector2 size)
        {
            var slot = CreatePanel(board, name, Color.clear, Vector2.zero, Vector2.one);
            PlaceTopLeft(slot, x, y, size.x, size.y);
            return slot;
        }

        private static Text CreateLabel(RectTransform board, string name, string content, float x, float y,
            float width, float height, int fontSize, TextAnchor anchor, Color color)
        {
            var text = CreatePlainText(board, name, content, fontSize, anchor, color);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            PlaceTopLeft(text.rectTransform, x, y, width, height);
            return text;
        }

        private static RectTransform CreateReserveRow(RectTransform board, string name, float x, float y,
            TextAnchor alignment, bool reverse)
        {
            var row = CreatePanel(board, name, Color.clear, Vector2.zero, Vector2.one);
            PlaceTopLeft(row, x, y, SideWidth, ReserveRowHeight);
            AddHorizontalLayout(row, expandHeight: false, controlWidth: true);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = 6f;
            layout.reverseArrangement = reverse;
            return row;
        }

        /// <summary>The log panel runs from <paramref name="top"/> to the board's bottom edge,
        /// stretched rather than fixed-height so a shorter aspect ratio squeezes the log instead of
        /// pushing it under the bottom bar.</summary>
        private static Text CreateLog(RectTransform board, float top)
        {
            var panel = new GameObject("LogPanel", typeof(RectTransform));
            panel.transform.SetParent(board, false);
            var image = panel.AddComponent<Image>();
            image.sprite = Theme.TextBoxSprite;
            image.type = Image.Type.Sliced;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -top);

            var text = CreatePlainText(panel.transform, "LogText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, Theme.TextDark);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 12f);
            textRect.offsetMax = new Vector2(-18f, -12f);
            return text;
        }

        private static Button CreateBarButton(RectTransform bar, string name, string label, Theme.ButtonStyle style)
        {
            var button = CreateButton(bar, name, label, style, useSprite: true);
            var layout = button.GetComponent<LayoutElement>();
            layout.flexibleWidth = 0f;
            layout.preferredWidth = BottomButtonWidth;
            return button;
        }
    }
}
