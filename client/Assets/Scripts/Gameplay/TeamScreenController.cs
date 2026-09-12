using UnityEngine;
using UnityEngine.UI;

namespace Pets.Gameplay
{
    /// <summary>The Team scene: view the run's current line-up and Box, and reorder the two active
    /// slots. Reads the run from ActiveRun rather than from a scene RunBootstrapper, since this is
    /// a separate scene reached from the Ingame Menu — the run it's showing was created back on
    /// the Map.
    ///
    /// Opening Team.unity on its own (no run in progress) is a normal thing to do while building
    /// the scene, so that case shows an explanation instead of throwing on a null RunState.</summary>
    public sealed class TeamScreenController : MonoBehaviour
    {
        [SerializeField] private TeamPanelController teamPanel;
        [SerializeField] private Button swapButton;
        [SerializeField] private Text emptyStateText;

        private void Start()
        {
            Refresh();
        }

        public void OnSwapLeadAndSupportClicked()
        {
            if (!ActiveRun.HasRun)
            {
                return;
            }
            ActiveRun.State.SwapLeadAndSupport();
            Refresh();
        }

        private void Refresh()
        {
            bool hasRun = ActiveRun.HasRun;
            teamPanel.gameObject.SetActive(hasRun);
            emptyStateText.gameObject.SetActive(!hasRun);
            swapButton.interactable = hasRun && ActiveRun.State.LineUp.Count >= 2;

            if (hasRun)
            {
                teamPanel.Refresh(ActiveRun.State, ActiveRun.Library);
            }
        }
    }
}
