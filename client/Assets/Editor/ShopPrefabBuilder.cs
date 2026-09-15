using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pets.UI;
using static Pets.EditorTools.SceneBuilderUtils;

namespace Pets.EditorTools
{
    /// <summary>Builds the Pokémon Center's shop prefabs and the Team screen's item chip:
    /// - ShopItemCard (Pets.UI.ShopItemCardView) — a supply on the shelf: icon on a display pad, name,
    ///   what the run owns, a description, a gold price tag and a nested Button prefab.
    /// - ShopPokemonCard (Pets.UI.ShopPokemonCardView) — a Pokémon up for adoption: its sprite on a
    ///   display pad and the battle screen's BattleStatsBox prefab nested beside it, then tier/growth,
    ///   price tag, an Adopt Button prefab and a stamp for once it's gone.
    /// - ItemChip (Pets.UI.ItemChipView) — an item in the Team screen's bag.
    ///
    /// Nests the Button and BattleStatsBox prefabs rather than rebuilding them, so it runs after
    /// UiPrefabBuilder (SceneCatalog.BuildAll does). Same contract as UiPrefabBuilder: always rebuilt
    /// from scratch, so hand edits in Prefab Mode survive only if reflected here.</summary>
    public static class ShopPrefabBuilder
    {
        private const string FolderPath = "Assets/Prefabs/UI";
        public const string ShopItemCardPrefabPath = FolderPath + "/ShopItemCard.prefab";
        public const string ShopPokemonCardPrefabPath = FolderPath + "/ShopPokemonCard.prefab";
        public const string ItemChipPrefabPath = FolderPath + "/ItemChip.prefab";

        // Pokémon card (720x136).
        private const float Inset = 14f;
        private const float PadSize = 108f;
        private static readonly Vector2 PriceTagSize = new Vector2(86f, 52f);
        private const float BuyButtonHeight = 56f;
        private const float PokemonBuyButtonWidth = 140f;

        // Supply card (220x214): icon pad with the name and count beside it, the description under
        // both, then the price tag and Buy along the bottom.
        private const float ItemInset = 12f;
        private const float ItemPadSize = 64f;
        private const float ItemTextLeft = ItemInset + ItemPadSize + 10f;
        private static readonly Vector2 ItemPriceTagSize = new Vector2(76f, 48f);
        private const float ItemBuyButtonWidth = 110f;
        private const float ItemBuyButtonHeight = 52f;

        private const float StatsBoxLeft = 130f;
        private const float PokemonInfoLeft = StatsBoxLeft + 320f + 14f;

        [MenuItem("Pets/Build Shop Prefabs")]
        public static void Build()
        {
            BuildShopItemCard();
            BuildShopPokemonCard();
            BuildItemChip();
            AssetDatabase.SaveAssets();
            Debug.Log($"Shop prefabs rebuilt under {FolderPath}");
        }

        private static void BuildShopItemCard()
        {
            var go = CreateRoot("ShopItemCard", ShopItemCardView.Size, Theme.TextBoxSprite);

            var pad = CreateSlicedImage(go.transform, "IconPad", Theme.SlotDarkSprite);
            PlaceTopLeft(pad.rectTransform, ItemInset, ItemInset, ItemPadSize, ItemPadSize);
            var icon = CreateInsetImage(pad.transform, "Icon", 12f);

            var name = CreateCardText(go.transform, "NameText", "Item", 20, TextAnchor.MiddleLeft, Theme.TextDark, bold: true);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Theme.FontSizeSmall;
            name.resizeTextMaxSize = 20;
            PinBetween(name.rectTransform, ItemTextLeft, ItemInset, top: 14f, height: 28f);
            var owned = CreateCardText(go.transform, "OwnedText", "Have 0", 16, TextAnchor.MiddleLeft, Theme.TextMuted, bold: true);
            PinBetween(owned.rectTransform, ItemTextLeft, ItemInset, top: 44f, height: 24f);

            var description = CreateCardText(go.transform, "DescriptionText", "What it does.", 14, TextAnchor.UpperLeft, Theme.TextMuted, bold: true);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            PinBetween(description.rectTransform, ItemInset + 4f, ItemInset + 4f, top: 86f, height: 56f);

            var price = CreatePriceTag(go.transform, ItemInset, ItemPriceTagSize, ItemInset);
            var buy = CreateBuyButton(go.transform, "BuyButton", "Buy", ItemBuyButtonWidth, ItemBuyButtonHeight, ItemInset);

            var view = go.AddComponent<ShopItemCardView>();
            SetField(view, "icon", icon);
            SetField(view, "nameText", name);
            SetField(view, "ownedText", owned);
            SetField(view, "descriptionText", description);
            SetField(view, "priceText", price);
            SetField(view, "buyButton", buy);
            icon.sprite = Theme.PokeballSprite;

            SavePrefab(go, ShopItemCardPrefabPath);
        }

        private static void BuildShopPokemonCard()
        {
            var go = CreateRoot("ShopPokemonCard", ShopPokemonCardView.Size, Theme.TextBoxSprite);
            var group = go.AddComponent<CanvasGroup>();

            var pad = CreateSlicedImage(go.transform, "PortraitPad", Theme.SlotBlueSprite);
            PlaceTopLeft(pad.rectTransform, Inset, Inset, PadSize, PadSize);
            var portrait = CreateInsetImage(pad.transform, "Portrait", 10f);

            var statsBox = InstantiateNested<BattleStatsBoxView>(UiPrefabBuilder.BattleStatsBoxPrefabPath, go.transform, "StatsBox");
            PlaceTopLeft((RectTransform)statsBox.transform, StatsBoxLeft,
                (ShopPokemonCardView.Size.y - BattleStatsBoxView.Size.y) / 2f, BattleStatsBoxView.Size.x, BattleStatsBoxView.Size.y);

            var info = CreateCardText(go.transform, "InfoText", "Tier 1", 18, TextAnchor.MiddleLeft, Theme.TextDark, bold: true);
            PinBetween(info.rectTransform, PokemonInfoLeft, Inset, top: 14f, height: 30f);

            var price = CreatePriceTag(go.transform, PokemonInfoLeft, PriceTagSize, Inset);
            var buy = CreateBuyButton(go.transform, "BuyButton", "Adopt", PokemonBuyButtonWidth, BuyButtonHeight, Inset);

            // Over everything, off until the mon is adopted. No raycast, so the card underneath still
            // reads as a card rather than a blocked-off area.
            var stamp = CreateCardText(go.transform, "SoldStamp", "ADOPTED", 44, TextAnchor.MiddleCenter, Theme.Danger, bold: true);
            var stampRect = stamp.rectTransform;
            stampRect.anchorMin = Vector2.zero;
            stampRect.anchorMax = Vector2.one;
            stampRect.offsetMin = Vector2.zero;
            stampRect.offsetMax = Vector2.zero;
            stampRect.localEulerAngles = new Vector3(0f, 0f, 8f);
            stamp.gameObject.AddComponent<Outline>().effectColor = Theme.TextLight;
            stamp.gameObject.SetActive(false);

            var view = go.AddComponent<ShopPokemonCardView>();
            SetField(view, "portrait", portrait);
            SetField(view, "statsBox", statsBox);
            SetField(view, "infoText", info);
            SetField(view, "priceText", price);
            SetField(view, "buyButton", buy);
            SetField(view, "soldStamp", stamp.gameObject);
            SetField(view, "group", group);

            SavePrefab(go, ShopPokemonCardPrefabPath);
        }

        private static void BuildItemChip()
        {
            // The frame keeps its raycast: it's what a player presses to drag the item.
            var go = CreateRoot("ItemChip", ItemChipView.Size, Theme.SlotDarkSprite, raycast: true);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = ItemChipView.Size.x;
            layout.preferredHeight = ItemChipView.Size.y;

            const float iconSize = 30f;
            var icon = CreatePlainImage(go.transform, "Icon");
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            icon.rectTransform.anchoredPosition = new Vector2(10f, 0f);

            var label = CreateCardText(go.transform, "Label", "Item", 17, TextAnchor.MiddleLeft, Theme.TextLight, bold: true);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f + iconSize + 8f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            var view = go.AddComponent<ItemChipView>();
            SetField(view, "icon", icon);
            SetField(view, "label", label);

            SavePrefab(go, ItemChipPrefabPath);
        }

        // ---- pieces ---------------------------------------------------------------------------

        private static GameObject CreateRoot(string name, Vector2 size, Sprite sprite, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = raycast;
            return go;
        }

        /// <summary>The gold price tag: the Lead-slot frame art with the price on it, bottom-left of
        /// the card's content column.</summary>
        private static Text CreatePriceTag(Transform card, float left, Vector2 size, float bottom)
        {
            var tag = CreateSlicedImage(card, "PriceTag", Theme.SlotGoldSprite);
            var rect = tag.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(left, bottom);

            var text = CreateCardText(tag.transform, "PriceText", "$0", 24, TextAnchor.MiddleCenter, Theme.TextLight, bold: true);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return text;
        }

        private static UiButton CreateBuyButton(Transform card, string name, string label, float width, float height, float inset)
        {
            var button = InstantiateNested<UiButton>(UiPrefabBuilder.ButtonPrefabPath, card, name);
            button.Style = Theme.ButtonStyle.Confirm;
            button.Text = label;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-inset, inset - 2f);
            return button;
        }

        private static Image CreateSlicedImage(Transform parent, string name, Sprite sprite)
        {
            var image = CreatePlainImage(parent, name);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Image CreateInsetImage(Transform parent, string name, float inset)
        {
            var image = CreatePlainImage(parent, name);
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return image;
        }

        private static Image CreatePlainImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateCardText(Transform parent, string name, string content, int fontSize,
            TextAnchor anchor, Color color, bool bold)
        {
            var text = CreatePlainText(parent, name, content, fontSize, anchor, color);
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static void PlaceTopRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-x, -y);
        }

        /// <summary>Stretched between <paramref name="left"/> and <paramref name="right"/> units in from
        /// the card's edges, <paramref name="top"/> down with a fixed height.</summary>
        private static void PinBetween(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static T InstantiateNested<T>(string prefabPath, Transform parent, string name) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Shop prefab build failed: {prefabPath} doesn't exist yet. Run Pets > Build UI Prefabs first.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
