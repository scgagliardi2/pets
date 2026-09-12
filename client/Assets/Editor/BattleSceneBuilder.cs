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
    /// <summary>Builds the Battle screen after the battle mockup: a full-screen battlefield backdrop;
    /// the foe's Lead and Support on the far clearing with their stat boxes top-left; the player's pair
    /// in the foreground with their boxes on the right; a pause / step / play / skip pill at the top;
    /// and the party strip with the (disabled) Throw button along the bottom. A result panel appears
    /// over it all when the fight ends — two buttons whose labels and destinations the controller
    /// sets from the kind of fight it was, plus a row above it where a won PvE node's catch offers
    /// are added at runtime.
    ///
    /// Positions are written in units from the top-left of the 1280x720 reference canvas, the numbers
    /// you'd read off the mockup. Everything in the lower half — the player's mons and boxes, the
    /// strip — is anchored to the bottom edge instead, so a taller or shorter screen moves the two
    /// halves apart rather than pushing the strip off it.
    ///
    /// The stat boxes and party slots are the BattleStatsBox / BattlePartySlot prefabs; the backdrop
    /// is Assets/Art/Backgrounds/BattleForest.png (tools/generate_battle_background.py). Re-run via
    /// Pets &gt; Build Battle Scene (or Pets &gt; Build All Scenes) after changing either prefab or
    /// BattleScreenController's serialized fields.</summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.Battle + ".unity";
        public const string BackgroundPath = "Assets/Art/Backgrounds/BattleForest.png";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private const int PartySlotCount = 6;

        // The backdrop's clearing and foreground patch are drawn where these sprites stand. The foe
        // pair's feet stay above y 322, where the player's stat boxes start, so neither is hidden.
        private static readonly Rect EnemyLeadSpriteRect = new Rect(750f, 130f, 190f, 190f);
        private static readonly Rect EnemySupportSpriteRect = new Rect(985f, 150f, 150f, 150f);
        private static readonly Rect PlayerLeadSpriteRect = new Rect(470f, 330f, 240f, 240f);
        private static readonly Rect PlayerSupportSpriteRect = new Rect(170f, 380f, 200f, 200f);

        private static readonly Vector2 EnemyLeadStatsPos = new Vector2(56f, 24f);
        private static readonly Vector2 EnemySupportStatsPos = new Vector2(56f, 136f);
        private static readonly Vector2 PlayerLeadStatsPos = new Vector2(924f, 322f);
        private static readonly Vector2 PlayerSupportStatsPos = new Vector2(924f, 434f);

        private const float PillButtonWidth = 60f;
        private const float PillButtonHeight = 48f;
        private const float PillGap = 6f;
        private const float PillPadding = 8f;
        private const float PillTop = 12f;
        private const float IconSize = 36f;

        private const float StripHeight = 172f;
        private const float SlotGap = 12f;
        private const float ThrowGap = 24f;
        private static readonly Vector2 ThrowSize = new Vector2(150f, 150f);

        private static readonly Vector2 ResultPanelSize = new Vector2(460f, 220f);
        private static readonly Vector2 ResultButtonSize = new Vector2(200f, 64f);
        private const float ResultButtonInset = 20f;
        private const float CatchRowHeight = 64f;
        private const float CatchRowGap = 12f;

        [MenuItem("Pets/Build Battle Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreateBackground(canvasRect);

            var board = CreatePanel(canvasRect, "Board", Color.clear, Vector2.zero, Vector2.one);

            // Field sprites first, so every box, control and the strip draw over them.
            var enemySupportSprite = CreateFieldSprite(board, "EnemySupportSprite", EnemySupportSpriteRect, bottom: false, out var enemySupportDamage);
            var enemyLeadSprite = CreateFieldSprite(board, "EnemyLeadSprite", EnemyLeadSpriteRect, bottom: false, out var enemyLeadDamage);
            var playerSupportSprite = CreateFieldSprite(board, "PlayerSupportSprite", PlayerSupportSpriteRect, bottom: true, out var playerSupportDamage);
            var playerLeadSprite = CreateFieldSprite(board, "PlayerLeadSprite", PlayerLeadSpriteRect, bottom: true, out var playerLeadDamage);

            var enemyLeadStats = CreateStatsBox(board, "EnemyLeadStats", EnemyLeadStatsPos, bottom: false);
            var enemySupportStats = CreateStatsBox(board, "EnemySupportStats", EnemySupportStatsPos, bottom: false);
            var playerLeadStats = CreateStatsBox(board, "PlayerLeadStats", PlayerLeadStatsPos, bottom: true);
            var playerSupportStats = CreateStatsBox(board, "PlayerSupportStats", PlayerSupportStatsPos, bottom: true);

            var foeCountText = CreateOverlayText(board, "FoeCountText", "Foe team", Theme.FontSizeHeading, TextAnchor.MiddleLeft);
            PlaceTop(foeCountText.rectTransform, new Rect(EnemySupportStatsPos.x + 4f,
                EnemySupportStatsPos.y + BattleStatsBoxView.Size.y + 6f, BattleStatsBoxView.Size.x, 28f));

            var pill = CreateFrame(board, "PlaybackControls", Theme.SlotDarkSprite);
            float pillWidth = 4f * PillButtonWidth + 3f * PillGap + 2f * PillPadding;
            PlaceTop(pill, new Rect((ReferenceResolution.x - pillWidth) / 2f, PillTop, pillWidth, PillButtonHeight + 2f * PillPadding));
            var pauseButton = CreateIconButton(pill, "PauseButton", Theme.PauseIconSprite, 0);
            var stepButton = CreateIconButton(pill, "StepButton", Theme.StepIconSprite, 1);
            var playButton = CreateIconButton(pill, "PlayButton", Theme.PlayIconSprite, 2);
            var skipButton = CreateIconButton(pill, "SkipButton", Theme.FastForwardIconSprite, 3);

            var stepText = CreateOverlayText(board, "StepText", "Step 0", Theme.FontSizeHeading, TextAnchor.MiddleCenter);
            PlaceTop(stepText.rectTransform, new Rect((ReferenceResolution.x - 200f) / 2f,
                PillTop + PillButtonHeight + 2f * PillPadding + 4f, 200f, 28f));

            var backButton = CreateButton(board, "BackButton", "Back", Theme.ButtonStyle.Secondary, useSprite: true);
            PlaceTop((RectTransform)backButton.transform, new Rect(ReferenceResolution.x - 24f - 150f, 16f, 150f, 64f));

            var strip = CreatePanel(board, "PartyStrip", Theme.ChromeBg, Vector2.zero, new Vector2(1f, 0f));
            strip.pivot = new Vector2(0.5f, 0f);
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = new Vector2(0f, StripHeight);

            var slotSize = BattlePartySlotView.Size;
            float rowWidth = PartySlotCount * slotSize.x + (PartySlotCount - 1) * SlotGap + ThrowGap + ThrowSize.x;
            float rowLeft = (ReferenceResolution.x - rowWidth) / 2f;
            float rowTop = (StripHeight - slotSize.y) / 2f;
            var partySlots = new BattlePartySlotView[PartySlotCount];
            for (int i = 0; i < PartySlotCount; i++)
            {
                partySlots[i] = InstantiatePrefab<BattlePartySlotView>(UiPrefabBuilder.BattlePartySlotPrefabPath, strip, $"PartySlot{i}");
                PlaceTop((RectTransform)partySlots[i].transform,
                    new Rect(rowLeft + i * (slotSize.x + SlotGap), rowTop, slotSize.x, slotSize.y));
            }
            var throwButton = CreateThrowButton(strip,
                new Rect(rowLeft + PartySlotCount * (slotSize.x + SlotGap) - SlotGap + ThrowGap, rowTop, ThrowSize.x, ThrowSize.y));

            var (resultPanel, resultText, battleAgainButton, resultBackButton, resultActionButton, catchRow) = CreateResultPanel(board);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            var controller = new GameObject("BattleScreen").AddComponent<BattleScreenController>();
            SetField(controller, "board", board.gameObject);
            SetField(controller, "playerLeadStats", playerLeadStats);
            SetField(controller, "playerSupportStats", playerSupportStats);
            SetField(controller, "enemyLeadStats", enemyLeadStats);
            SetField(controller, "enemySupportStats", enemySupportStats);
            SetField(controller, "playerLeadSprite", playerLeadSprite);
            SetField(controller, "playerSupportSprite", playerSupportSprite);
            SetField(controller, "enemyLeadSprite", enemyLeadSprite);
            SetField(controller, "enemySupportSprite", enemySupportSprite);
            SetField(controller, "playerLeadDamage", playerLeadDamage);
            SetField(controller, "playerSupportDamage", playerSupportDamage);
            SetField(controller, "enemyLeadDamage", enemyLeadDamage);
            SetField(controller, "enemySupportDamage", enemySupportDamage);
            SetField(controller, "partySlots", partySlots);
            SetField(controller, "stepText", stepText);
            SetField(controller, "foeCountText", foeCountText);
            SetField(controller, "pauseButton", pauseButton);
            SetField(controller, "stepButton", stepButton);
            SetField(controller, "playButton", playButton);
            SetField(controller, "skipButton", skipButton);
            SetField(controller, "throwButton", throwButton);
            SetField(controller, "backButton", backButton);
            SetField(controller, "resultPanel", resultPanel.gameObject);
            SetField(controller, "resultText", resultText);
            SetField(controller, "battleAgainButton", battleAgainButton.GetComponent<UiButton>());
            SetField(controller, "resultBackButton", resultBackButton.GetComponent<UiButton>());
            SetField(controller, "resultActionButton", resultActionButton.GetComponent<UiButton>());
            SetField(controller, "catchButtonsContainer", catchRow);
            SetField(controller, "catchButtonPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.ButtonPrefabPath).GetComponent<UiButton>());

            UnityEventTools.AddVoidPersistentListener(backButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(resultBackButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(battleAgainButton.onClick, navigator.GoToBattle);
            // The node fight's one button is the only one whose destination isn't fixed: it depends
            // on how the fight ended and on the run's Morale, so it asks the controller.
            UnityEventTools.AddVoidPersistentListener(resultActionButton.onClick, controller.OnResultActionClicked);
            UnityEventTools.AddVoidPersistentListener(pauseButton.onClick, controller.OnPauseClicked);
            UnityEventTools.AddVoidPersistentListener(stepButton.onClick, controller.OnStepClicked);
            UnityEventTools.AddVoidPersistentListener(playButton.onClick, controller.OnPlayClicked);
            UnityEventTools.AddVoidPersistentListener(skipButton.onClick, controller.OnSkipClicked);

            // The catch row is filled at runtime, so its layout group has to survive the bake.
            ForceLayoutRebuild(canvasRect, catchRow);
            resultPanel.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Battle scene rebuilt at {ScenePath}");
        }

        /// <summary>The backdrop, enveloping the canvas at 16:9 so another aspect ratio crops it
        /// rather than stretching it.</summary>
        private static void CreateBackground(RectTransform canvasRect)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    $"Battle scene build failed: no sprite at {BackgroundPath}. Run tools/generate_battle_background.py.");
            }

            var go = new GameObject("Background", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ReferenceResolution;
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ReferenceResolution.x / ReferenceResolution.y;
        }

        /// <summary>Pins by top-left corner, <paramref name="r"/> in units from the canvas's (or
        /// parent's) top-left.</summary>
        private static void PlaceTop(RectTransform rect, Rect r)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = r.size;
            rect.anchoredPosition = new Vector2(r.x, -r.y);
        }

        /// <summary>The same reference-canvas rect, but anchored to the bottom edge.</summary>
        private static void PlaceBottom(RectTransform rect, Rect r)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = r.size;
            rect.anchoredPosition = new Vector2(r.x, ReferenceResolution.y - r.y - r.height);
        }

        private static T InstantiatePrefab<T>(string path, Transform parent, string name) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static BattleStatsBoxView CreateStatsBox(RectTransform board, string name, Vector2 position, bool bottom)
        {
            var box = InstantiatePrefab<BattleStatsBoxView>(UiPrefabBuilder.BattleStatsBoxPrefabPath, board, name);
            var r = new Rect(position, BattleStatsBoxView.Size);
            if (bottom)
            {
                PlaceBottom((RectTransform)box.transform, r);
            }
            else
            {
                PlaceTop((RectTransform)box.transform, r);
            }
            return box;
        }

        /// <summary>A mon standing on the field, with the "-12" for the Step just played as a child.</summary>
        private static Image CreateFieldSprite(RectTransform board, string name, Rect r, bool bottom, out Text damage)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(board, false);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (bottom)
            {
                PlaceBottom(image.rectTransform, r);
            }
            else
            {
                PlaceTop(image.rectTransform, r);
            }

            damage = CreateOverlayText(go.transform, "DamageText", string.Empty, 36, TextAnchor.UpperRight);
            damage.color = Theme.Danger;
            damage.GetComponent<Outline>().effectColor = Theme.TextLight;
            var damageRect = damage.rectTransform;
            damageRect.anchorMin = Vector2.zero;
            damageRect.anchorMax = Vector2.one;
            damageRect.offsetMin = Vector2.zero;
            damageRect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>Light bold text with a dark outline, readable over the backdrop.</summary>
        private static Text CreateOverlayText(Transform parent, string name, string content, int fontSize, TextAnchor anchor)
        {
            var text = CreatePlainText(parent, name, content, fontSize, anchor, Theme.TextLight);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.ChromeBg;
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static RectTransform CreateFrame(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image.rectTransform;
        }

        private static Button CreateIconButton(RectTransform pill, string name, Sprite icon, int index)
        {
            var frame = CreateFrame(pill, name, Theme.SlotDarkSprite);
            PlaceTop(frame, new Rect(PillPadding + index * (PillButtonWidth + PillGap), PillPadding, PillButtonWidth, PillButtonHeight));
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            button.colors = colors;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(frame, false);
            var image = iconGo.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            var iconRect = image.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            return button;
        }

        /// <summary>The ball and label from the mockup. Disabled: catching (design doc §12.1) isn't
        /// built, so it's here to hold the place, not to be pressed.</summary>
        private static Button CreateThrowButton(RectTransform strip, Rect r)
        {
            var frame = CreateFrame(strip, "ThrowButton", Theme.SlotDarkSprite);
            PlaceTop(frame, r);
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            button.interactable = false;

            var ballGo = new GameObject("Ball", typeof(RectTransform));
            ballGo.transform.SetParent(frame, false);
            var ball = ballGo.AddComponent<Image>();
            ball.sprite = Theme.PokeballSprite;
            ball.raycastTarget = false;
            var ballRect = ball.rectTransform;
            ballRect.anchorMin = ballRect.anchorMax = new Vector2(0.5f, 1f);
            ballRect.pivot = new Vector2(0.5f, 1f);
            ballRect.sizeDelta = new Vector2(72f, 72f);
            ballRect.anchoredPosition = new Vector2(0f, -16f);

            var label = CreatePlainText(frame, "Label", "Throw", 28, TextAnchor.MiddleCenter, Theme.TextLight);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 40f);
            labelRect.anchoredPosition = new Vector2(0f, 14f);
            return button;
        }

        private static (RectTransform panel, Text result, Button battleAgain, Button back, Button action, RectTransform catchRow) CreateResultPanel(RectTransform board)
        {
            var panel = CreateFrame(board, "ResultPanel", Theme.TextBoxSprite);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = ResultPanelSize;
            panel.anchoredPosition = new Vector2(0f, 60f);

            var result = CreatePlainText(panel, "ResultText", "Victory!", 48, TextAnchor.MiddleCenter, Theme.TextDark);
            result.fontStyle = FontStyle.Bold;
            var resultRect = result.rectTransform;
            resultRect.anchorMin = new Vector2(0f, 1f);
            resultRect.anchorMax = new Vector2(1f, 1f);
            resultRect.pivot = new Vector2(0.5f, 1f);
            resultRect.offsetMin = new Vector2(20f, -120f);
            resultRect.offsetMax = new Vector2(-20f, -20f);

            // The dev battle's pair, side by side. A node fight hides both and shows the single
            // centred action below instead — see BattleScreenController.UpdateResultButtons.
            var battleAgain = CreateButton(panel, "BattleAgainButton", "Battle Again", Theme.ButtonStyle.Confirm, useSprite: true);
            PlaceResultButton((RectTransform)battleAgain.transform, alignRight: false);
            var back = CreateButton(panel, "ResultBackButton", "Back to Team", Theme.ButtonStyle.Secondary, useSprite: true);
            PlaceResultButton((RectTransform)back.transform, alignRight: true);

            var action = CreateButton(panel, "ResultActionButton", "Continue", Theme.ButtonStyle.Primary, useSprite: true);
            var actionRect = (RectTransform)action.transform;
            actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(0.5f, 0f);
            actionRect.sizeDelta = ResultButtonSize;
            actionRect.anchoredPosition = new Vector2(0f, ResultButtonInset);

            return (panel, result, battleAgain, back, action, CreateCatchRow(panel));
        }

        /// <summary>The row a won PvE node's catch offers appear in (BattleScreenController fills it
        /// from the shared Button prefab, one per defeated wild mon — the "pick 1 from defeated" stub
        /// standing in for design doc §12.1). Sits above the panel rather than inside it: the panel
        /// is already full, and the catches read as an extra thing on offer rather than part of the
        /// result. A child of the panel all the same, so it shows and hides with it.</summary>
        private static RectTransform CreateCatchRow(RectTransform panel)
        {
            var row = CreatePanel(panel, "CatchRow", Color.clear, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(ResultPanelSize.x, CatchRowHeight);
            row.anchoredPosition = new Vector2(0f, CatchRowGap);
            AddHorizontalLayout(row, expandHeight: true);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            return row;
        }

        private static void PlaceResultButton(RectTransform rect, bool alignRight)
        {
            float x = alignRight ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(x, 0f);
            rect.pivot = new Vector2(x, 0f);
            rect.sizeDelta = ResultButtonSize;
            rect.anchoredPosition = new Vector2(alignRight ? -ResultButtonInset : ResultButtonInset, ResultButtonInset);
        }
    }
}
