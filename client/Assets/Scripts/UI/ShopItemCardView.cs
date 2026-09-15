using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>One supply on the Pokémon Center's shelf — a Poké Ball or an item. See
    /// Assets/Prefabs/UI/ShopItemCard.prefab (built by Pets.EditorTools.ShopPrefabBuilder): an icon
    /// on a display pad, the name with how many the run already owns beside it, a description, and a
    /// gold price tag next to a nested Button prefab.</summary>
    public sealed class ShopItemCardView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(220f, 214f);

        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text ownedText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text priceText;
        [SerializeField] private UiButton buyButton;

        public Text NameText => nameText;
        public Text OwnedText => ownedText;
        public Text PriceText => priceText;
        public Button BuyButton => buyButton.Button;

        /// <param name="iconTint">White for an item's own art; a ball tier's colour for the shared
        /// Poké Ball sprite.</param>
        public void Show(string displayName, Sprite iconSprite, Color iconTint, string description, string owned,
            int price, bool canAfford)
        {
            icon.sprite = iconSprite;
            icon.color = iconTint;
            icon.enabled = iconSprite != null;
            nameText.text = displayName;
            ownedText.text = owned;
            descriptionText.text = description;
            priceText.text = $"${price}";
            buyButton.Text = "Buy";
            buyButton.Button.interactable = canAfford;
        }
    }
}
