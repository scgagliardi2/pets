using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>Runtime settings applied once before the first scene loads, for things that belong
    /// to the application rather than to any screen.
    ///
    /// A RuntimeInitializeOnLoadMethod rather than a bootstrap object in Home.unity: there's
    /// nothing to serialize, and a scene object would have to either be duplicated into every
    /// scene (so entering a scene directly — which the PlayMode tests and the Editor's play-from-
    /// this-scene both do — still configures the app) or survive as a DontDestroyOnLoad singleton,
    /// which this project deliberately avoids (see ActiveRun's note).</summary>
    public static class AppConfig
    {
        /// <summary>Nothing in this game animates faster than a UI tween, so there's no reason to
        /// render more often than the display refreshes. Left uncapped, a static menu screen
        /// renders as fast as the GPU allows — wasted battery on a phone and a hot laptop for a
        /// screen that isn't changing. QualitySettings.vSyncCount covers this on desktop; mobile
        /// ignores vSyncCount and honours targetFrameRate instead, so both are set.</summary>
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
