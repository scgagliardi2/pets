using System.Collections.Generic;
using Pets.Gameplay;
using UnityEngine;

namespace Pets.UI
{
    /// <summary>
    /// Static (non-animated) display of both teams' lineups going into a battle — shown inside
    /// BattleResultPanel alongside the outcome/log. Cards are just colored rectangles
    /// (CreatureDefinition.PlaceholderColor), not sprites — placeholder-art phase, PLAN.md §8.
    /// </summary>
    public sealed class FightLineupUI : MonoBehaviour
    {
        [SerializeField] private Transform playerRowContainer;
        [SerializeField] private Transform enemyRowContainer;
        [SerializeField] private FightCardView cardPrefab;

        private readonly List<FightCardView> playerCards = new List<FightCardView>();
        private readonly List<FightCardView> enemyCards = new List<FightCardView>();

        public void Show(List<FightCardInfo> playerTeam, List<FightCardInfo> enemyTeam)
        {
            SyncViewCount(playerCards, playerRowContainer, playerTeam.Count);
            for (int i = 0; i < playerTeam.Count; i++)
            {
                playerCards[i].Bind(playerTeam[i]);
            }

            SyncViewCount(enemyCards, enemyRowContainer, enemyTeam.Count);
            for (int i = 0; i < enemyTeam.Count; i++)
            {
                enemyCards[i].Bind(enemyTeam[i]);
            }
        }

        private void SyncViewCount(List<FightCardView> views, Transform container, int count)
        {
            while (views.Count < count)
            {
                views.Add(Instantiate(cardPrefab, container));
            }
            for (int i = 0; i < views.Count; i++)
            {
                views[i].gameObject.SetActive(i < count);
            }
        }
    }
}
