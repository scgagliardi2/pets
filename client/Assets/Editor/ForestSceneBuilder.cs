using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;

namespace Pets.EditorTools
{
    /// <summary>Builds the Phase 0 Forest Location Hub scene (design doc §5.2) from code rather
    /// than hand-authored scene YAML — see PLAN.md §6, Phase 0's "one hand-authored Location" item.
    /// Re-run via Pets &gt; Build Forest Scene any time the Gameplay controllers' serialized fields
    /// change shape; this always rebuilds Game.unity from scratch.</summary>
    public static class ForestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private static readonly Color PanelBg = new Color(0.93f, 0.93f, 0.88f);
        private static readonly Color TabBg = new Color(0.8f, 0.8f, 0.74f);
        private static readonly Color ButtonBg = new Color(0.7f, 0.8f, 0.9f);

        [MenuItem("Pets/Build Forest Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera();
            CreateEventSystem();
            var canvasRect = CreateCanvas();
            CreateBootstrapper();

            var tabBar = CreatePanel(canvasRect, "TabBar", TabBg, new Vector2(0, 0.9f), Vector2.one);
            AddHorizontalLayout(tabBar, expandHeight: true);
            var teamTabBtn = CreateButton(tabBar, "TeamTabButton", "Team");
            var mapTabBtn = CreateButton(tabBar, "MapTabButton", "Map");
            var shopTabBtn = CreateButton(tabBar, "ShopTabButton", "Shop");
            var centerTabBtn = CreateButton(tabBar, "CenterTabButton", "Center");

            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, new Vector2(1, 0.9f));

            var teamPanel = CreatePanel(content, "TeamPanel", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(teamPanel);
            var lineUpText = CreateText(teamPanel, "LineUpText", string.Empty, 20, TextAnchor.UpperLeft, 160);
            var boxText = CreateText(teamPanel, "BoxText", string.Empty, 20, TextAnchor.UpperLeft, 160);
            var teamController = teamPanel.gameObject.AddComponent<TeamPanelController>();
            SetField(teamController, "lineUpText", lineUpText);
            SetField(teamController, "boxText", boxText);

            var mapPanel = CreatePanel(content, "MapPanel", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(mapPanel);
            var nodeSequenceText = CreateText(mapPanel, "NodeSequenceText", string.Empty, 18, TextAnchor.UpperLeft, 60);
            var currentNodeText = CreateText(mapPanel, "CurrentNodeText", string.Empty, 22, TextAnchor.UpperLeft, 40);
            var goButton = CreateButton(mapPanel, "GoButton", "Go");
            var goButtonLabel = goButton.GetComponentInChildren<Text>();
            var mapController = mapPanel.gameObject.AddComponent<MapPanelController>();
            SetField(mapController, "nodeSequenceText", nodeSequenceText);
            SetField(mapController, "currentNodeText", currentNodeText);
            SetField(mapController, "goButton", goButton);
            SetField(mapController, "goButtonLabel", goButtonLabel);

            var shopPanel = CreatePanel(content, "ShopPanel", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(shopPanel);
            CreateText(shopPanel, "ShopText", "Nothing for sale yet - the Shop opens in Phase 1.", 20, TextAnchor.MiddleCenter, 60);

            var centerPanel = CreatePanel(content, "CenterPanel", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(centerPanel);
            CreateText(centerPanel, "CenterText", "Pokemon Center adoption is coming in Phase 1.", 20, TextAnchor.MiddleCenter, 60);

            var pveOverlay = CreatePanel(canvasRect, "PvEOverlay", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(pveOverlay);
            var logText = CreateText(pveOverlay, "LogText", string.Empty, 16, TextAnchor.UpperLeft, 300);
            var outcomeText = CreateText(pveOverlay, "OutcomeText", string.Empty, 24, TextAnchor.MiddleCenter, 40);
            var catchContainer = CreatePanel(pveOverlay, "CatchButtonsContainer", Color.clear, Vector2.zero, Vector2.one);
            var catchLayoutElement = catchContainer.gameObject.AddComponent<LayoutElement>();
            catchLayoutElement.preferredHeight = 50;
            catchLayoutElement.flexibleWidth = 1;
            AddHorizontalLayout(catchContainer, expandHeight: true);
            var pveContinueButton = CreateButton(pveOverlay, "ContinueButton", "Continue");
            var pveController = pveOverlay.gameObject.AddComponent<PvEClashController>();
            SetField(pveController, "logText", logText);
            SetField(pveController, "outcomeText", outcomeText);
            SetField(pveController, "catchButtonsContainer", catchContainer);
            SetField(pveController, "continueButton", pveContinueButton);
            pveOverlay.gameObject.SetActive(false);

            var campOverlay = CreatePanel(canvasRect, "CampOverlay", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(campOverlay);
            var campResultText = CreateText(campOverlay, "ResultText", string.Empty, 20, TextAnchor.MiddleCenter, 80);
            var campContinueButton = CreateButton(campOverlay, "ContinueButton", "Continue");
            var campController = campOverlay.gameObject.AddComponent<CampPanelController>();
            SetField(campController, "resultText", campResultText);
            campOverlay.gameObject.SetActive(false);

            var runOverOverlay = CreatePanel(canvasRect, "RunOverOverlay", PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(runOverOverlay);
            CreateText(runOverOverlay, "RunOverText", "Run Over - your Morale ran out.", 26, TextAnchor.MiddleCenter, 80);
            runOverOverlay.gameObject.SetActive(false);

            var hub = new GameObject("LocationHub").AddComponent<LocationHubController>();
            SetField(hub, "tabBar", tabBar.gameObject);
            SetField(hub, "teamPanel", teamPanel.gameObject);
            SetField(hub, "mapPanel", mapPanel.gameObject);
            SetField(hub, "shopPanel", shopPanel.gameObject);
            SetField(hub, "centerPanel", centerPanel.gameObject);
            SetField(hub, "pveOverlay", pveOverlay.gameObject);
            SetField(hub, "campOverlay", campOverlay.gameObject);
            SetField(hub, "runOverOverlay", runOverOverlay.gameObject);

            var flow = new GameObject("LocationFlow").AddComponent<LocationFlowController>();
            SetField(flow, "hub", hub);
            SetField(flow, "mapPanel", mapController);
            SetField(flow, "teamPanel", teamController);
            SetField(flow, "pveController", pveController);
            SetField(flow, "campController", campController);

            SetField(mapController, "flow", flow);
            SetField(pveController, "flow", flow);
            SetField(campController, "flow", flow);

            UnityEventTools.AddIntPersistentListener(teamTabBtn.onClick, hub.ShowTab, 0);
            UnityEventTools.AddIntPersistentListener(mapTabBtn.onClick, hub.ShowTab, 1);
            UnityEventTools.AddIntPersistentListener(shopTabBtn.onClick, hub.ShowTab, 2);
            UnityEventTools.AddIntPersistentListener(centerTabBtn.onClick, hub.ShowTab, 3);
            UnityEventTools.AddVoidPersistentListener(goButton.onClick, mapController.OnGoClicked);
            UnityEventTools.AddVoidPersistentListener(pveContinueButton.onClick, pveController.OnContinueClicked);
            UnityEventTools.AddVoidPersistentListener(campContinueButton.onClick, campController.OnContinueClicked);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Forest scene rebuilt at {ScenePath}");
        }

        private static void CreateMainCamera()
        {
            // Screen Space - Overlay UI doesn't need a camera to render, but the Game view shows
            // a "No cameras rendering" placeholder without one — a plain solid-color camera avoids
            // that and gives the empty space behind panels a background.
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            camera.orthographic = true;
        }

        private static void CreateEventSystem()
        {
            // The project's Active Input Handling is set to the new Input System package only
            // (activeInputHandler: 1 in ProjectSettings) — the legacy StandaloneInputModule throws
            // at runtime under that setting, so UI needs InputSystemUIInputModule instead.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 720);
            go.AddComponent<GraphicRaycaster>();
            return go.GetComponent<RectTransform>();
        }

        private static void CreateBootstrapper()
        {
            var bootstrapper = new GameObject("RunBootstrapper").AddComponent<RunBootstrapper>();
            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>("Assets/Content/PokemonSpeciesLibrary.asset");
            SetField(bootstrapper, "speciesLibrary", library);

            var lead = AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/charmander.asset");
            var support = AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>("Assets/Content/Species/squirtle.asset");
            SetField(bootstrapper, "starterLead", lead);
            SetField(bootstrapper, "starterSupport", support);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color background, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
            }
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.black;
            text.text = content;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonBg;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
            layoutElement.flexibleWidth = 1;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = label;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static void AddVerticalLayout(RectTransform panel)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
        }

        private static void AddHorizontalLayout(RectTransform panel, bool expandHeight)
        {
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = expandHeight;
            layout.childControlWidth = false;
            layout.childControlHeight = expandHeight;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
