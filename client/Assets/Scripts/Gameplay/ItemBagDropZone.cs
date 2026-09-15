using UnityEngine;
using UnityEngine.EventSystems;

namespace Pets.Gameplay
{
    /// <summary>The Team screen's bag row as a drop target: dropping a mon's held item here takes it
    /// off. Reports the drop only — TeamPanelController applies it.</summary>
    public sealed class ItemBagDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private TeamPanelController panel;

        public void OnDrop(PointerEventData eventData)
        {
            var item = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemDragSource>() : null;
            if (item != null)
            {
                panel.DropItemInBag(item);
            }
        }
    }
}
