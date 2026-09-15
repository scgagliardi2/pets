using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>The evolution scene, played over the battle screen once a won fight's EXP has been
    /// paid and something has crossed its twelfth point (ExperienceResolver.ExpPerEvolution).
    ///
    /// One mon at a time, in the mainline shape: the old form stands alone, then the silhouette
    /// flickers between the two species faster and faster, then a white flash and the new form is
    /// there. It's the one moment in a run where a line-up visibly changes shape, and before this
    /// existed it was a line of text on a result panel.
    ///
    /// Every evolution in the report is shown before the panel behind it appears — see
    /// BattleScreenController.ShowResult, which hands the queue over and waits on
    /// <see cref="OnComplete"/>. That callback is assigned at runtime by the host rather than
    /// serialized, for the same reason NodeEventOverlayController's is: the host is a scene-only object
    /// this prefab shouldn't know about.
    ///
    /// The whole thing is skippable with a click anywhere — it's an animation, not a decision, and
    /// a player who has seen it fifty times shouldn't have to sit through the fifty-first.</summary>
    public sealed class EvolutionOverlayController : MonoBehaviour
    {
        [SerializeField] private Image sprite;

        /// <summary>The white plate that covers the moment of transformation. A separate full-screen
        /// Image rather than a tint on <see cref="sprite"/>, because a uGUI tint multiplies and so
        /// can only ever darken a sprite — there is no way to whiten one out without a shader — and
        /// because the flash has to hide the swap between the two forms completely.</summary>
        [SerializeField] private Image flash;

        [SerializeField] private Text captionText;

        [Header("Pacing")]
        /// <summary>How long the old form is held before the flicker starts.</summary>
        [SerializeField] private float introSeconds = 0.9f;

        /// <summary>How long the flicker runs before the reveal.</summary>
        [SerializeField] private float flickerSeconds = 2.2f;

        /// <summary>Seconds a single flicker beat lasts at the start; it shortens toward
        /// <see cref="fastestFlickerBeat"/> as the flicker goes on, which is what makes the
        /// transformation feel like it's building rather than just strobing.</summary>
        [SerializeField] private float slowestFlickerBeat = 0.22f;
        [SerializeField] private float fastestFlickerBeat = 0.05f;

        /// <summary>How long the new form is held, with its caption, before the next evolution or
        /// the result panel.</summary>
        [SerializeField] private float revealSeconds = 1.6f;

        /// <summary>How long the white flash takes to fade off the new form.</summary>
        [SerializeField] private float flashFadeSeconds = 0.5f;

        /// <summary>Invoked once every evolution in the queue has played, or the moment the player
        /// skips. Always invoked exactly once per <see cref="Play"/>.</summary>
        public Action OnComplete;

        private Coroutine routine;
        private bool finished;

        /// <summary>True while the sequence is on screen — what a test waits out, and what stops the
        /// battle screen showing its result panel underneath.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Plays <paramref name="evolutions"/> in order and then calls
        /// <paramref name="onComplete"/>. An empty or null queue completes immediately without
        /// showing anything, so a caller never has to special-case "nothing evolved".</summary>
        public void Play(IReadOnlyList<ExperienceResolver.Evolution> evolutions, Action onComplete)
        {
            OnComplete = onComplete;
            finished = false;

            if (evolutions == null || evolutions.Count == 0)
            {
                gameObject.SetActive(false);
                Finish();
                return;
            }

            gameObject.SetActive(true);
            IsPlaying = true;
            routine = StartCoroutine(PlayAll(new List<ExperienceResolver.Evolution>(evolutions)));
        }

        /// <summary>The skip button, which covers the whole overlay. Jumps straight to the end
        /// rather than to the next mon: someone skipping has decided they don't want to watch this,
        /// and making them press it once per evolving mon is the same answer asked repeatedly.</summary>
        public void OnSkipClicked()
        {
            if (!IsPlaying)
            {
                return;
            }
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            Finish();
        }

        private IEnumerator PlayAll(List<ExperienceResolver.Evolution> evolutions)
        {
            foreach (var evolution in evolutions)
            {
                yield return PlayOne(evolution);
            }
            routine = null;
            Finish();
        }

        private IEnumerator PlayOne(ExperienceResolver.Evolution evolution)
        {
            var before = PokemonSprites.LoadFront(evolution.From);
            var after = PokemonSprites.LoadFront(evolution.To);

            SetFlashAlpha(0f);
            sprite.sprite = before;
            sprite.enabled = before != null || after != null;
            captionText.text = $"What? {evolution.FromName} is evolving!";
            yield return new WaitForSeconds(introSeconds);

            captionText.text = string.Empty;
            float elapsed = 0f;
            bool showingAfter = false;
            while (elapsed < flickerSeconds)
            {
                showingAfter = !showingAfter;
                sprite.sprite = showingAfter ? after : before;
                // Beats shorten linearly across the flicker, so the two forms trade places quicker
                // and quicker right up to the reveal.
                float progress = flickerSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / flickerSeconds);
                float beat = Mathf.Lerp(slowestFlickerBeat, fastestFlickerBeat, progress);
                yield return new WaitForSeconds(beat);
                elapsed += beat;
            }

            // The flash covers the swap, so the new form is never seen arriving — it's simply there
            // when the white clears.
            sprite.sprite = after;
            SetFlashAlpha(1f);
            captionText.text = $"{evolution.FromName} evolved into {evolution.ToName}!";

            for (float t = 0f; t < flashFadeSeconds; t += Time.deltaTime)
            {
                SetFlashAlpha(1f - Mathf.Clamp01(t / flashFadeSeconds));
                yield return null;
            }
            SetFlashAlpha(0f);

            yield return new WaitForSeconds(revealSeconds);
        }

        /// <summary>Guarded against running twice: the coroutine finishing and a skip landing on the
        /// same frame would otherwise show the result panel twice and, on a Gym, advance the run
        /// twice with it.</summary>
        private void Finish()
        {
            if (finished)
            {
                return;
            }
            finished = true;
            IsPlaying = false;
            gameObject.SetActive(false);
            var callback = OnComplete;
            OnComplete = null;
            callback?.Invoke();
        }

        private void SetFlashAlpha(float alpha)
        {
            if (flash == null)
            {
                return;
            }
            var color = flash.color;
            color.a = alpha;
            flash.color = color;
            flash.enabled = alpha > 0f;
        }
    }
}
