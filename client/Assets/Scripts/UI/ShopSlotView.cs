using System;
using Pets.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>One shop-offer slot. Instantiated at runtime by ShopScreenUI, one per ShopConfig.ShopSize.</summary>
    public sealed class ShopSlotView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text costText;
        [SerializeField] private Image background;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button freezeButton;
        [SerializeField] private Text freezeButtonLabel;

        public void Bind(CreatureDefinition creature, bool frozen, int cost, Action onBuy, Action onToggleFreeze)
        {
            gameObject.SetActive(creature != null);
            if (creature == null)
            {
                return;
            }

            nameText.text = creature.DisplayName;
            statsText.text = $"{creature.BaseAttack}/{creature.BaseHealth}";
            costText.text = $"{cost}g";
            background.color = creature.PlaceholderColor;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy());

            freezeButton.onClick.RemoveAllListeners();
            freezeButton.onClick.AddListener(() => onToggleFreeze());
            freezeButtonLabel.text = frozen ? "Unfreeze" : "Freeze";
        }
    }
}
