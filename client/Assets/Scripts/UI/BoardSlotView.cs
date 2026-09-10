using System;
using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>One board-creature slot. Instantiated at runtime by ShopScreenUI, one per creature owned.</summary>
    public sealed class BoardSlotView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Image background;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button moveLeftButton;
        [SerializeField] private Button moveRightButton;

        public void Bind(BoardCreature creature, Action onSell, Action onMoveLeft, Action onMoveRight)
        {
            var definition = creature.Definition;
            nameText.text = $"{definition.DisplayName} Lv{creature.Level}";

            int attack = definition.BaseAttack + (creature.Level - 1) * definition.LevelAttackBonus + creature.BonusAttack;
            int health = definition.BaseHealth + (creature.Level - 1) * definition.LevelHealthBonus + creature.BonusHealth;
            statsText.text = $"{attack}/{health}";
            background.color = definition.PlaceholderColor;

            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() => onSell());
            moveLeftButton.onClick.RemoveAllListeners();
            moveLeftButton.onClick.AddListener(() => onMoveLeft());
            moveRightButton.onClick.RemoveAllListeners();
            moveRightButton.onClick.AddListener(() => onMoveRight());
        }
    }
}
