using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable sprite-backed text-box widget — see Assets/Prefabs/UI/TextBox.prefab
    /// (built by Pets.EditorTools.UiPrefabBuilder). Non-interactive counterpart to UiButton,
    /// wrapping the 9-sliced TextBox art (Theme.TextBoxSprite) for labels/readouts that want the
    /// bordered-box look instead of a flat panel background.</summary>
    public sealed class UiTextBox : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text label;

        public Text TextComponent => label;

        public string Text
        {
            get => label.text;
            set => label.text = value;
        }

        private void Awake() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (background == null)
            {
                return;
            }
            background.sprite = Theme.TextBoxSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
    }
}
