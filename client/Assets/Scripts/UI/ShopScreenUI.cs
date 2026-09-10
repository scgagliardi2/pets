using System.Collections.Generic;
using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// Shop-phase panel: gold/lives/round display, shop offer row, board row, reroll/fight
    /// buttons. Slot views are instantiated at runtime (count varies with ShopConfig/board size),
    /// everything else is authored once by Editor/GameSceneBuilder.cs.
    /// </summary>
    public sealed class ShopScreenUI : MonoBehaviour
    {
        [SerializeField] private RunController runController;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Text goldText;
        [SerializeField] private Text livesText;
        [SerializeField] private Text roundText;
        [SerializeField] private Transform shopSlotContainer;
        [SerializeField] private Transform boardSlotContainer;
        [SerializeField] private ShopSlotView shopSlotPrefab;
        [SerializeField] private BoardSlotView boardSlotPrefab;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button fightButton;

        private readonly List<ShopSlotView> shopSlotViews = new List<ShopSlotView>();
        private readonly List<BoardSlotView> boardSlotViews = new List<BoardSlotView>();

        private void Awake()
        {
            rerollButton.onClick.AddListener(() => runController.Reroll());
            fightButton.onClick.AddListener(() => runController.Fight());
        }

        private void OnEnable()
        {
            runController.OnStateChanged += Refresh;
        }

        private void OnDisable()
        {
            runController.OnStateChanged -= Refresh;
        }

        public void Refresh()
        {
            var state = runController.State;
            if (state == null || state.Phase != GamePhase.Shop)
            {
                shopPanel.SetActive(false);
                return;
            }

            shopPanel.SetActive(true);
            goldText.text = $"Gold: {state.Gold}";
            livesText.text = $"Lives: {state.Lives}";
            roundText.text = $"Round: {state.Round}";

            SyncViewCount(shopSlotViews, shopSlotPrefab, shopSlotContainer, state.ShopSlots.Count);
            for (int i = 0; i < state.ShopSlots.Count; i++)
            {
                int index = i;
                var slot = state.ShopSlots[i];
                int cost = slot.Offer != null ? runController.Config.BuyCost(slot.Offer.Tier) : 0;
                shopSlotViews[i].Bind(slot.Offer, slot.Frozen, cost,
                    onBuy: () => runController.Buy(index),
                    onToggleFreeze: () => runController.ToggleFreeze(index));
            }

            SyncViewCount(boardSlotViews, boardSlotPrefab, boardSlotContainer, state.Board.Count);
            for (int i = 0; i < state.Board.Count; i++)
            {
                int index = i;
                boardSlotViews[i].Bind(state.Board[i],
                    onSell: () => runController.Sell(index),
                    onMoveLeft: () => runController.MoveBoardCreature(index, -1),
                    onMoveRight: () => runController.MoveBoardCreature(index, 1));
            }
        }

        private static void SyncViewCount<T>(List<T> views, T prefab, Transform container, int count) where T : Component
        {
            while (views.Count < count)
            {
                views.Add(Instantiate(prefab, container));
            }
            for (int i = 0; i < views.Count; i++)
            {
                views[i].gameObject.SetActive(i < count);
            }
        }
    }
}
