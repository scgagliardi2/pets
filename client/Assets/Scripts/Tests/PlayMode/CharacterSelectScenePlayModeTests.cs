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
            Assert.AreEqual(28, buttons.Length);
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
            Assert.AreEqual(27, secondaryButtons.Length, "the chosen Starter should not reappear in the Secondary grid");
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

        [UnityTest]
        public IEnumerator ClickingTypeFilter_NarrowsTheGridToOnlyThatType()
        {
            int allCount = GridContent().GetComponentsInChildren<Button>().Length;

            var typeFilterButton = GameObject.Find("Canvas").transform.Find("ToolbarBar/TypeFilterButton").GetComponent<Button>();
            typeFilterButton.onClick.Invoke();
            yield return null;

            string label = typeFilterButton.GetComponentInChildren<Text>().text;
            StringAssert.StartsWith("Type: ", label);
            string activeType = label.Substring("Type: ".Length);

            var filteredButtons = GridContent().GetComponentsInChildren<Button>();
            Assert.Less(filteredButtons.Length, allCount, "filtering by a single type should narrow the grid");
            Assert.Greater(filteredButtons.Length, 0, "the first type in the enum should have at least one curated species");

            foreach (var button in filteredButtons)
            {
                string typeLine = button.GetComponentsInChildren<Text>()[1].text; // Name, Type, ATK, HP, SPD
                StringAssert.Contains(activeType, typeLine);
            }
        }

        [UnityTest]
        public IEnumerator ClickingSortAttack_OrdersCardsHighestFirst_ThenLowestFirstOnSecondClick()
        {
            var canvas = GameObject.Find("Canvas").transform;
            var sortAttackButton = canvas.Find("ToolbarBar/SortAttackButton").GetComponent<Button>();

            sortAttackButton.onClick.Invoke();
            yield return null;
            AssertOrderedByAttack(descending: true);

            sortAttackButton.onClick.Invoke();
            yield return null;
            AssertOrderedByAttack(descending: false);
        }

        private static void AssertOrderedByAttack(bool descending)
        {
            var buttons = GridContent().GetComponentsInChildren<Button>();
            Assert.Greater(buttons.Length, 1, "need at least two cards to prove an ordering");

            int previous = descending ? int.MaxValue : int.MinValue;
            foreach (var button in buttons)
            {
                string atkText = button.GetComponentsInChildren<Text>()[2].text; // "ATK 49"
                int attack = int.Parse(atkText.Substring("ATK ".Length));
                if (descending)
                {
                    Assert.LessOrEqual(attack, previous, "cards should be sorted by Attack, highest first");
                }
                else
                {
                    Assert.GreaterOrEqual(attack, previous, "cards should be sorted by Attack, lowest first");
                }
                previous = attack;
            }
        }
    }
}
