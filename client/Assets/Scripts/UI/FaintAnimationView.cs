using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>The faint: a mon that has run out of HP sinks out of the field and fades as it
    /// goes, the way the mainline games drop a fainted Pokémon off the bottom of the screen.
    ///
    /// Sits on a field sprite's own GameObject beside its <see cref="Image"/> (see
    /// BattleSceneBuilder.CreateFieldSprite), so the thing that animates and the thing that draws
    /// are one object and the battle screen doesn't have to hold a parallel table of positions.
    /// The resting position is captured the first time it's needed rather than in Awake: the scene
    /// builder places these sprites by absolute rect and a prefab-instantiated one is laid out a
    /// frame later, so reading it too early would record a zero and every faint would snap the
    /// sprite to the middle of the board.
    ///
    /// Driven from Update off <see cref="ValueTween"/>, the same way HealthBarView drains a bar —
    /// one pattern for "a widget animating itself", and a tween a test can advance by hand.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class FaintAnimationView : MonoBehaviour
    {
        /// <summary>How far the sprite sinks, in canvas units. Roughly a sprite's height, so it
        /// clears the field rather than stopping halfway with its head showing.</summary>
        [SerializeField] private float dropDistance = 110f;

        /// <summary>Alpha at the end of the drop. Zero — a fainted mon is gone, and anything above
        /// zero leaves a ghost sitting on the field behind whoever was promoted into its place.</summary>
        [SerializeField] private float endAlpha = 0f;

        private Image image;
        private RectTransform rect;
        private ValueTween tween;
        private Vector2 restingPosition;
        private bool hasRestingPosition;

        /// <summary>True from the moment <see cref="Play"/> is called until the drop finishes —
        /// what a caller waits on, and what stops a redraw mid-faint from resetting the sprite.</summary>
        public bool IsAnimating => tween.IsRunning;

        /// <summary>True once a faint has been played and not yet cleared, animation finished or
        /// not, so a redraw can tell "this mon is down" from "this mon is standing".</summary>
        public bool IsFainted { get; private set; }

        private void Awake()
        {
            image = GetComponent<Image>();
            rect = (RectTransform)transform;
        }

        /// <summary>Drops and fades the sprite over <paramref name="seconds"/>. Calling it again on
        /// an already-fainted mon is a no-op rather than a restart — a Step is redrawn more than
        /// once (the drain beat, then the faint beat), and a faint that restarted each time would
        /// never finish.</summary>
        public void Play(float seconds)
        {
            CaptureRestingPosition();
            if (IsFainted)
            {
                return;
            }

            IsFainted = true;
            tween = new ValueTween(0f, 1f, seconds);
            Apply();
        }

        /// <summary>Puts the sprite back on its feet at full opacity — for the mon promoted into
        /// this slot, and for the first draw of a fight.</summary>
        public void Clear()
        {
            CaptureRestingPosition();
            IsFainted = false;
            tween = default;
            rect.anchoredPosition = restingPosition;
            SetAlpha(1f);
        }

        /// <summary>Steps a running drop; Update calls this with the frame time.</summary>
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
            // Eased in: a faint that starts slowly and accelerates downward reads as falling, where
            // a linear slide reads as the sprite being dragged off.
            float t = tween.IsRunning ? tween.Value : 1f;
            float eased = t * t;
            rect.anchoredPosition = restingPosition + new Vector2(0f, -dropDistance * eased);
            SetAlpha(Mathf.Lerp(1f, endAlpha, eased));
        }

        private void SetAlpha(float alpha)
        {
            if (image == null)
            {
                return;
            }
            var color = image.color;
            color.a = alpha;
            image.color = color;
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
            if (image == null)
            {
                image = GetComponent<Image>();
            }
            restingPosition = rect.anchoredPosition;
            hasRestingPosition = true;
        }
    }
}
