using System.IO;
using Pets.Data;
using Pets.Gameplay;
using Pets.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pets.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Game.unity and its slot prefabs the same way ContentSeeder builds
    /// content: Unity itself constructs and serializes real GameObjects/prefabs (not hand-typed
    /// YAML, not runtime-procedural UI), so the result is normal, Inspector-editable UI
    /// afterward. Run via the menu item below, or in batch mode with
    /// `-executeMethod Pets.Editor.GameSceneBuilder.GenerateGameScene`. Re-running replaces the
    /// scene and prefabs from scratch (not a field-by-field merge like ContentSeeder, since a
    /// whole-scene rebuild is simpler and safer than diffing a hierarchy) — do custom scene edits
    /// in a copy, not directly in Game.unity, if you don't want them clobbered by a re-run.
    ///
    /// Requires Pets/Generate Starter Content to have been run first (it needs CreatureLibrary,
    /// BotRosterLibrary, and ShopConfig assets to already exist).
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string PrefabsPath = "Assets/UI/Prefabs";

        [MenuItem("Pets/Generate Game Scene")]
        public static void GenerateGameScene()
        {
            EnsureFolder("Assets/UI");
            EnsureFolder(PrefabsPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded after NewScene (not before) — loading them earlier and holding the
            // references across the scene switch produced null asset references once the
            // scene was saved (NewScene appears to invalidate previously-loaded ScriptableObject
            // handles), so these are (re)loaded fresh right before use instead.
            var creatureLibrary = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Content/CreatureLibrary.asset");
            var botRosterLibrary = AssetDatabase.LoadAssetAtPath<BotRosterLibrary>("Assets/Content/BotRosterLibrary.asset");
            var shopConfig = AssetDatabase.LoadAssetAtPath<ShopConfig>("Assets/Content/ShopConfig.asset");

            if (creatureLibrary == null || botRosterLibrary == null || shopConfig == null)
            {
                Debug.LogError("Pets/Generate Game Scene: run Pets/Generate Starter Content first.");
                return;
            }

            var shopSlotPrefab = BuildShopSlotPrefab();
            var boardSlotPrefab = BuildBoardSlotPrefab();

            BuildEventSystem();
            var runController = BuildRunController(creatureLibrary, botRosterLibrary, shopConfig);
            var canvasGo = BuildCanvas();

            BuildShopPanel(canvasGo.transform, runController, shopSlotPrefab, boardSlotPrefab);
            BuildBattleResultPanel(canvasGo.transform, runController);
            BuildRunEndPanel(canvasGo.transform, runController);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Pets/Generate Game Scene: saved {ScenePath}");
        }

        // ---- top-level scene pieces ----

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var legacyModule = go.GetComponent<BaseInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static RunController BuildRunController(CreatureLibrary library, BotRosterLibrary roster, ShopConfig config)
        {
            var go = new GameObject("RunController");
            var controller = go.AddComponent<RunController>();
            var so = new SerializedObject(controller);
            so.FindProperty("creatureLibrary").objectReferenceValue = library;
            so.FindProperty("botRosterLibrary").objectReferenceValue = roster;
            so.FindProperty("shopConfig").objectReferenceValue = config;
            so.ApplyModifiedProperties();
            return controller;
        }

        private static GameObject BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        private static void BuildShopPanel(Transform canvasTransform, RunController runController, ShopSlotView shopSlotPrefab, BoardSlotView boardSlotPrefab)
        {
            var panelGo = CreateUIObject("ShopPanel", canvasTransform);
            StretchToParent(panelGo.GetComponent<RectTransform>());
            var panelLayout = panelGo.AddComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 16;
            panelLayout.padding = new RectOffset(24, 24, 48, 24);
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childAlignment = TextAnchor.UpperCenter;

            var headerGo = CreateUIObject("Header", panelGo.transform);
            var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 24;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            AddLayoutElement(headerGo, preferredHeight: 60);
            var goldText = CreateText("GoldText", headerGo.transform, "Gold: 0", 28);
            var livesText = CreateText("LivesText", headerGo.transform, "Lives: 0", 28);
            var roundText = CreateText("RoundText", headerGo.transform, "Round: 0", 28);

            var shopLabel = CreateText("ShopLabel", panelGo.transform, "Shop", 22);
            AddLayoutElement(shopLabel.gameObject, preferredHeight: 30);
            var shopRowGo = CreateUIObject("ShopRow", panelGo.transform);
            var shopRowLayout = shopRowGo.AddComponent<HorizontalLayoutGroup>();
            shopRowLayout.spacing = 12;
            shopRowLayout.childAlignment = TextAnchor.MiddleCenter;
            shopRowLayout.childForceExpandWidth = false;
            shopRowLayout.childForceExpandHeight = false;
            AddLayoutElement(shopRowGo, preferredHeight: 220);

            var boardLabel = CreateText("BoardLabel", panelGo.transform, "Your Board", 22);
            AddLayoutElement(boardLabel.gameObject, preferredHeight: 30);
            var boardRowGo = CreateUIObject("BoardRow", panelGo.transform);
            var boardRowLayout = boardRowGo.AddComponent<HorizontalLayoutGroup>();
            boardRowLayout.spacing = 12;
            boardRowLayout.childAlignment = TextAnchor.MiddleCenter;
            boardRowLayout.childForceExpandWidth = false;
            boardRowLayout.childForceExpandHeight = false;
            AddLayoutElement(boardRowGo, preferredHeight: 220);

            var footerGo = CreateUIObject("Footer", panelGo.transform);
            var footerLayout = footerGo.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 24;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;
            AddLayoutElement(footerGo, preferredHeight: 80);
            var rerollButton = CreateButton("RerollButton", footerGo.transform, "Reroll", 160, 60);
            var fightButton = CreateButton("FightButton", footerGo.transform, "Fight!", 160, 60);

            var shopScreenUI = panelGo.AddComponent<ShopScreenUI>();
            var so = new SerializedObject(shopScreenUI);
            so.FindProperty("runController").objectReferenceValue = runController;
            so.FindProperty("shopPanel").objectReferenceValue = panelGo;
            so.FindProperty("goldText").objectReferenceValue = goldText;
            so.FindProperty("livesText").objectReferenceValue = livesText;
            so.FindProperty("roundText").objectReferenceValue = roundText;
            so.FindProperty("shopSlotContainer").objectReferenceValue = shopRowGo.transform;
            so.FindProperty("boardSlotContainer").objectReferenceValue = boardRowGo.transform;
            so.FindProperty("shopSlotPrefab").objectReferenceValue = shopSlotPrefab;
            so.FindProperty("boardSlotPrefab").objectReferenceValue = boardSlotPrefab;
            so.FindProperty("rerollButton").objectReferenceValue = rerollButton;
            so.FindProperty("fightButton").objectReferenceValue = fightButton;
            so.ApplyModifiedProperties();
        }

        private static void BuildBattleResultPanel(Transform canvasTransform, RunController runController)
        {
            var panelGo = CreateUIObject("BattleResultPanel", canvasTransform);
            StretchToParent(panelGo.GetComponent<RectTransform>());
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var outcomeText = CreateText("OutcomeText", panelGo.transform, "", 40);
            outcomeText.color = Color.white;
            AddLayoutElement(outcomeText.gameObject, preferredHeight: 60);

            var logText = CreateText("LogText", panelGo.transform, "", 18);
            logText.color = Color.white;
            logText.alignment = TextAnchor.UpperLeft;
            AddLayoutElement(logText.gameObject, preferredHeight: 600);

            var continueButton = CreateButton("ContinueButton", panelGo.transform, "Continue", 200, 60);

            panelGo.SetActive(false);

            var battleResultUI = canvasTransform.gameObject.AddComponent<BattleResultUI>();
            var so = new SerializedObject(battleResultUI);
            so.FindProperty("runController").objectReferenceValue = runController;
            so.FindProperty("panel").objectReferenceValue = panelGo;
            so.FindProperty("outcomeText").objectReferenceValue = outcomeText;
            so.FindProperty("logText").objectReferenceValue = logText;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.ApplyModifiedProperties();
        }

        private static void BuildRunEndPanel(Transform canvasTransform, RunController runController)
        {
            var panelGo = CreateUIObject("RunEndPanel", canvasTransform);
            StretchToParent(panelGo.GetComponent<RectTransform>());
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.92f);
            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var titleText = CreateText("TitleText", panelGo.transform, "", 40);
            titleText.color = Color.white;
            AddLayoutElement(titleText.gameObject, preferredHeight: 80);

            var newRunButton = CreateButton("NewRunButton", panelGo.transform, "New Run", 220, 70);

            panelGo.SetActive(false);

            var runEndUI = canvasTransform.gameObject.AddComponent<RunEndUI>();
            var so = new SerializedObject(runEndUI);
            so.FindProperty("runController").objectReferenceValue = runController;
            so.FindProperty("panel").objectReferenceValue = panelGo;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("newRunButton").objectReferenceValue = newRunButton;
            so.ApplyModifiedProperties();
        }

        // ---- prefabs ----

        private static ShopSlotView BuildShopSlotPrefab()
        {
            var root = CreateUIObject("ShopSlotView", null);
            AddLayoutElement(root, 160, 220);
            var bg = root.AddComponent<Image>();
            bg.color = Color.gray;
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var nameText = CreateText("NameText", root.transform, "Name", 18);
            var statsText = CreateText("StatsText", root.transform, "0/0", 16);
            var costText = CreateText("CostText", root.transform, "0g", 16);
            var buyButton = CreateButton("BuyButton", root.transform, "Buy", 120, 40);
            var freezeButton = CreateButton("FreezeButton", root.transform, "Freeze", 120, 32);
            var freezeLabel = freezeButton.GetComponentInChildren<Text>();

            var view = root.AddComponent<ShopSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("buyButton").objectReferenceValue = buyButton;
            so.FindProperty("freezeButton").objectReferenceValue = freezeButton;
            so.FindProperty("freezeButtonLabel").objectReferenceValue = freezeLabel;
            so.ApplyModifiedProperties();

            var prefabGo = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsPath}/ShopSlotView.prefab");
            Object.DestroyImmediate(root);
            return prefabGo.GetComponent<ShopSlotView>();
        }

        private static BoardSlotView BuildBoardSlotPrefab()
        {
            var root = CreateUIObject("BoardSlotView", null);
            AddLayoutElement(root, 160, 220);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.7f, 0.85f, 0.7f);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var nameText = CreateText("NameText", root.transform, "Name", 16);
            var statsText = CreateText("StatsText", root.transform, "0/0", 16);

            var moveRowGo = CreateUIObject("MoveRow", root.transform);
            var moveRowLayout = moveRowGo.AddComponent<HorizontalLayoutGroup>();
            moveRowLayout.spacing = 4;
            moveRowLayout.childAlignment = TextAnchor.MiddleCenter;
            moveRowLayout.childForceExpandWidth = false;
            moveRowLayout.childForceExpandHeight = false;
            AddLayoutElement(moveRowGo, preferredHeight: 32);
            var moveLeftButton = CreateButton("MoveLeftButton", moveRowGo.transform, "<", 40, 32);
            var moveRightButton = CreateButton("MoveRightButton", moveRowGo.transform, ">", 40, 32);

            var sellButton = CreateButton("SellButton", root.transform, "Sell", 120, 36);

            var view = root.AddComponent<BoardSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("sellButton").objectReferenceValue = sellButton;
            so.FindProperty("moveLeftButton").objectReferenceValue = moveLeftButton;
            so.FindProperty("moveRightButton").objectReferenceValue = moveRightButton;
            so.ApplyModifiedProperties();

            var prefabGo = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsPath}/BoardSlotView.prefab");
            Object.DestroyImmediate(root);
            return prefabGo.GetComponent<BoardSlotView>();
        }

        // ---- low-level UI construction helpers ----

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static LayoutElement AddLayoutElement(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = go.AddComponent<LayoutElement>();
            }
            if (preferredWidth >= 0)
            {
                le.preferredWidth = preferredWidth;
            }
            if (preferredHeight >= 0)
            {
                le.preferredHeight = preferredHeight;
            }
            return le;
        }

        private static Text CreateText(string name, Transform parent, string defaultText, int fontSize)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = defaultText;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, float preferredWidth, float preferredHeight)
        {
            var go = CreateUIObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.82f, 0.82f, 0.88f);
            var button = go.AddComponent<Button>();
            AddLayoutElement(go, preferredWidth, preferredHeight);

            var labelGo = CreateText("Label", go.transform, label, 18);
            StretchToParent(labelGo.GetComponent<RectTransform>());

            return button;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
