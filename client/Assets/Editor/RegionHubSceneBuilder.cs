using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Gameplay;
using Pets.Meta;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Region Hub scene (design doc §5.2): the run's Morale/Money/Badges and a
    /// Menu button in the title bar, a header saying which Gym is next, and three Location cards —
    /// name, flavor, a row for the Location's Pokémon types (filled at runtime) and a Travel button.
    /// RegionHubController fills the cards from LocationCatalog.OffersFor.
    ///
    /// Everything is anchored by hand rather than laid out by baked groups, because the card text
    /// and type icons are written at runtime (see SceneBuilderUtils.ForceLayoutRebuild). The one
    /// layout group, each card's type row, is left live for the same reason.
    ///
    /// Re-run via Pets &gt; Build Region Hub Scene (or Pets &gt; Build All Scenes) after changing
    /// RegionHubController's serialized fields.</summary>
    public static class RegionHubSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.RegionHub + ".unity";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        // Title-bar chrome matches the Location Map's, so moving between the two doesn't shift the
        // Menu button or the resource readout.
        private const float TitleHeight = 76f;
        private const float SideMargin = 20f;
        private const float ButtonHeight = 64f;
        private const float MenuButtonWidth = 150f;
        private const float ResourceBarWidth = 460f;

        private static readonly Vector2 CardSize = new Vector2(360f, 400f);
        private const float CardSpacing = 30f;
        private const float CardCenterY = -60f;
        private const float CardInset = 18f;
        private const float TravelButtonWidth = 220f;

        [MenuItem("Pets/Build Region Hub Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Theme.ScreenBg, Vector2.zero, Vector2.one);
            CreateScreenTitleBar(canvasRect, "Region Hub", TitleHeight);
            var titleBar = (RectTransform)canvasRect.Find("TitleBar");

            var menuButton = CreateButton(titleBar, "MenuButton", "Menu", Theme.ButtonStyle.Secondary, useSprite: true);
            var menuRect = menuButton.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = menuRect.pivot = new Vector2(0f, 0.5f);
            menuRect.sizeDelta = new Vector2(MenuButtonWidth, ButtonHeight);
            menuRect.anchoredPosition = new Vector2(SideMargin, 0f);

            var (resourceBar, resourceValues) = CreateResourceBar(
                titleBar, "ResourceBar", new Vector2(1f, 0f), new Vector2(1f, 1f), new[] { "Morale", "Money", "Badges" });
            resourceBar.pivot = new Vector2(1f, 0.5f);
            resourceBar.sizeDelta = new Vector2(ResourceBarWidth, 0f);
            resourceBar.anchoredPosition = new Vector2(-SideMargin, 0f);
            var resourceBarController = resourceBar.gameObject.AddComponent<ResourceBarController>();
            SetField(resourceBarController, "moraleValue", resourceValues[0]);
            SetField(resourceBarController, "moneyValue", resourceValues[1]);
            SetField(resourceBarController, "badgesValue", resourceValues[2]);

            var headerText = CreatePlainText(canvasRect, "HeaderText", "Choose your next Location",
                Theme.FontSizeTitle, TextAnchor.MiddleCenter, Theme.TextDark);
            headerText.fontStyle = FontStyle.Bold;
            PinBelowTop(headerText.rectTransform, TitleHeight + 18f, 48f);

            var subheaderText = CreatePlainText(canvasRect, "SubheaderText", string.Empty,
                Theme.FontSizeBody, TextAnchor.MiddleCenter, Theme.TextMuted);
            subheaderText.fontStyle = FontStyle.Bold;
            PinBelowTop(subheaderText.rectTransform, TitleHeight + 68f, 30f);

            var controller = new GameObject("RegionHub").AddComponent<RegionHubController>();

            int count = LocationCatalog.OfferCount;
            var nameTexts = new Text[count];
            var flavorTexts = new Text[count];
            var typeRows = new RectTransform[count];
            var travelButtons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                var card = CreateCard(canvasRect, i, count);
                nameTexts[i] = CardText(card, "NameText", Theme.FontSizeTitle, Theme.TextDark, top: 22f, height: 44f);
                flavorTexts[i] = CardText(card, "FlavorText", Theme.FontSizeBody, Theme.TextMuted, top: 76f, height: 64f);
                CardText(card, "TypesLabel", Theme.FontSizeBody, Theme.TextDark, top: 156f, height: 28f).text = "Wild Pokémon types";
                typeRows[i] = CreateTypesRow(card, top: 190f, height: 40f);

                var travel = CreateButton(card, "TravelButton", "Travel", Theme.ButtonStyle.Confirm, useSprite: true);
                var travelRect = travel.GetComponent<RectTransform>();
                travelRect.anchorMin = travelRect.anchorMax = travelRect.pivot = new Vector2(0.5f, 0f);
                travelRect.sizeDelta = new Vector2(TravelButtonWidth, ButtonHeight);
                travelRect.anchoredPosition = new Vector2(0f, 24f);
                UnityEventTools.AddIntPersistentListener(travel.onClick, controller.OnTravelClicked, i);
                travelButtons[i] = travel;
            }

            SetField(controller, "headerText", headerText);
            SetField(controller, "subheaderText", subheaderText);
            SetField(controller, "resourceBar", resourceBarController);
            SetField(controller, "typeIconPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(TypeIconPrefabBuilder.PrefabPath));
            SetField(controller, "nameTexts", nameTexts);
            SetField(controller, "flavorTexts", flavorTexts);
            SetField(controller, "typeRows", typeRows);
            SetField(controller, "travelButtons", travelButtons);

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            UnityEventTools.AddVoidPersistentListener(menuButton.onClick, navigator.GoToIngameMenu);

            // Character Select hands its chosen pair to this screen, so the run is built here.
            LocationMapSceneBuilder.CreateBootstrapper();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Region Hub scene rebuilt at {ScenePath}");
        }

        private static RectTransform CreateCard(RectTransform canvasRect, int index, int count)
        {
            var go = new GameObject($"LocationCard{index}", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var image = go.AddComponent<Image>();
            image.sprite = Theme.TextBoxSprite;
            image.type = Image.Type.Sliced;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = CardSize;
            float offset = index - (count - 1) / 2f;
            rect.anchoredPosition = new Vector2(offset * (CardSize.x + CardSpacing), CardCenterY);
            return rect;
        }

        private static Text CardText(RectTransform card, string name, int fontSize, Color color, float top, float height)
        {
            var text = CreatePlainText(card, name, string.Empty, fontSize, TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(CardInset, -(top + height));
            rect.offsetMax = new Vector2(-CardInset, -top);
            return text;
        }

        private static RectTransform CreateTypesRow(RectTransform card, float top, float height)
        {
            var go = new GameObject("TypesRow", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(CardInset, -(top + height));
            rect.offsetMax = new Vector2(-CardInset, -top);
            return rect;
        }

        private static void PinBelowTop(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -(top + height));
            rect.offsetMax = new Vector2(-SideMargin, -top);
        }
    }
}
