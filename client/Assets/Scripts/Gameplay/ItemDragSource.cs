using UnityEngine;
using UnityEngine.EventSystems;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Something on the Team screen that carries an item and can be dragged: a chip in the
    /// bag, or the badge on a card showing what that mon holds. Reports the gesture to
    /// TeamPanelController, which decides what the drop means — onto a mon is equip (or hand over),
    /// onto the bag is take it off.
    ///
    /// A badge sits inside a card inside a TeamSlotView, and uGUI gives the drag to the nearest
    /// handler up the hierarchy from what was pressed — so pressing the badge drags the item, and
    /// pressing anywhere else on the card still drags the mon.</summary>
    public sealed class ItemDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private TeamPanelController panel;

        public string ItemId { get; private set; }

        /// <summary>True for a chip in the bag; false for the badge on a mon's card, which
        /// <see cref="Group"/>/<see cref="Index"/> then locate.</summary>
        public bool FromBag { get; private set; }

        public RosterGroup Group { get; private set; }
        public int Index { get; private set; }
        public Sprite Icon { get; private set; }

        public void InitializeInBag(TeamPanelController owner, string itemId, Sprite icon)
        {
            panel = owner;
            ItemId = itemId;
            Icon = icon;
            FromBag = true;
        }

        public void InitializeOnMon(TeamPanelController owner, string itemId, Sprite icon, RosterGroup group, int index)
        {
            panel = owner;
            ItemId = itemId;
            Icon = icon;
            FromBag = false;
            Group = group;
            Index = index;
        }

        public void OnBeginDrag(PointerEventData eventData) => panel.BeginItemDrag(this, eventData);

        public void OnDrag(PointerEventData eventData) => panel.DragItem(eventData);

        public void OnEndDrag(PointerEventData eventData) => panel.EndItemDrag();
    }
}
