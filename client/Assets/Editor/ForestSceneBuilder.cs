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
    /// <summary>Builds the Phase 0 Forest Location Hub scene (design doc §5.2) from code rather
    /// than hand-authored scene YAML — see PLAN.md §6, Phase 0's "one hand-authored Location" item.
    /// Re-run via Pets &gt; Build Forest Scene any time the Gameplay controllers' serialized fields
    /// change shape; this always rebuilds Game.unity from scratch.
    ///
    /// Hierarchy is deliberately kept flat where ForestScenePlayModeTests.cs Transform.Find()s into
    /// it (Content/&lt;Panel&gt;/&lt;child&gt;, PvEOverlay/&lt;child&gt;, etc.) — panel headers are
    /// inserted as an extra first child via AddPanelHeader rather than a wrapping level, and the
    /// PvE/Camp/RunOver overlays stay direct Canvas children exactly as before.</summary>
    public static class ForestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        private static readonly Vector2 ResourceBarMin = new Vector2(0f, 0.93f);
        private static readonly Vector2 TabBarMax = new Vector2(1f, 0.93f);
        private static readonly Vector2 TabBarMin = new Vector2(0f, 0.85f);

        [MenuItem("Pets/Build Forest Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ScreenBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas();
            CreateBootstrapper();

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);

            var (resourceBarRect, resourceValues) = CreateResourceBar(
                canvasRect, "ResourceBar", ResourceBarMin, Vector2.one, new[] { "Morale", "Money" });
            var resourceBarController = resourceBarRect.gameObject.AddComponent<ResourceBarController>();
            SetField(resourceBarController, "moraleValue", resourceValues[0]);
            SetField(resourceBarController, "moneyValue", resourceValues[1]);

            var tabBar = CreatePanel(canvasRect, "TabBar", Theme.ChromeBg, TabBarMin, TabBarMax);
            AddHorizontalLayout(tabBar, expandHeight: true);
            var teamTabBtn = CreateButton(tabBar, "TeamTabButton", "Team", Theme.ButtonStyle.Secondary);
            var mapTabBtn = CreateButton(tabBar, "MapTabButton", "Map", Theme.ButtonStyle.Secondary);
            var shopTabBtn = CreateButton(tabBar, "ShopTabButton", "Shop", Theme.ButtonStyle.Secondary);
            var centerTabBtn = CreateButton(tabBar, "CenterTabButton", "Center", Theme.ButtonStyle.Secondary);

            var content = CreatePanel(canvasRect, "Content", Color.clear, Vector2.zero, TabBarMin);

            var teamPanel = CreatePanel(content, "TeamPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(teamPanel);
            AddPanelHeader(teamPanel, "Team");
            var lineUpText = CreateText(teamPanel, "LineUpText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, 160);
            var boxText = CreateText(teamPanel, "BoxText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, 160);
            var teamController = teamPanel.gameObject.AddComponent<TeamPanelController>();
            SetField(teamController, "lineUpText", lineUpText);
            SetField(teamController, "boxText", boxText);

            var mapPanel = CreatePanel(content, "MapPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(mapPanel);
            AddPanelHeader(mapPanel, "Map");
            var nodeSequenceText = CreateText(mapPanel, "NodeSequenceText", string.Empty, Theme.FontSizeBody, TextAnchor.UpperLeft, 60);
            var currentNodeText = CreateText(mapPanel, "CurrentNodeText", string.Empty, Theme.FontSizeHeading, TextAnchor.UpperLeft, 40);
            var goButton = CreateButton(mapPanel, "GoButton", "Go", Theme.ButtonStyle.Confirm);
            var goButtonLabel = goButton.GetComponentInChildren<Text>();
            var mapController = mapPanel.gameObject.AddComponent<MapPanelController>();
            SetField(mapController, "nodeSequenceText", nodeSequenceText);
            SetField(mapController, "currentNodeText", currentNodeText);
            SetField(mapController, "goButton", goButton);
            SetField(mapController, "goButtonLabel", goButtonLabel);

            var shopPanel = CreatePanel(content, "ShopPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(shopPanel);
            AddPanelHeader(shopPanel, "Shop");
            CreateText(shopPanel, "ShopText", "Nothing for sale yet - the Shop opens in Phase 1.", Theme.FontSizeBody, TextAnchor.MiddleCenter, 60);

            var centerPanel = CreatePanel(content, "CenterPanel", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(centerPanel);
            AddPanelHeader(centerPanel, "Pokemon Center");
            CreateText(centerPanel, "CenterText", "Pokemon Center adoption is coming in Phase 1.", Theme.FontSizeBody, TextAnchor.MiddleCenter, 60);

            var pveOverlay = CreatePanel(canvasRect, "PvEOverlay", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(pveOverlay);
            AddPanelHeader(pveOverlay, "Wild Encounter");
            var logText = CreateText(pveOverlay, "LogText", string.Empty, Theme.FontSizeSmall, TextAnchor.UpperLeft, 300);
            var outcomeText = CreateText(pveOverlay, "OutcomeText", string.Empty, Theme.FontSizeHeading, TextAnchor.MiddleCenter, 40);
            var catchContainer = CreatePanel(pveOverlay, "CatchButtonsContainer", Color.clear, Vector2.zero, Vector2.one);
            var catchLayoutElement = catchContainer.gameObject.AddComponent<LayoutElement>();
            catchLayoutElement.preferredHeight = 50;
            catchLayoutElement.flexibleWidth = 1;
            AddHorizontalLayout(catchContainer, expandHeight: true);
            var pveContinueButton = CreateButton(pveOverlay, "ContinueButton", "Continue", Theme.ButtonStyle.Primary);
            var pveController = pveOverlay.gameObject.AddComponent<PvEClashController>();
            SetField(pveController, "logText", logText);
            SetField(pveController, "outcomeText", outcomeText);
            SetField(pveController, "catchButtonsContainer", catchContainer);
            SetField(pveController, "continueButton", pveContinueButton);
            // Must rebuild layout while still active — an inactive GameObject's layout groups
            // aren't included in a later rebuild pass run on an active ancestor (see
            // ForceLayoutRebuild's doc comment). catchContainer is keepLive: PvEClashController
            // adds/removes catch buttons into it at runtime, so its HorizontalLayoutGroup must
            // stay alive rather than get baked-and-destroyed like the rest of this panel.
            ForceLayoutRebuild(pveOverlay, catchContainer);
            pveOverlay.gameObject.SetActive(false);

            var campOverlay = CreatePanel(canvasRect, "CampOverlay", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(campOverlay);
            AddPanelHeader(campOverlay, "Camp");
            var campResultText = CreateText(campOverlay, "ResultText", string.Empty, Theme.FontSizeHeading, TextAnchor.MiddleCenter, 80);
            var campContinueButton = CreateButton(campOverlay, "ContinueButton", "Continue", Theme.ButtonStyle.Primary);
            var campController = campOverlay.gameObject.AddComponent<CampPanelController>();
            SetField(campController, "resultText", campResultText);
            ForceLayoutRebuild(campOverlay);
            campOverlay.gameObject.SetActive(false);

            var runOverOverlay = CreatePanel(canvasRect, "RunOverOverlay", Theme.PanelBg, Vector2.zero, Vector2.one);
            AddVerticalLayout(runOverOverlay);
            AddPanelHeader(runOverOverlay, "Run Over");
            var runOverText = CreateText(runOverOverlay, "RunOverText", "Your Morale ran out.", Theme.FontSizeHeading, TextAnchor.MiddleCenter, 80);
            runOverText.color = Theme.Danger;
            ForceLayoutRebuild(runOverOverlay);
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
            SetField(hub, "tabButtons", new[] { teamTabBtn, mapTabBtn, shopTabBtn, centerTabBtn });

            var flow = new GameObject("LocationFlow").AddComponent<LocationFlowController>();
            SetField(flow, "hub", hub);
            SetField(flow, "mapPanel", mapController);
            SetField(flow, "teamPanel", teamController);
            SetField(flow, "pveController", pveController);
            SetField(flow, "campController", campController);
            SetField(flow, "resourceBar", resourceBarController);

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

            // Covers everything still active (ResourceBar, TabBar, Content's four tab panels) —
            // see ForceLayoutRebuild's doc comment for why this is needed at all in a batchmode
            // Editor script.
            ForceLayoutRebuild(canvasRect);

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
