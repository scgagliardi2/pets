using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Phase 0 Forest Location Hub scene (design doc §5.2) from code rather
    /// than hand-authored scene YAML — see PLAN.md §6, Phase 0's "one hand-authored Location" item.
    /// Re-run via Pets &gt; Build Forest Scene any time the Gameplay controllers' serialized fields
    /// change shape; this always rebuilds Game.unity from scratch.</summary>
    public static class ForestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        private static readonly Color PanelBg = new Color(0.93f, 0.93f, 0.88f);
        private static readonly Color TabBg = new Color(0.8f, 0.8f, 0.74f);

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
    }
}
