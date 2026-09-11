using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable sprite-backed text-box widget — see Assets/Prefabs/UI/TextBox.prefab
    /// (built by Pets.EditorTools.UiPrefabBuilder). Non-interactive counterpart to UiButton,
    /// wrapping the 9-sliced TextBox art (Theme.TextBoxSprite) for labels/readouts that want the
    /// bordered parchment look instead of a flat panel background.</summary>
    [ExecuteAlways]
    public sealed class UiTextBox : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text label;

        public Text TextComponent => label;

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set
            {
                if (label != null)
                {
                    label.text = value;
                }
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (background == null)
            {
                return;
            }

            background.sprite = Theme.TextBoxSprite;
            background.type = Image.Type.Sliced;
            background.fillCenter = true;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = Color.white;
            // Nothing here is clickable, and leaving it as a raycast target would have this box
            // silently block whatever sits behind it.
            background.raycastTarget = false;

            if (label != null)
            {
                label.color = Theme.TextDark;
                label.raycastTarget = false;
            }
        }
    }
}
