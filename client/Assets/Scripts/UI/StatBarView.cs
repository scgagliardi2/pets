using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>A stat shown as a bar against a fixed ceiling, with the raw value beside it — see
    /// Assets/Prefabs/UI/SpeedBar.prefab (built by Pets.EditorTools.UiPrefabBuilder), which shares
    /// HealthBar.prefab's pill/track/readout layout. Unlike HealthBarView there's no "current of
    /// max": the max is a scale (e.g. PokemonSpeciesDefinitionAsset.MaxBaseSpeed), so the readout
    /// shows only the value and the fill keeps one colour.</summary>
    [ExecuteAlways]
    public sealed class StatBarView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text valueLabel;
        [SerializeField] private int value = 1;
        [SerializeField] private int max = 1;

        public Image Fill => fill;
        public Text ValueLabel => valueLabel;
        public int Value => value;
        public int Max => max;

        /// <summary>0..1; a value above the ceiling pins a full bar and a non-positive max reads as empty.</summary>
        public float Fraction => max > 0 ? Mathf.Clamp01((float)value / max) : 0f;

        public void SetValue(int value, int max)
        {
            this.value = value;
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

            SizeFill(fill, Fraction);
            valueLabel.text = Mathf.Max(value, 0).ToString();
        }

        /// <summary>Shared with HealthBarView. Sizes the fill by moving its right anchor rather than
        /// with Image.Type.Filled: Filled crops the sprite, which would cut the chamfered right end
        /// off a 9-sliced bar, while an anchor keeps both ends intact at any width.</summary>
        internal static void SizeFill(Image fill, float fraction)
        {
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(fraction, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // An empty bar is just the track — a zero-width sliced sprite still draws its borders.
            fill.enabled = fraction > 0f;
        }
    }
}
