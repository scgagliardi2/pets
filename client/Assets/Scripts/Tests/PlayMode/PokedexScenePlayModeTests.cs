using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Pets.Data;
using Pets.Gameplay;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>Drives the saved Pokédex scene. The point of the screen is that it shows the
    /// *whole* roster where Character Select deliberately shows a slice, so that's what most of
    /// these assert — plus the usual new-screen wiring check, since a generated scene and its
    /// controller agreeing is exactly what compiles fine and does nothing.
    ///
    /// It runs with no run in progress on purpose: the Pokédex hangs off Home and must not need
    /// one.</summary>
    public class PokedexScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Pokedex.unity";

        [SetUp]
        public void SetUp() => ActiveRun.End();

        [TearDown]
        public void TearDown() => ActiveRun.End();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;
        }

        private static Transform Canvas() => GameObject.Find("Canvas").transform;

        private static Transform GridContent() => Canvas().Find("SpeciesScroll/Viewport/Content");

        private static PokemonSpeciesLibrary SceneLibrary()
        {
            var controller = Object.FindFirstObjectByType<PokedexController>();
            Assert.IsNotNull(controller, "the scene should have a PokedexController");
            var field = typeof(PokedexController).GetField("speciesLibrary", BindingFlags.NonPublic | BindingFlags.Instance);
            return (PokemonSpeciesLibrary)field.GetValue(controller);
        }

        [UnityTest]
        public IEnumerator Grid_ShowsACardForEverySpeciesInTheRoster()
        {
            var library = SceneLibrary();
            Assert.IsNotNull(library, "the scene builder should have wired the species library");
            Assert.Greater(library.AllSpecies.Count, 0, "the species library is empty");

            var cards = GridContent().GetComponentsInChildren<Button>();
            Assert.AreEqual(library.AllSpecies.Count, cards.Length);
            yield break;
        }

        /// <summary>The Pokédex's reason to exist: it shows species Character Select won't offer.
        /// If this ever fails the two screens have converged and one of them is redundant.</summary>
        [UnityTest]
        public IEnumerator Grid_IncludesSpeciesCharacterSelectFiltersOut()
        {
            var library = SceneLibrary();
            var ineligible = library.AllSpecies
                .Where(s => !CharacterSelectController.IsStarterEligible(s))
                .ToList();
            Assert.IsNotEmpty(ineligible, "no species is above the starter cap, so there's nothing to compare");

            var cardNames = GridContent().GetComponentsInChildren<Button>().Select(b => b.gameObject.name).ToList();
            foreach (var species in ineligible)
            {
                CollectionAssert.Contains(cardNames, $"Card_{species.DisplayName}",
                    $"{species.DisplayName} is above the starter cap and should still be in the Pokedex");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryCard_ShowsItsSpriteTypeIconsAndStatBars()
        {
            foreach (var card in GridContent().GetComponentsInChildren<Button>())
            {
                string name = card.gameObject.name;

                var sprite = card.transform.Find("Sprite")?.GetComponent<Image>();
                Assert.IsNotNull(sprite?.sprite, $"{name}'s artwork failed to load");

                var icons = card.GetComponentsInChildren<TypeIconView>();
                Assert.IsTrue(icons.Length == 1 || icons.Length == 2,
                    $"{name} should show 1 or 2 type icons, found {icons.Length}");
                foreach (var icon in icons)
                {
                    Assert.IsNotNull(icon.Icon.sprite, $"{name}'s {icon.Type} type icon failed to load");
                }

                var health = card.GetComponentInChildren<HealthBarView>();
                Assert.IsNotNull(health, $"{name} has no health bar");
                Assert.AreEqual(health.Max, health.Current, $"{name} should show full health");

                var speed = card.GetComponentInChildren<StatBarView>();
                Assert.IsNotNull(speed, $"{name} has no speed bar");
                Assert.AreEqual(PokemonSpeciesDefinitionAsset.MaxBaseSpeed, speed.Max,
                    $"{name}'s speed bar isn't scaled to the roster-wide cap");

                var rect = card.GetComponent<RectTransform>();
                Assert.LessOrEqual(LayoutUtility.GetPreferredHeight(rect), rect.rect.height + 0.5f,
                    $"{name}'s lines overflow its cell — grow SpeciesGridView.CardHeight");
            }
            yield break;
        }

        /// <summary>The count readout is what tells a filtered Pokédex apart from a short
        /// one.</summary>
        [UnityTest]
        public IEnumerator CountText_ReportsTheWholeRosterAndThenTheFilteredSubset()
        {
            int total = SceneLibrary().AllSpecies.Count;
            var countText = Canvas().Find("CountText").GetComponent<Text>();
            Assert.AreEqual($"{total} species", countText.text);

            var dropdown = Canvas().Find("ToolbarBar/TypeFilterDropdown").GetComponent<Dropdown>();
            dropdown.value = 1;
            yield return null;

            int shown = GridContent().GetComponentsInChildren<Button>().Length;
            Assert.Less(shown, total, "filtering to one type should narrow the grid");
            Assert.AreEqual($"{shown} of {total} species", countText.text);

            dropdown.value = 0;
            yield return null;
            Assert.AreEqual($"{total} species", countText.text);
        }

        [UnityTest]
        public IEnumerator ClickingACard_FillsTheDetailLineForThatSpecies()
        {
            var detailText = Canvas().Find("BottomBar/DetailText").GetComponent<Text>();
            string before = detailText.text;

            var card = GridContent().GetComponentsInChildren<Button>().First();
            // Card_<DisplayName>, the name SpeciesGridView binds.
            string displayName = card.gameObject.name.Substring("Card_".Length);
            card.onClick.Invoke();
            yield return null;

            Assert.AreNotEqual(before, detailText.text, "clicking a card should fill the detail line");
            StringAssert.Contains(displayName, detailText.text);

            var species = SceneLibrary().AllSpecies.First(s => s.DisplayName == displayName);
            StringAssert.Contains($"#{species.Id}", detailText.text);
            StringAssert.Contains($"total {species.BaseStatTotal}", detailText.text);
            StringAssert.Contains(species.Passive.DisplayName, detailText.text);
        }

        [UnityTest]
        public IEnumerator SortingByAttack_OrdersTheWholeRosterHighestFirst()
        {
            Canvas().Find("ToolbarBar/SortAttackButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            int previous = int.MaxValue;
            foreach (var card in GridContent().GetComponentsInChildren<Button>())
            {
                int attack = int.Parse(card.transform.Find("NameRow/AttackValue").GetComponent<Text>().text);
                Assert.LessOrEqual(attack, previous, "cards should be sorted by Attack, highest first");
                previous = attack;
            }
        }

        [UnityTest]
        public IEnumerator BackButton_ReturnsHome()
        {
            var back = Canvas().Find("BottomBar/BackButton").GetComponent<Button>();
            Assert.AreEqual(1, back.onClick.GetPersistentEventCount());
            Assert.IsInstanceOf<SceneNavigator>(back.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(SceneNavigator.GoHome), back.onClick.GetPersistentMethodName(0));
            yield break;
        }
    }
}
