using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Reusable HP bar widget — see Assets/Prefabs/UI/HealthBar.prefab (built by
    /// Pets.EditorTools.UiPrefabBuilder): an "HP" pill, a recessed track with a fill that shrinks and
    /// turns green → yellow → red as health drops (Theme.HealthFillSprite), and a "current/max"
    /// readout. Used on Character Select's cards and the battle screen's stat boxes. The fill is sized
    /// the same way as StatBarView's (see StatBarView.SizeFill). Same Inspector-live pattern as
    /// TypeIconView.
    ///
    /// <see cref="SetHealth"/> jumps straight to a value; <see cref="AnimateHealth"/> drains (or
    /// refills) the fill and counts the readout toward it over time, which is how the battle screen
    /// shows a hit landing. <see cref="Current"/> is always the value being moved *to* — the number
    /// on screen during a drain is <see cref="DisplayedHealth"/>.</summary>
    [ExecuteAlways]
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text valueLabel;
        [SerializeField] private int current = 1;
        [SerializeField] private int max = 1;

        private ValueTween tween;

        public Image Fill => fill;
        public Text ValueLabel => valueLabel;
        public int Current => current;
        public int Max => max;

        /// <summary>0..1 of the target value; overheal clamps to a full bar and a non-positive max
        /// reads as empty.</summary>
        public float Fraction => FractionOf(current);

        public bool IsAnimating => tween.IsRunning;

        public int DisplayedHealth => Mathf.RoundToInt(Shown);

        private float Shown => tween.IsRunning ? tween.Value : current;

        public void SetHealth(int current, int max)
        {
            this.current = current;
            this.max = max;
            tween = default;
            Apply();
        }

        /// <summary>Moves the bar from whatever it's showing now — mid-drain included — to
        /// <paramref name="current"/> over <paramref name="seconds"/>, linearly.</summary>
        public void AnimateHealth(int current, int max, float seconds)
        {
            float from = Shown;
            this.current = current;
            this.max = max;
            tween = new ValueTween(from, current, seconds);
            Apply();
        }

        /// <summary>Steps a running animation; Update calls this with the frame time.</summary>
        public void Advance(float seconds)
        {
            if (!tween.IsRunning)
            {
                return;
            }
            tween.Advance(seconds);
            Apply();
        }

        private void Update()
        {
            if (tween.IsRunning)
            {
                Advance(Time.deltaTime);
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private float FractionOf(float value) => max > 0 ? Mathf.Clamp01(value / max) : 0f;

        private void Apply()
        {
            if (fill == null || valueLabel == null)
            {
                return;
            }

            float fraction = FractionOf(Shown);
            StatBarView.SizeFill(fill, fraction);
            fill.sprite = Theme.HealthFillSprite(fraction);

            valueLabel.text = $"{Mathf.Max(DisplayedHealth, 0)}/{max}";
        }
    }
}
