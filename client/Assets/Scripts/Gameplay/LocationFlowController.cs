using UnityEngine;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Coordinates the Forest Location Hub (design doc §5.2): owns transitions between
    /// the tabbed hub and the PvE/Camp overlays, and advances the node-map on a resolved node.</summary>
    public sealed class LocationFlowController : MonoBehaviour
    {
        private const int MapTabIndex = 1;

        [SerializeField] private LocationHubController hub;
        [SerializeField] private MapPanelController mapPanel;
        [SerializeField] private TeamPanelController teamPanel;
        [SerializeField] private PvEClashController pveController;
        [SerializeField] private CampPanelController campController;
        [SerializeField] private ResourceBarController resourceBar;

        private RunState state;

        private void Start()
        {
            state = RunBootstrapper.Instance.State;
            RefreshHubPanels();
            hub.ShowTab(MapTabIndex);
        }

        private void RefreshHubPanels()
        {
            mapPanel.Refresh(state);
            teamPanel.Refresh(state, RunBootstrapper.Instance.SpeciesLibrary);
            resourceBar.Refresh(state);
        }

        public void OnGoClicked()
        {
            var node = state.CurrentNode;
            if (node.Type == NodeType.PvE)
            {
                hub.ShowPvEOverlay();
                pveController.Begin(state, RunBootstrapper.Instance.SpeciesLibrary);
            }
            else
            {
                hub.ShowCampOverlay();
                campController.Begin(state);
            }
        }

        /// <summary>A lost PvE fight doesn't clear the node — Morale already took the hit, and the
        /// player retries the same encounter rather than being waved past it.</summary>
        public void OnPvEResolved(bool won)
        {
            if (won)
            {
                state.AdvanceToNextNode();
            }
            ReturnToHubOrEndRun();
        }

        public void OnCampResolved()
        {
            state.AdvanceToNextNode();
            ReturnToHubOrEndRun();
        }

        private void ReturnToHubOrEndRun()
        {
            if (state.IsRunOver)
            {
                hub.ShowRunOver();
                return;
            }
            RefreshHubPanels();
            hub.ShowTab(MapTabIndex);
        }
    }
}
