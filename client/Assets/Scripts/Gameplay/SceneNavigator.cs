using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pets.Gameplay
{
    /// <summary>The one component every menu button's onClick points at. The Home, Ingame Menu,
    /// Team and Region Map scenes are all wired by the Editor scene builders through
    /// UnityEventTools.AddVoidPersistentListener, which needs a real no-argument method on a real
    /// component — so plain navigation lives here rather than as a per-screen controller class
    /// that would otherwise hold nothing but LoadScene calls.
    ///
    /// Screens that also have state to show (Team) keep their own controller for that and still
    /// use this for their Back button.</summary>
    public sealed class SceneNavigator : MonoBehaviour
    {
        /// <summary>Home's "New Game": drops whatever run was in progress before sending the
        /// player to pick a starter, so a half-finished run can't leak into the next one via
        /// ActiveRun. Resuming an interrupted run is a save-system job (PLAN.md Phase 2), not
        /// something to fake by leaving the old state lying around.</summary>
        public void StartNewGame()
        {
            ActiveRun.End();
            PendingRunSelection.Clear();
            SceneManager.LoadScene(SceneNames.CharacterSelect);
        }

        public void GoToCharacterSelect() => SceneManager.LoadScene(SceneNames.CharacterSelect);

        public void GoToMap() => SceneManager.LoadScene(SceneNames.Map);

        public void GoToIngameMenu() => SceneManager.LoadScene(SceneNames.IngameMenu);

        public void GoToTeam() => SceneManager.LoadScene(SceneNames.Team);

        public void GoHome() => SceneManager.LoadScene(SceneNames.Home);

        public void GoToHistory() => SceneManager.LoadScene(SceneNames.History);

        public void GoToCredits() => SceneManager.LoadScene(SceneNames.Credits);

        public void GoToDevRoster() => SceneManager.LoadScene(SceneNames.DevRoster);

        /// <summary>Home's "Continue Run": back into the run still held by ActiveRun, landing on
        /// the Ingame Menu rather than straight on the Map so the player sees where they are
        /// before moving. Guarded because a run only survives "Quit to Home" in memory (there's no
        /// save layer yet — PLAN.md Phase 2), so anything that clears ActiveRun makes this a
        /// no-op; HomeScreenController hides the button in that case.</summary>
        public void ContinueRun()
        {
            if (!ActiveRun.HasRun)
            {
                return;
            }
            SceneManager.LoadScene(SceneNames.IngameMenu);
        }

        /// <summary>Application.Quit is a no-op in the Editor (it only ends a real player
        /// process), so the Quit button would look broken every time it's tested in Play mode
        /// without the Editor branch.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
