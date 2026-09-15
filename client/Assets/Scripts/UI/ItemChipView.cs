using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>An item in the Team screen's bag: its icon and name on a dark slot. See
    /// Assets/Prefabs/UI/ItemChip.prefab (built by Pets.EditorTools.ShopPrefabBuilder). Only a view —
    /// dragging it onto a mon is Pets.Gameplay.ItemDragSource's job.</summary>
    public sealed class ItemChipView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(190f, 48f);

        [SerializeField] private Image icon;
        [SerializeField] private Text label;

        public Image Icon => icon;
        public Text Label => label;

        public void Show(Sprite iconSprite, string text)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            label.text = text;
        }
    }
}
