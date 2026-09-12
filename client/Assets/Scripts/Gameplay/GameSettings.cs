using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>Player preferences, edited on the Settings screen. Stored in PlayerPrefs: these are
    /// settings for the app on this device, not part of a run, so they don't wait on the run save
    /// layer (PLAN.md Phase 2) and survive quitting.</summary>
    public static class GameSettings
    {
        public const string AutoplayBattlesKey = "settings.autoplayBattles";

        /// <summary>Whether a battle starts playing on its own (design doc §10.4). On by default;
        /// the battle screen's pause is always available either way.</summary>
        public static bool AutoplayBattles
        {
            get => PlayerPrefs.GetInt(AutoplayBattlesKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(AutoplayBattlesKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
