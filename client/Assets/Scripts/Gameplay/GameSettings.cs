using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>Player preferences, edited on the Settings screen. Stored in PlayerPrefs: these are
    /// settings for the app on this device, not part of a run, so they don't wait on the run save
    /// layer (PLAN.md Phase 2) and survive quitting.</summary>
    public static class GameSettings
    {
        public const string AutoplayBattlesKey = "settings.autoplayBattles";

        /// <summary>Whether a battle starts playing on its own (design doc §10.4).
        ///
        /// **Off by default.** A fight opens paused on its first Step, and the player presses Play
        /// or steps through it. Autoplay was the original default, but now that a Step is something
        /// to watch — the Leads strike, bars drain, passives fire — starting mid-flow means the
        /// opening exchange is already over before you've looked at the board. Turning it on is one
        /// press on the battle screen, and the choice sticks via the Settings screen.
        ///
        /// Note this only decides how a battle *opens*. Pause and Play are always available.
        ///
        /// Changing the fallback here changes it only for players who have never touched the
        /// setting: anyone who has toggled it has a stored value, which continues to win.</summary>
        public static bool AutoplayBattles
        {
            get => PlayerPrefs.GetInt(AutoplayBattlesKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(AutoplayBattlesKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
