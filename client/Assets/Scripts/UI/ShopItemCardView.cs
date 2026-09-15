using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>One supply on the Pokémon Center's shelf — a Poké Ball or an item. See
    /// Assets/Prefabs/UI/ShopItemCard.prefab (built by Pets.EditorTools.ShopPrefabBuilder): an icon
    /// on a display pad with the name, how many the run already owns and a description beside it, and
    /// a gold price tag and a nested Button prefab along the bottom. An item bought off the shelf stays
    /// on it, faded, stamped Sold.</summary>
    public sealed class ShopItemCardView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(360f, 140f);

        private const float SoldAlpha = 0.55f;

        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text ownedText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text priceText;
        [SerializeField] private UiButton buyButton;
        [SerializeField] private GameObject soldStamp;
        [SerializeField] private CanvasGroup group;

        public Text NameText => nameText;
        public Text OwnedText => ownedText;
        public Text PriceText => priceText;
        public Button BuyButton => buyButton.Button;
        public bool IsSold => soldStamp.activeSelf;

        /// <param name="iconTint">White for an item's own art; a ball tier's colour for the shared
        /// Poké Ball sprite.</param>
        public void Show(string displayName, Sprite iconSprite, Color iconTint, string description, string owned,
            int price, bool canAfford)
        {
            gameObject.SetActive(true);
            icon.sprite = iconSprite;
            icon.color = iconTint;
            icon.enabled = iconSprite != null;
            nameText.text = displayName;
            ownedText.text = owned;
            descriptionText.text = description;
            priceText.text = $"${price}";
            buyButton.Text = "Buy";
            buyButton.Button.interactable = canAfford;
            soldStamp.SetActive(false);
            group.alpha = 1f;
        }

        /// <summary>Bought: emptied, faded and stamped, with the button dead. Emptied for the same reason
        /// ShopPokemonCardView.MarkSold is — a card never shown would still carry placeholder text.</summary>
        public void MarkSold()
        {
            gameObject.SetActive(true);
            icon.enabled = false;
            nameText.text = string.Empty;
            ownedText.text = string.Empty;
            descriptionText.text = string.Empty;
            priceText.text = string.Empty;
            buyButton.Text = "Sold";
            buyButton.Button.interactable = false;
            soldStamp.SetActive(true);
            group.alpha = SoldAlpha;
        }
    }
}
