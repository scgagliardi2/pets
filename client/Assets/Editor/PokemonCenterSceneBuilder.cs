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
    /// title bar and three full-width shelf rows (ADR 0014), each with a dark sign on its left and a
    /// wooden plank under its cards — Pokémon up for adoption, then Poké Balls, then held items — with
    /// the clerk's line in a TextBox across the footer between Team and Leave. The run's Money, Balls
    /// and bag sit in the title bar.
    ///
    /// Every card is an instance of a prefab (ShopPrefabBuilder), and every button the Button prefab,
    /// so the look lives in Assets/Prefabs/UI rather than here. Card counts are fixed at build time:
    /// PokemonCenterShop.PokemonOnOffer Pokémon, one card per ball tier (BallCatalog.AllTiers), and
    /// PokemonCenterShop.ItemsOnOffer items — which items is rolled per visit, so adding an item to the
    /// library needs no rebuild. The build fails loudly rather than overrunning a row or the footer.
    ///
    /// Everything is anchored by hand; the only layout group is the resource bar's, left live.
    ///
    /// Re-run via Pets &gt; Build Pokemon Center Scene (or Pets &gt; Build All Scenes) after changing
    /// PokemonCenterController's serialized fields or the shop prefabs.</summary>
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

        private const float ShelfTop = TitleHeight + AwningHeight + 8f;
        private const float ShelfGap = 8f;
        private const float ShelfInset = 8f;
        private const float PlankHeight = 8f;
        private const float ShelfBottomPadding = 4f;
        private const float SignWidth = 104f;
        private const float CardsLeft = ShelfInset + SignWidth + 10f;
        private const float CardGap = 12f;

        private static readonly Color Wall = new Color(0.97f, 0.93f, 0.85f);
        private static readonly Color Floor = new Color(0.82f, 0.74f, 0.62f);
        private static readonly Color ShelfBack = new Color(0.93f, 0.86f, 0.74f);
        private static readonly Color Plank = new Color(0.55f, 0.36f, 0.20f);
        private static readonly Color AwningRed = new Color(0.86f, 0.24f, 0.24f);
        private static readonly Color AwningWhite = new Color(0.98f, 0.97f, 0.94f);

        private static float ShelfWidth => ReferenceResolution.x - 2f * SideMargin;

        [MenuItem("Pets/Build Pokemon Center Scene")]
        public static void Build()
        {
            var itemLibrary = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibraryPath);
            if (itemLibrary == null)
            {
                throw new System.InvalidOperationException($"Pokémon Center scene build failed: no ItemLibrary at {ItemLibraryPath}.");
            }

            int pokemonCount = PokemonCenterShop.PokemonOnOffer;
            int ballCount = BallCatalog.AllTiers.Length;
            int itemCount = PokemonCenterShop.ItemsOnOffer;
            CheckRowFits("Pokémon", pokemonCount, ShopPokemonCardView.Size);
            CheckRowFits("Poké Ball", ballCount, ShopItemCardView.Size);
            CheckRowFits("item", itemCount, ShopItemCardView.Size);
            float floorTop = ReferenceResolution.y - BottomBarHeight;
            float shelvesBottom = ShelfTopFor(2, ShopPokemonCardView.Size.y, ShopItemCardView.Size.y)
                + ShelfHeight(ShopItemCardView.Size.y);
            if (shelvesBottom > floorTop)
            {
                throw new System.InvalidOperationException(
                    $"Pokémon Center scene build failed: the three shelves reach {shelvesBottom} and the footer starts at {floorTop}.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera(Theme.ChromeBg);
            CreateEventSystem();
            var canvasRect = CreateCanvas(ReferenceResolution);
            // Expand, not the default match-width: the three shelves need the full 720 units of
            // height, and matching width on a screen wider than 16:9 (a 2.1:1 window is ~600 units
            // tall) ran the item row under the footer. Expand guarantees at least 1280x720 at any
            // aspect, and the extra room lands at the sides, where the shelves are centred.
            canvasRect.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

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

            var pokemonShelf = CreateShelf(canvasRect, "PokemonShelf", "Pokémon",
                ShelfTopFor(0, ShopPokemonCardView.Size.y, ShopItemCardView.Size.y), ShopPokemonCardView.Size.y);
            var pokemonCards = new ShopPokemonCardView[pokemonCount];
            for (int i = 0; i < pokemonCount; i++)
            {
                pokemonCards[i] = PlaceCard<ShopPokemonCardView>(pokemonShelf, ShopPrefabBuilder.ShopPokemonCardPrefabPath,
                    $"PokemonCard{i}", i, ShopPokemonCardView.Size);
            }

            var ballShelf = CreateShelf(canvasRect, "BallShelf", "Poké\nBalls",
                ShelfTopFor(1, ShopPokemonCardView.Size.y, ShopItemCardView.Size.y), ShopItemCardView.Size.y);
            var ballCards = new ShopItemCardView[ballCount];
            for (int i = 0; i < ballCount; i++)
            {
                ballCards[i] = PlaceCard<ShopItemCardView>(ballShelf, ShopPrefabBuilder.ShopItemCardPrefabPath,
                    $"BallCard{i}", i, ShopItemCardView.Size);
            }

            var itemShelf = CreateShelf(canvasRect, "ItemShelf", "Held\nItems",
                ShelfTopFor(2, ShopPokemonCardView.Size.y, ShopItemCardView.Size.y), ShopItemCardView.Size.y);
            var itemCards = new ShopItemCardView[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                itemCards[i] = PlaceCard<ShopItemCardView>(itemShelf, ShopPrefabBuilder.ShopItemCardPrefabPath,
                    $"ItemCard{i}", i, ShopItemCardView.Size);
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
            // Stretched between the two buttons, so a wider-than-reference canvas widens the counter
            // rather than leaving a gap before Leave.
            var clerkBox = (RectTransform)clerkText.transform.parent;
            clerkBox.anchorMin = new Vector2(0f, 0.5f);
            clerkBox.anchorMax = new Vector2(1f, 0.5f);
            clerkBox.pivot = new Vector2(0.5f, 0.5f);
            clerkBox.offsetMin = new Vector2(SideMargin + TeamButtonWidth + ClerkBoxGap, -ClerkBoxHeight / 2f);
            clerkBox.offsetMax = new Vector2(-(SideMargin + LeaveButtonWidth + ClerkBoxGap), ClerkBoxHeight / 2f);

            var controller = new GameObject("PokemonCenter").AddComponent<PokemonCenterController>();
            SetField(controller, "itemLibrary", itemLibrary);
            SetField(controller, "moneyValue", resourceValues[0]);
            SetField(controller, "ballsValue", resourceValues[1]);
            SetField(controller, "itemsValue", resourceValues[2]);
            SetField(controller, "clerkText", clerkText);
            SetField(controller, "pokemonCards", pokemonCards);
            SetField(controller, "ballCards", ballCards);
            SetField(controller, "itemCards", itemCards);

            for (int i = 0; i < pokemonCount; i++)
            {
                UnityEventTools.AddIntPersistentListener(pokemonCards[i].BuyButton.onClick, controller.OnAdoptClicked, i);
            }
            for (int i = 0; i < ballCount; i++)
            {
                UnityEventTools.AddIntPersistentListener(ballCards[i].BuyButton.onClick, controller.OnBuyBallClicked, i);
            }
            for (int i = 0; i < itemCount; i++)
            {
                UnityEventTools.AddIntPersistentListener(itemCards[i].BuyButton.onClick, controller.OnBuyItemClicked, i);
            }

            var navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
            UnityEventTools.AddVoidPersistentListener(teamButton.onClick, navigator.GoToTeam);
            UnityEventTools.AddVoidPersistentListener(leaveButton.onClick, navigator.ReturnToMap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            SceneCatalog.EnsureBuildScenes();

            Debug.Log($"Pokémon Center scene rebuilt at {ScenePath}");
        }

        private static float ShelfHeight(float cardHeight) => ShelfInset + cardHeight + PlankHeight + ShelfBottomPadding;

        /// <summary>Row 0 holds the Pokémon; rows 1 and 2 hold supply cards.</summary>
        private static float ShelfTopFor(int row, float pokemonCardHeight, float supplyCardHeight)
        {
            float top = ShelfTop;
            for (int r = 0; r < row; r++)
            {
                top += ShelfHeight(r == 0 ? pokemonCardHeight : supplyCardHeight) + ShelfGap;
            }
            return top;
        }

        private static void CheckRowFits(string what, int count, Vector2 cardSize)
        {
            float needed = CardsLeft + count * cardSize.x + (count - 1) * CardGap + ShelfInset;
            if (needed > ShelfWidth)
            {
                throw new System.InvalidOperationException(
                    $"Pokémon Center scene build failed: {count} {what} cards need {needed} units of a {ShelfWidth}-unit shelf. " +
                    "Rework the shelf layout before offering more.");
            }
        }

        /// <summary>Red and white stripes across the top of the shop, with a dark trim under them.</summary>
        private static void CreateAwning(RectTransform canvasRect)
        {
            var awning = CreatePanel(canvasRect, "Awning", Color.clear, new Vector2(0f, 1f), Vector2.one);
            awning.pivot = new Vector2(0.5f, 1f);
            awning.offsetMin = new Vector2(0f, -(TitleHeight + AwningHeight));
            awning.offsetMax = new Vector2(0f, -TitleHeight);

            // Enough stripes for a canvas twice the reference width — Expand widens it on wide screens,
            // and the awning is clipped by nothing, so extra stripes past the edge are simply offscreen.
            int stripes = Mathf.CeilToInt(2f * ReferenceResolution.x / AwningStripeWidth);
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

        /// <summary>A full-width shelf row: a wooden-backed panel with a dark sign naming the row on its
        /// left and a plank under the cards.</summary>
        private static RectTransform CreateShelf(RectTransform canvasRect, string name, string title, float top, float cardHeight)
        {
            // Centred across the top rather than pinned to the left edge, so a canvas wider than the
            // reference (Expand, on a wide screen) splits its extra room evenly either side.
            var shelf = CreatePanel(canvasRect, name, ShelfBack, Vector2.zero, Vector2.zero);
            shelf.anchorMin = shelf.anchorMax = shelf.pivot = new Vector2(0.5f, 1f);
            shelf.sizeDelta = new Vector2(ShelfWidth, ShelfHeight(cardHeight));
            shelf.anchoredPosition = new Vector2(0f, -top);

            var sign = CreatePanel(shelf, "Sign", Theme.PanelHeaderBg, Vector2.zero, Vector2.zero);
            PlaceTopLeft(sign, ShelfInset, ShelfInset, SignWidth, cardHeight);
            var signText = CreatePlainText(sign, "Title", title, Theme.FontSizeHeading + 2, TextAnchor.MiddleCenter, Theme.TextLight);
            signText.fontStyle = FontStyle.Bold;
            StretchTo(signText.rectTransform, Vector2.zero, Vector2.one);

            var plank = CreatePanel(shelf, "Plank", Plank, new Vector2(0f, 1f), Vector2.one);
            plank.GetComponent<Image>().raycastTarget = false;
            plank.pivot = new Vector2(0.5f, 1f);
            float plankTop = ShelfInset + cardHeight;
            plank.offsetMin = new Vector2(4f, -(plankTop + PlankHeight));
            plank.offsetMax = new Vector2(-4f, -plankTop);
            return shelf;
        }

        /// <summary>A card prefab instance standing on the shelf's plank, in column <paramref name="column"/>
        /// to the right of the sign.</summary>
        private static T PlaceCard<T>(RectTransform shelf, string prefabPath, string name, int column, Vector2 size)
            where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Pokémon Center scene build failed: no prefab at {prefabPath}. Run Pets > Build All Scenes, which builds the shop prefabs first.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, shelf);
            instance.name = name;
            PlaceTopLeft(instance.GetComponent<RectTransform>(), CardsLeft + column * (size.x + CardGap), ShelfInset, size.x, size.y);
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
