using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable HP bar widget — see Assets/Prefabs/UI/HealthBar.prefab (built by
    /// Pets.EditorTools.UiPrefabBuilder): an "HP" pill, a recessed track with a fill that shrinks and
    /// turns green → yellow → red as health drops (Theme.HealthFillSprite), and a "current/max"
    /// readout. Character Select cards show it today; it's shaped for the battle screen's Lead and
    /// Support panels later, which is why it takes a current value at all. The fill is sized the
    /// same way as StatBarView's (see StatBarView.SizeFill). Same Inspector-live pattern as
    /// TypeIconView.</summary>
    [ExecuteAlways]
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text valueLabel;
        [SerializeField] private int current = 1;
        [SerializeField] private int max = 1;

        public Image Fill => fill;
        public Text ValueLabel => valueLabel;
        public int Current => current;
        public int Max => max;

        /// <summary>0..1; overheal clamps to a full bar and a non-positive max reads as empty.</summary>
        public float Fraction => max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        public void SetHealth(int current, int max)
        {
            this.current = current;
            this.max = max;
            Apply();
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (fill == null || valueLabel == null)
            {
                return;
            }

            float fraction = Fraction;
            StatBarView.SizeFill(fill, fraction);
            fill.sprite = Theme.HealthFillSprite(fraction);

            valueLabel.text = $"{Mathf.Max(current, 0)}/{max}";
        }
    }
}
