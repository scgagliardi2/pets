using UnityEngine;
using UnityEngine.UI;

namespace Pets.Gameplay
{
    /// <summary>The Team scene: view the run's current line-up and Box, and rearrange either.
    /// Reordering itself is the panel's job — a mon is moved by dragging its card onto another
    /// slot — so what's left here is the screen around it: the no-run empty state, the
    /// Lead/Support button as a one-click shortcut for the most common swap, and keeping both in
    /// step with whatever a drag just did.
    ///
    /// Reads the run from ActiveRun rather than from a scene RunBootstrapper, since this is a
    /// separate scene reached from the Ingame Menu — the run it's showing was created back on the
    /// Map.
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
            // A drag can change how many mons are in the line-up, which is what the Swap button's
            // enabled state is derived from. Only the button needs updating — the panel has
            // already redrawn its own rows by the time it raises this.
            teamPanel.Changed += UpdateSwapButton;
            Refresh();
        }

        private void OnDestroy()
        {
            if (teamPanel != null)
            {
                teamPanel.Changed -= UpdateSwapButton;
            }
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
            UpdateSwapButton();

            if (hasRun)
            {
                teamPanel.Refresh(ActiveRun.State, ActiveRun.Library);
            }
        }

        private void UpdateSwapButton()
        {
            swapButton.interactable = ActiveRun.HasRun && ActiveRun.State.LineUp.Count >= 2;
        }
    }
}
