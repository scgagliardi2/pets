using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Pets.Gameplay;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>The Settings screen: its one setting (autoplay battles) round-trips to
    /// GameSettings, and Back returns to whichever screen opened it. The player's real setting is
    /// restored afterwards — PlayerPrefs in the Editor are the real ones.</summary>
    public class SettingsScenePlayModeTests
    {
        private const string SettingsScenePath = "Assets/Scenes/Settings.unity";
        private const string IngameMenuScenePath = "Assets/Scenes/IngameMenu.unity";

        private bool hadSetting;
        private bool savedSetting;

        [SetUp]
        public void SetUp()
        {
            hadSetting = PlayerPrefs.HasKey(GameSettings.AutoplayBattlesKey);
            savedSetting = GameSettings.AutoplayBattles;
        }

        [TearDown]
        public void TearDown()
        {
            if (hadSetting)
            {
                GameSettings.AutoplayBattles = savedSetting;
            }
            else
            {
                PlayerPrefs.DeleteKey(GameSettings.AutoplayBattlesKey);
            }
        }

        private static IEnumerator LoadScene(string path)
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
            yield return null;
            yield return null;
        }

        [Test]
        public void AutoplayBattles_DefaultsToOn()
        {
            PlayerPrefs.DeleteKey(GameSettings.AutoplayBattlesKey);

            Assert.IsTrue(GameSettings.AutoplayBattles);
        }

        [UnityTest]
        public IEnumerator AutoplayToggle_FlipsTheSetting_AndSaysWhichWayItIs()
        {
            GameSettings.AutoplayBattles = true;

            yield return LoadScene(SettingsScenePath);
            var toggle = FindButton("AutoplayToggleButton");
            Assert.AreEqual("On", toggle.GetComponent<UiButton>().Text);

            toggle.onClick.Invoke();
            Assert.IsFalse(GameSettings.AutoplayBattles);
            Assert.AreEqual("Off", toggle.GetComponent<UiButton>().Text);

            toggle.onClick.Invoke();
            Assert.IsTrue(GameSettings.AutoplayBattles);
            Assert.AreEqual("On", toggle.GetComponent<UiButton>().Text);
        }

        [UnityTest]
        public IEnumerator Settings_BackButton_IsWiredToReturnFromSettings()
        {
            yield return LoadScene(SettingsScenePath);

            var back = FindButton("BackButton");
            Assert.AreEqual(1, back.onClick.GetPersistentEventCount());
            Assert.IsInstanceOf<SceneNavigator>(back.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(SceneNavigator.ReturnFromSettings), back.onClick.GetPersistentMethodName(0));
        }

        /// <summary>Opened from the Ingame Menu, Back goes to the Ingame Menu — not to Home.</summary>
        [UnityTest]
        public IEnumerator OpenedFromTheIngameMenu_BackReturnsThere()
        {
            yield return LoadScene(IngameMenuScenePath);

            Object.FindFirstObjectByType<SceneNavigator>().GoToSettings();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Settings);

            Object.FindFirstObjectByType<SceneNavigator>().ReturnFromSettings();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.IngameMenu);
        }

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"Expected a Button named '{name}' in the Settings scene");
            return button;
        }
    }
}
