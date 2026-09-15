using UnityEngine;
using UnityEngine.UI;
using Pets.Simulation;

namespace Pets.UI
{
    /// <summary>One Pokémon up for adoption at the Pokémon Center. See
    /// Assets/Prefabs/UI/ShopPokemonCard.prefab (built by Pets.EditorTools.ShopPrefabBuilder): the
    /// sprite on a display pad, the battle screen's own BattleStatsBox prefab nested beside it — so a
    /// mon on the shelf reads exactly like one in a fight — then its tier and growth, a gold price
    /// tag and an Adopt button. Once adopted the card stays on the shelf, faded, stamped Adopted.</summary>
    public sealed class ShopPokemonCardView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(720f, 136f);

        private const float SoldAlpha = 0.55f;

        [SerializeField] private Image portrait;
        [SerializeField] private BattleStatsBoxView statsBox;
        [SerializeField] private Text infoText;
        [SerializeField] private Text priceText;
        [SerializeField] private UiButton buyButton;
        [SerializeField] private GameObject soldStamp;
        [SerializeField] private CanvasGroup group;

        public BattleStatsBoxView StatsBox => statsBox;
        public Text InfoText => infoText;
        public Text PriceText => priceText;
        public Button BuyButton => buyButton.Button;
        public bool IsSold => soldStamp.activeSelf;

        public void Show(Sprite sprite, string displayName, PokemonType type1, bool hasSecondType, PokemonType type2,
            Stats stats, int speedMax, string info, int price, bool canAfford)
        {
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            statsBox.Show(displayName, type1, hasSecondType, type2, stats.Attack, stats.Speed, speedMax);
            statsBox.HealthBar.SetHealth(stats.Health, stats.Health);
            infoText.text = info;
            priceText.text = $"${price}";
            buyButton.Text = "Adopt";
            buyButton.Button.interactable = canAfford;
            soldStamp.SetActive(false);
            group.alpha = 1f;
        }

        /// <summary>The mon has gone home with the player: the card stays on the shelf, emptied and
        /// faded, with the stamp over it and the button dead. Emptied rather than left as it was,
        /// because a card that was never shown (the scene opened after the adoption) would otherwise
        /// still carry the prefab's placeholder stats.</summary>
        public void MarkSold()
        {
            portrait.enabled = false;
            statsBox.SetEmpty(string.Empty);
            infoText.text = string.Empty;
            buyButton.Text = "Adopted";
            buyButton.Button.interactable = false;
            soldStamp.SetActive(true);
            group.alpha = SoldAlpha;
        }

        /// <summary>No mon on this shelf spot at all (a roster too small to fill it).</summary>
        public void ShowEmpty()
        {
            gameObject.SetActive(false);
        }
    }
}
