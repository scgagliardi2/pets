using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>The Team scene: view the run's current line-up and Box, and rearrange either.
    /// Reordering itself is the panel's job — a mon is moved by dragging its card onto another
    /// slot — so what's left here is the screen around it: the no-run empty state, the
    /// Lead/Support button as a one-click shortcut for the most common swap, the confirmation a
    /// release has to pass through, and keeping all of it in step with whatever a drag just did.
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

        [Header("Release confirmation")]
        [SerializeField] private GameObject releaseConfirmPanel;
        [SerializeField] private Text releaseConfirmText;
        [SerializeField] private Button releaseConfirmButton;

        // The slot the open confirmation is about. Safe to hold as an index rather than the mon
        // itself because the confirmation is modal — its backdrop takes the raycast, so nothing
        // can reorder the rows underneath while it's up.
        private RosterGroup pendingGroup;
        private int pendingIndex = -1;

        private void Start()
        {
            // A drag can change how many mons are in the line-up, which is what the Swap button's
            // enabled state is derived from. Only the button needs updating — the panel has
            // already redrawn its own rows by the time it raises this.
            teamPanel.Changed += UpdateSwapButton;
            teamPanel.ReleaseRequested += OnReleaseRequested;
            releaseConfirmPanel.SetActive(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (teamPanel != null)
            {
                teamPanel.Changed -= UpdateSwapButton;
                teamPanel.ReleaseRequested -= OnReleaseRequested;
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

        /// <summary>Opens the confirmation for a card dropped on the release zone. A release that
        /// the rules won't allow still opens it, saying why, with the Release button dead: the
        /// player gets an answer to "why did nothing happen" in the same place they asked the
        /// question.</summary>
        private void OnReleaseRequested(RosterGroup group, int index, string displayName)
        {
            if (!ActiveRun.HasRun)
            {
                return;
            }

            pendingGroup = group;
            pendingIndex = index;

            bool allowed = ActiveRun.State.CanReleaseMon(group, index);
            releaseConfirmText.text = allowed
                ? $"Release {displayName}?\nThis cannot be undone."
                : $"{displayName} is the last mon in your party.\nYou can't be left with none.";
            releaseConfirmButton.interactable = allowed;
            releaseConfirmPanel.SetActive(true);
        }

        public void OnConfirmReleaseClicked()
        {
            if (ActiveRun.HasRun && pendingIndex >= 0)
            {
                ActiveRun.State.ReleaseMon(pendingGroup, pendingIndex);
            }
            CloseReleaseConfirm();
            Refresh();
        }

        public void OnCancelReleaseClicked() => CloseReleaseConfirm();

        private void CloseReleaseConfirm()
        {
            pendingIndex = -1;
            releaseConfirmPanel.SetActive(false);
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
