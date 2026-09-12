using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pets.Gameplay
{
    /// <summary>Crossfades between scenes, and is the reason scene changes go through
    /// <see cref="TransitionTo"/> rather than SceneManager.LoadScene directly.
    ///
    /// Every navigation used to be a synchronous LoadScene, which stalls the main thread for the
    /// whole teardown-plus-load-plus-Start of the next screen — and Start is where this game does
    /// its real work (Character Select builds a card per species, the Map generates a graph and
    /// lays out every node). The result was a freeze on every button press, with the button still
    /// drawn in its pressed state, and no indication anything was happening. Fading to black
    /// first and loading asynchronously afterwards puts that cost behind an opaque screen, so the
    /// hitch is no longer something the player can see.
    ///
    /// Created per scene from <see cref="SceneManager.sceneLoaded"/> rather than placed by the
    /// scene builders: it needs no serialized references, it has to exist in every scene including
    /// ones entered directly (play-from-this-scene in the Editor, and the PlayMode tests), and
    /// sceneLoaded fires after the scene's Awake calls but before any Start, so the overlay is up
    /// before the first frame is drawn. It gets its own Canvas at a high sorting order, which also
    /// keeps fading it from dirtying the screen's own canvas batch.</summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        /// <summary>Long enough to read as a deliberate transition, short enough not to feel like
        /// waiting. Unscaled, so it still runs if anything ever pauses the game with timeScale.</summary>
        public const float Duration = 0.15f;

        /// <summary>Above every screen's own canvas (those are all at the default 0) and above the
        /// Dropdown template's 30000 override, so nothing can draw over the fade.</summary>
        private const int SortingOrder = 32000;

        private static ScreenFade current;

        private CanvasGroup group;
        private bool isTransitioning;

        /// <summary>True while a scene change is in flight. Nothing should queue a second one.</summary>
        public static bool IsTransitioning => current != null && current.isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Guarded because RuntimeInitializeOnLoadMethod runs again on each Play in the Editor
            // while the static field can survive with domain reload disabled (this project plays
            // with it off — see Assets/Editor's capture helpers).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureExists();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureExists();

        private static void EnsureExists()
        {
            if (current != null)
            {
                return;
            }

            var go = new GameObject("ScreenFade");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var fade = go.AddComponent<ScreenFade>();
            fade.group = go.AddComponent<CanvasGroup>();

            var imageGO = new GameObject("Cover", typeof(RectTransform));
            imageGO.transform.SetParent(go.transform, false);
            var image = imageGO.AddComponent<Image>();
            image.color = Color.black;
            var rect = imageGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            current = fade;

            // Opaque to begin with, so the scene underneath is never shown un-faded, then eased
            // off over Duration. A scene the player arrives at mid-transition therefore always
            // fades in, whichever way it was entered.
            fade.group.alpha = 1f;
            fade.group.blocksRaycasts = true;
            fade.StartCoroutine(fade.FadeTo(0f));
        }

        /// <summary>Fades out, loads <paramref name="sceneName"/> asynchronously, and lets the new
        /// scene's own fade-in take over. Ignored if a transition is already running, so a
        /// double-click can't start two loads.</summary>
        public static void TransitionTo(string sceneName)
        {
            if (IsTransitioning)
            {
                return;
            }

            EnsureExists();
            current.StartCoroutine(current.Transition(sceneName));
        }

        private IEnumerator Transition(string sceneName)
        {
            isTransitioning = true;
            group.blocksRaycasts = true;

            yield return FadeTo(1f);

            // Only now that the screen is covered: the load and the next scene's Start are what
            // actually hitch, and neither is visible from here.
            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            // The loaded scene replaces this object along with the rest of the old one, so
            // clearing the flag matters only if the load somehow didn't take.
            isTransitioning = false;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = group.alpha;
            if (Mathf.Approximately(start, target))
            {
                group.alpha = target;
                group.blocksRaycasts = target > 0f;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / Duration));
                yield return null;
            }

            group.alpha = target;
            group.blocksRaycasts = target > 0f;
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }
    }
}
