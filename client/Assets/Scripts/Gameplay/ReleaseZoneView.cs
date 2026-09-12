using UnityEngine;
using UnityEngine.EventSystems;

namespace Pets.Gameplay
{
    /// <summary>The Team screen's release target: drop a card here to let that mon go. A drop
    /// zone rather than a button on every card, because the screen already moves mons by dragging
    /// them — releasing is then "drag it off the team" rather than a separate mechanic to learn,
    /// and twelve little buttons next to twelve draggable cards would mostly generate misclicks
    /// on an irreversible action.
    ///
    /// Reports the drop and nothing else: TeamPanelController decides what it means, and the
    /// screen asks the player to confirm before anything is actually let go.</summary>
    public sealed class ReleaseZoneView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private TeamPanelController panel;

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<TeamSlotView>()
                : null;
            if (source != null && source.IsFilled)
            {
                panel.ReleaseFromSlot(source);
            }
        }
    }
}
