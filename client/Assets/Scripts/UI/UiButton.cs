using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable sprite-backed button widget — see Assets/Prefabs/UI/Button.prefab (built
    /// by Pets.EditorTools.UiPrefabBuilder). Wraps the 9-sliced Button art (Theme.ButtonSprite) so
    /// every place that wants the "Monster Trails" look drags/instantiates one component instead
    /// of re-deriving the Image/Button/Text setup by hand. Style is editable directly in the
    /// Inspector (or the prefab itself) and re-applies live via OnValidate, so sprite/tint/size
    /// tweaks don't require touching code or rebuilding a scene to preview.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButton : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private Theme.ButtonStyle style = Theme.ButtonStyle.Primary;

        public Button Button => GetComponent<Button>();
        public Text Label => label;

        public string Text
        {
            get => label.text;
            set => label.text = value;
        }

        public Theme.ButtonStyle Style
        {
            get => style;
            set
            {
                style = value;
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (background == null || label == null)
            {
                return;
            }
            background.sprite = Theme.ButtonSprite(style);
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            label.color = Theme.TextLight;
        }
    }
}
