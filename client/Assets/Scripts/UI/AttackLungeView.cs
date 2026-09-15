using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>The attack: a Lead shoves toward its opponent and comes back, once per Step, so the
    /// exchange the simulator resolved instantly is something you can watch happen.
    ///
    /// Sits on a field sprite's own GameObject beside its <see cref="Image"/>, the same arrangement
    /// as <see cref="FaintAnimationView"/> next to it, and captures its resting position the same
    /// lazy way and for the same reason: the scene builder places these sprites by absolute rect,
    /// and reading the position too early would record a zero and fling the sprite across the board
    /// on its first lunge.
    ///
    /// Both sides lunge at once rather than taking turns. That's not a shortcut — the combat model
    /// is a simultaneous exchange (battle-sim-spec.md §3.1: both Leads deal damage before either
    /// side's reactions resolve), so alternating attacks would be animating something the rules
    /// don't do.
    ///
    /// **It shares `anchoredPosition` with the faint**, which is why the battle screen plays a lunge
    /// to completion before the faint beat begins. Two things writing the same field in the same
    /// frame is the one way this breaks, and sequencing is what prevents it — see
    /// BattleScreenController.PlayStep.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class AttackLungeView : MonoBehaviour
    {
        /// <summary>How far the sprite shoves, in canvas units. Short on purpose: the mons stand a
        /// long way apart, and a lunge that actually crossed the gap would read as a charge rather
        /// than a strike.</summary>
        [SerializeField] private float lungeDistance = 42f;

        /// <summary>Where in the lunge the sprite is furthest forward, as a fraction of the whole.
        /// Before the midpoint, so it snaps out and drifts back — which reads as a strike. Peaking
        /// at the midpoint reads as a sway, and after it as being pulled.</summary>
        [SerializeField, Range(0.1f, 0.9f)] private float strikeAt = 0.35f;

        private RectTransform rect;
        private ValueTween tween;
        private Vector2 restingPosition;
        private bool hasRestingPosition;

        /// <summary>The direction this slot attacks in — toward the opposing side. Set by the battle
        /// screen, because which way is "at the enemy" is a property of the slot, not of the mon
        /// standing in it.</summary>
        public Vector2 Direction { get; set; } = Vector2.right;

        public bool IsAnimating => tween.IsRunning;

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        /// <summary>Shoves and returns over <paramref name="seconds"/>. Called again mid-lunge it
        /// restarts, which is right here and wrong for a faint: a Step redraws more than once, and a
        /// second attack really is a second attack, where a second faint is the same faint.</summary>
        public void Play(float seconds)
        {
            CaptureRestingPosition();
            tween = new ValueTween(0f, 1f, seconds);
            Apply();
        }

        /// <summary>Puts the sprite back where it stands. For the mon promoted into this slot, and
        /// for the first draw of a fight.</summary>
        public void Clear()
        {
            CaptureRestingPosition();
            tween = default;
            rect.anchoredPosition = restingPosition;
        }

        /// <summary>Steps a running lunge; Update calls this with the frame time.</summary>
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

        private void Apply()
        {
            float t = tween.IsRunning ? tween.Value : 1f;

            // Out fast and eased to a stop, then back. Splitting the curve at strikeAt rather than
            // using one symmetric wave is what puts the weight at the front of the movement.
            float amount;
            if (t < strikeAt)
            {
                float x = t / strikeAt;
                amount = 1f - (1f - x) * (1f - x);
            }
            else
            {
                float x = (t - strikeAt) / (1f - strikeAt);
                amount = 1f - x * x;
            }

            rect.anchoredPosition = restingPosition + Direction.normalized * (lungeDistance * amount);
        }

        private void CaptureRestingPosition()
        {
            if (hasRestingPosition)
            {
                return;
            }
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }
            restingPosition = rect.anchoredPosition;
            hasRestingPosition = true;
        }
    }
}
