using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Gameplay;
using Pets.Meta;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Pokémon Center scene (ADR 0013) as a shop front: a striped awning under the
    /// title bar and two shelves of cards with a wooden plank under each row — Pokémon up for adoption
    /// on the left, the Poké Mart's supplies on the right as a two-column grid — with the clerk's line
    /// in a TextBox across the footer between Team and Leave. The run's Money, Balls and bag sit in the
    /// title bar.
    ///
    /// Every card is an instance of a prefab (ShopPrefabBuilder), and every button the Button prefab,
    /// so the look lives in Assets/Prefabs/UI rather than here. Card counts are fixed at build time:
    /// PokemonCenterShop.PokemonOnOffer Pokémon cards, then one supply card per ball tier
    /// (BallCatalog.AllTiers) followed by one per item in the ItemLibrary — add an item, rebuild this
    /// scene. The supply grid has room for two rows; the build fails loudly rather than laying a third
    /// row over the footer.
    ///
    /// Everything is anchored by hand; the only layout group is the resource bar's, left live.
    ///
    /// Re-run via Pets &gt; Build Pokemon Center Scene (or Pets &gt; Build All Scenes) after changing
    /// PokemonCenterController's serialized fields, the shop prefabs, or the item library.</summary>
    public static class PokemonCenterSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SceneNames.PokemonCenter + ".unity";
        public const string ItemLibraryPath = "Assets/Content/ItemLibrary.asset";

        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        private const float TitleHeight = 76f;
        private const float SideMargin = 20f;
        private const float BottomBarHeight = 84f;
        private const float ButtonHeight = 64f;
        private const float ResourceBarWidth = 460f;
        private const float TeamButtonWidth = 200f;
        private const float LeaveButtonWidth = 220f;
        private const float ClerkBoxHeight = 68f;
        private const float ClerkBoxGap = 20f;

        private const float AwningHeight = 26f;
        private const float AwningStripeWidth = 40f;
        private const float AwningTrimHeight = 4f;

        private const float ShelfTop = TitleHeight + AwningHeight + 12f;
        private const float ShelfGap = 20f;
        private const float ShelfHeaderHeight = 40f;
        private const float ShelfPadding = 10f;
        private const float PlankHeight = 10f;
        private const float RowGap = 8f;
        private const float ColumnGap = 16f;

        private const float PokemonShelfWidth = 740f;
        private const int SupplyColumns = 2;

        private static readonly Color Wall = new Color(0.97f, 0.93f, 0.85f);
        private static readonly Color Floor = new Color(0.82f, 0.74f, 0.62f);
        private static readonly Color ShelfBack = new Color(0.93f, 0.86f, 0.74f);
        private static readonly Color Plank = new Color(0.55f, 0.36f, 0.20f);
        private static readonly Color AwningRed = new Color(0.86f, 0.24f, 0.24f);
        private static readonly Color AwningWhite = new Color(0.98f, 0.97f, 0.94f);

        [MenuItem("Pets/Build Pokemon Center Scene")]
        public static void Build()
        {
            var itemLibrary = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibraryPath);
            if (itemLibrary == null)
            {
                throw new System.InvalidOperationException($"Pokémon Center scene build failed: no ItemLibrary at {ItemLibraryPath}.");
            }

            int supplyCount = PokemonCenterController.BallCardCount + itemLibrary.AllItems.Count;
            int supplyRows = Mathf.CeilToInt(supplyCount / (float)SupplyColumns);
            float floorTop = ReferenceResolution.y - BottomBarHeight;
            if (ShelfTop + ShelfHeight(supplyRows, ShopItemCardView.Size.y) > floorTop)
            {
                throw new System.InvalidOperationException(
                    $"Pokémon Center scene build failed: {supplyCount} supplies need {supplyRows} rows of the Poké Mart " +
                    "shelf, and it only has room for two. Rework the shelf layout before adding another item.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);

            CreatePanel(canvasRect, "Background", Wall, Vector2.zero, Vector2.one);
            var floor = CreatePanel(canvasRect, "Floor", Floor, Vector2.zero, new Vector2(1f, 0f));
            floor.offsetMax = new Vector2(0f, BottomBarHeight);

            CreateScreenTitleBar(canvasRect, "Pokémon Center", TitleHeight);
            var titleBar = (RectTransform)canvasRect.Find("TitleBar");
            var (resourceBar, resourceValues) = CreateResourceBar(
                titleBar, "ResourceBar", new Vector2(1f, 0f), new Vector2(1f, 1f), new[] { "Money", "Balls", "Items" });
            resourceBar.pivot = new Vector2(1f, 0.5f);
            resourceBar.sizeDelta = new Vector2(ResourceBarWidth, 0f);
            resourceBar.anchoredPosition = new Vector2(-SideMargin, 0f);

            CreateAwning(canvasRect);

            // Left: Pokémon for adoption, one to a row.
            int pokemonCount = PokemonCenterShop.PokemonOnOffer;
            var pokemonShelf = CreateShelf(canvasRect, "PokemonShelf", "Pokémon for Adoption",
                SideMargin, PokemonShelfWidth, pokemonCount, ShopPokemonCardView.Size.y);
            var pokemonCards = new ShopPokemonCardView[pokemonCount];
            for (int i = 0; i < pokemonCount; i++)
            {
                pokemonCards[i] = PlaceCard<ShopPokemonCardView>(pokemonShelf, ShopPrefabBuilder.ShopPokemonCardPrefabPath,
                    $"PokemonCard{i}", row: i, column: 0, columns: 1, ShopPokemonCardView.Size);
            }

            // Right: supplies — one card per ball tier, then every item — two to a row.
            float supplyShelfLeft = SideMargin + PokemonShelfWidth + ShelfGap;
            float supplyShelfWidth = ReferenceResolution.x - supplyShelfLeft - SideMargin;
            var supplyShelf = CreateShelf(canvasRect, "SupplyShelf", "Poké Mart", supplyShelfLeft, supplyShelfWidth,
                supplyRows, ShopItemCardView.Size.y);
            var supplyCards = new ShopItemCardView[supplyCount];
            for (int i = 0; i < supplyCount; i++)
            {
                supplyCards[i] = PlaceCard<ShopItemCardView>(supplyShelf, ShopPrefabBuilder.ShopItemCardPrefabPath,
                    $"SupplyCard{i}", row: i / SupplyColumns, column: i % SupplyColumns, columns: SupplyColumns,
                    ShopItemCardView.Size);
            }

            // Footer: Team (and back here) on the left, Leave for the map on the right, and the clerk's
            // counter between them.
            var bottomBar = CreatePanel(canvasRect, "BottomBar", Color.clear, Vector2.zero, new Vector2(1f, 0f));
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.offsetMax = new Vector2(0f, BottomBarHeight);
            var teamButton = CreateButton(bottomBar, "TeamButton", "Team", Theme.ButtonStyle.Primary, useSprite: true);
            AnchorInBar(teamButton, TeamButtonWidth, toRight: false);
            var leaveButton = CreateButton(bottomBar, "LeaveButton", "Leave", Theme.ButtonStyle.Secondary, useSprite: true);
            AnchorInBar(leaveButton, LeaveButtonWidth, toRight: true);

            var clerkText = CreateTextBox(bottomBar, "ClerkBox", PokemonCenterController.WelcomeLine,
                fontSize: 18, anchor: TextAnchor.MiddleLeft);
            clerkText.fontStyle = FontStyle.Bold;
            clerkText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var clerkBox = (RectTransform)clerkText.transform.parent;
            clerkBox.anchorMin = clerkBox.anchorMax = clerkBox.pivot = new Vector2(0f, 0.5f);
            clerkBox.sizeDelta = new Vector2(
                ReferenceResolution.x - 2f * SideMargin - TeamButtonWidth - LeaveButtonWidth - 2f * ClerkBoxGap, ClerkBoxHeight);
            clerkBox.anchoredPosition = new Vector2(SideMargin + TeamButtonWidth + ClerkBoxGap, 0f);

            var controller = new GameObject("PokemonCenter").AddComponent<PokemonCenterController>();
            SetField(controller, "itemLibrary", itemLibrary);
            SetField(controller, "moneyValue", resourceValues[0]);
            SetField(controller, "ballsValue", resourceValues[1]);
            SetField(controller, "itemsValue", resourceValues[2]);
            SetField(controller, "clerkText", clerkText);
            SetField(controller, "supplyCards", supplyCards);
            SetField(controller, "pokemonCards", pokemonCards);

            for (int i = 0; i < supplyCount; i++)
            {
                UnityEventTools.AddIntPersistentListener(supplyCards[i].BuyButton.onClick, controller.OnBuySupplyClicked, i);
            }
            for (int i = 0; i < pokemonCount; i++)
            {
                UnityEventTools.AddIntPersistentListener(pokemonCards[i].BuyButton.onClick, controller.OnAdoptClicked, i);
            }

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            UnityEventTools.AddVoidPersistentListener(teamButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(leaveButton.onClick, navigator.ReturnToMap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Pokémon Center scene rebuilt at {ScenePath}");
        }

        private static float RowTop(int row, float cardHeight) =>
            ShelfHeaderHeight + ShelfPadding + row * (cardHeight + PlankHeight + RowGap);

        private static float ShelfHeight(int rows, float cardHeight) =>
            RowTop(rows, cardHeight) - RowGap + ShelfPadding;

        /// <summary>Red and white stripes across the top of the shop, with a dark trim under them.</summary>
        private static void CreateAwning(RectTransform canvasRect)
        {
            var awning = CreatePanel(canvasRect, "Awning", Color.clear, new Vector2(0f, 1f), Vector2.one);
            awning.pivot = new Vector2(0.5f, 1f);
            awning.offsetMin = new Vector2(0f, -(TitleHeight + AwningHeight));
            awning.offsetMax = new Vector2(0f, -TitleHeight);

            int stripes = Mathf.CeilToInt(ReferenceResolution.x / AwningStripeWidth);
            for (int i = 0; i < stripes; i++)
            {
                var stripe = CreatePanel(awning, $"Stripe{i}", i % 2 == 0 ? AwningRed : AwningWhite, Vector2.zero, new Vector2(0f, 1f));
                stripe.GetComponent<Image>().raycastTarget = false;
                stripe.pivot = new Vector2(0f, 0.5f);
                stripe.sizeDelta = new Vector2(AwningStripeWidth, 0f);
                stripe.anchoredPosition = new Vector2(i * AwningStripeWidth, 0f);
            }

            var trim = CreatePanel(awning, "Trim", Theme.ChromeBg, Vector2.zero, new Vector2(1f, 0f));
            trim.GetComponent<Image>().raycastTarget = false;
            trim.pivot = new Vector2(0.5f, 0f);
            trim.offsetMax = new Vector2(0f, AwningTrimHeight);
        }

        /// <summary>A shelf unit: a wooden-backed panel with a dark sign across the top and one plank under
        /// each row of cards.</summary>
        private static RectTransform CreateShelf(RectTransform canvasRect, string name, string title, float left, float width,
            int rows, float cardHeight)
        {
            var shelf = CreatePanel(canvasRect, name, ShelfBack, Vector2.zero, Vector2.zero);
            PlaceTopLeft(shelf, left, ShelfTop, width, ShelfHeight(rows, cardHeight));

            var header = CreatePanel(shelf, "Header", Theme.PanelHeaderBg, new Vector2(0f, 1f), Vector2.one);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -ShelfHeaderHeight);
            var headerText = CreatePlainText(header, "Title", title, Theme.FontSizeHeading + 2, TextAnchor.MiddleCenter, Theme.TextLight);
            headerText.fontStyle = FontStyle.Bold;
            StretchTo(headerText.rectTransform, Vector2.zero, Vector2.one);

            for (int i = 0; i < rows; i++)
            {
                var plank = CreatePanel(shelf, $"Plank{i}", Plank, new Vector2(0f, 1f), Vector2.one);
                plank.GetComponent<Image>().raycastTarget = false;
                plank.pivot = new Vector2(0.5f, 1f);
                float top = RowTop(i, cardHeight) + cardHeight;
                plank.offsetMin = new Vector2(4f, -(top + PlankHeight));
                plank.offsetMax = new Vector2(-4f, -top);
            }
            return shelf;
        }

        /// <summary>A card prefab instance standing on row <paramref name="row"/>'s plank, in column
        /// <paramref name="column"/> of a grid centred across the shelf.</summary>
        private static T PlaceCard<T>(RectTransform shelf, string prefabPath, string name, int row, int column, int columns,
            Vector2 size) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Pokémon Center scene build failed: no prefab at {prefabPath}. Run Pets > Build All Scenes, which builds the shop prefabs first.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, shelf);
            instance.name = name;
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2((column - (columns - 1) / 2f) * (size.x + ColumnGap), -RowTop(row, size.y));
            return instance.GetComponent<T>();
        }

        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static void AnchorInBar(Button button, float width, bool toRight)
        {
            var rect = button.GetComponent<RectTransform>();
            float x = toRight ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(x, 0.5f);
            rect.sizeDelta = new Vector2(width, ButtonHeight);
            rect.anchoredPosition = new Vector2(toRight ? -SideMargin : SideMargin, 0f);
        }
    }
}
