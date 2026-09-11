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

namespace Pets.Tests
{
    /// <summary>Drives the actual saved Character Select scene: verifies every curated species
    /// gets a card, that picking a Starter removes it from the Secondary grid, and that confirming
    /// hands the chosen pair off to RunBootstrapper via PendingRunSelection (see CLAUDE.md's
    /// "for UI changes, actually click through the flow" convention — this is the automated
    /// analogue of that for a headless environment).</summary>
    public class CharacterSelectScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;
        }

        private static Transform GridContent() => GameObject.Find("Canvas").transform.Find("SpeciesScroll/Viewport/Content");

        [UnityTest]
        public IEnumerator Grid_ShowsACardForEveryCuratedSpecies()
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            Assert.AreEqual(13, buttons.Length);
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryCard_HasItsPokemonSpriteLoaded()
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                var sprite = button.transform.Find("Sprite")?.GetComponent<Image>();
                Assert.IsNotNull(sprite, $"{button.gameObject.name} has no Sprite child");
                Assert.IsNotNull(sprite.sprite, $"{button.gameObject.name}'s sprite failed to load");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator PickingAStarterThenASecondary_ThenConfirming_StartsARunWithThatExactPair()
        {
            var starterButtons = GridContent().GetComponentsInChildren<Button>();
            string starterCardName = starterButtons[0].gameObject.name;
            starterButtons[0].onClick.Invoke();
            yield return null;

            var secondaryButtons = GridContent().GetComponentsInChildren<Button>();
            Assert.AreEqual(12, secondaryButtons.Length, "the chosen Starter should not reappear in the Secondary grid");
            Assert.IsFalse(secondaryButtons.Any(b => b.gameObject.name == starterCardName));

            secondaryButtons[0].onClick.Invoke();
            yield return null;

            var canvas = GameObject.Find("Canvas").transform;
            var confirmButton = canvas.Find("ConfirmButton").GetComponent<Button>();
            Assert.IsTrue(confirmButton.gameObject.activeInHierarchy);

            string summaryBeforeConfirm = canvas.Find("PromptText").GetComponent<Text>().text;

            confirmButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsNotNull(RunBootstrapper.Instance, "confirming should have loaded the Game scene and its RunBootstrapper");
            var state = RunBootstrapper.Instance.State;
            var library = RunBootstrapper.Instance.SpeciesLibrary;
            string leadName = library.GetById(state.LineUp[0].SpeciesId).DisplayName;
            string supportName = library.GetById(state.LineUp[1].SpeciesId).DisplayName;

            StringAssert.Contains(leadName, summaryBeforeConfirm);
            StringAssert.Contains(supportName, summaryBeforeConfirm);
        }
    }
}
