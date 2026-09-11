using UnityEngine;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Location Hub (design doc §5.2): a tabbed screen (Team / Map / Shop / Center)
    /// shown while working through a Location's node-map, plus two full-screen overlays (PvE
    /// clash, Camp) that replace the tabs while a node is being resolved.</summary>
    public sealed class LocationHubController : MonoBehaviour
    {
        [SerializeField] private GameObject tabBar;
        [SerializeField] private GameObject teamPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject centerPanel;
        [SerializeField] private GameObject pveOverlay;
        [SerializeField] private GameObject campOverlay;
        [SerializeField] private GameObject runOverOverlay;
        [SerializeField] private Button[] tabButtons;

        private GameObject[] tabPanels;

        private void Awake()
        {
            tabPanels = new[] { teamPanel, mapPanel, shopPanel, centerPanel };
        }

        public void ShowTab(int index)
        {
            pveOverlay.SetActive(false);
            campOverlay.SetActive(false);
            runOverOverlay.SetActive(false);
            tabBar.SetActive(true);

            for (int i = 0; i < tabPanels.Length; i++)
            {
                tabPanels[i].SetActive(i == index);
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool selected = i == index;
                tabButtons[i].image.color = selected ? Theme.TabSelectedBg : Theme.TabUnselectedBg;
                var label = tabButtons[i].GetComponentInChildren<Text>();
                label.color = selected ? Theme.TextDark : Theme.TextLight;
            }
        }

        public void ShowPvEOverlay()
        {
            SetAllTabsAndOverlaysInactive();
            pveOverlay.SetActive(true);
        }

        public void ShowCampOverlay()
        {
            SetAllTabsAndOverlaysInactive();
            campOverlay.SetActive(true);
        }

        public void ShowRunOver()
        {
            SetAllTabsAndOverlaysInactive();
            runOverOverlay.SetActive(true);
        }

        private void SetAllTabsAndOverlaysInactive()
        {
            tabBar.SetActive(false);
            foreach (var panel in tabPanels)
            {
                panel.SetActive(false);
            }
            pveOverlay.SetActive(false);
            campOverlay.SetActive(false);
            runOverOverlay.SetActive(false);
        }
    }
}
