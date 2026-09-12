using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>One slot on the Team screen — which collection and index it stands for, and the
    /// drag/drop plumbing that lets a player pick its mon up and drop it on another slot.
    /// Attached to the slot object by TeamPanelController, which owns what a completed drag
    /// actually does; this type only reports the gesture.
    ///
    /// The handlers live on the slot rather than on the card inside it because uGUI walks up the
    /// hierarchy looking for a handler: the card carries the Image that gets raycast, and the
    /// event finds this on its way up. That also means an empty slot is still a drop target, which
    /// is the point — dropping onto one is how a mon moves between the party and the Box.</summary>
    public sealed class TeamSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private TeamPanelController panel;

        public RosterGroup Group { get; private set; }

        public int Index { get; private set; }

        /// <summary>False for a slot drawn as empty — those can be dropped onto but not dragged
        /// from.</summary>
        public bool IsFilled { get; private set; }

        /// <summary>The card inside the slot, which is what actually follows the pointer.</summary>
        public RectTransform Card { get; private set; }

        public void Initialize(TeamPanelController owner, RosterGroup group, int index, bool isFilled, RectTransform card)
        {
            panel = owner;
            Group = group;
            Index = index;
            IsFilled = isFilled;
            Card = card;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsFilled)
            {
                panel.BeginSlotDrag(this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsFilled)
            {
                panel.DragSlot(this, eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            panel.EndSlotDrag();
        }

        /// <summary>Fires on the slot under the pointer when the drag is released, before
        /// OnEndDrag. The mon being dragged belongs to whichever slot started the drag, which uGUI
        /// hands back as the event's pointerDrag object.</summary>
        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<TeamSlotView>()
                : null;
            if (source != null && source != this && source.IsFilled)
            {
                panel.MoveBetweenSlots(source, this);
            }
        }
    }
}
