using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>The one component every menu button's onClick points at. The Home, Ingame Menu,
    /// Team and Region Map scenes are all wired by the Editor scene builders through
    /// UnityEventTools.AddVoidPersistentListener, which needs a real no-argument method on a real
    /// component — so plain navigation lives here rather than as a per-screen controller class
    /// that would otherwise hold nothing but LoadScene calls.
    ///
    /// Screens that also have state to show (Team) keep their own controller for that and still
    /// use this for their Back button.
    ///
    /// Every method here goes through ScreenFade.TransitionTo rather than loading a scene
    /// directly: a synchronous load freezes the main thread for the whole of the next screen's
    /// Start, which on Character Select and the Map is a lot of work. See ScreenFade.</summary>
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
            ScreenFade.TransitionTo(SceneNames.CharacterSelect);
        }

        public void GoToCharacterSelect() => ScreenFade.TransitionTo(SceneNames.CharacterSelect);

        public void GoToMap() => ScreenFade.TransitionTo(SceneNames.Map);

        public void GoToIngameMenu() => ScreenFade.TransitionTo(SceneNames.IngameMenu);

        public void GoToTeam() => ScreenFade.TransitionTo(SceneNames.Team);

        public void GoHome() => ScreenFade.TransitionTo(SceneNames.Home);

        public void GoToHistory() => ScreenFade.TransitionTo(SceneNames.History);

        public void GoToCredits() => ScreenFade.TransitionTo(SceneNames.Credits);

        public void GoToDevRoster() => ScreenFade.TransitionTo(SceneNames.DevRoster);

        /// <summary>Team's dev "Random Battle" and the Battle screen's "Battle Again" — every load
        /// of the Battle scene rolls a fresh enemy team, so re-entering it is how a new fight
        /// starts.</summary>
        public void GoToBattle() => ScreenFade.TransitionTo(SceneNames.Battle);

        /// <summary>The scene Settings was opened from, so its Back button can go back there.
        /// Static for the same reason ActiveRun is: it has to outlive the scene that set it.</summary>
        private static string settingsReturnScene;

        /// <summary>Home's and the Ingame Menu's Settings button.</summary>
        public void GoToSettings()
        {
            settingsReturnScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            ScreenFade.TransitionTo(SceneNames.Settings);
        }

        /// <summary>Settings' Back button. Home when Settings was opened directly (no scene to
        /// return to), which is also the safe answer if the recorded scene is Settings itself.</summary>
        public void ReturnFromSettings()
        {
            string target = string.IsNullOrEmpty(settingsReturnScene) || settingsReturnScene == SceneNames.Settings
                ? SceneNames.Home
                : settingsReturnScene;
            ScreenFade.TransitionTo(target);
        }

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
            ScreenFade.TransitionTo(SceneNames.IngameMenu);
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
