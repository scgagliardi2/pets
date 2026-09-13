using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>The saved Region Hub scene against RegionHubController (design doc §5.2, ADR 0006):
    /// it bootstraps a run from Character Select's pair, offers three Locations, sends the run to the
    /// one picked, and is where "Back to Map" lands while the run is between Locations.</summary>
    public class RegionHubScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RegionHub.unity";
        private const string IngameMenuScenePath = "Assets/Scenes/IngameMenu.unity";

        [SetUp]
        public void SetUp() => ResetRunState();

        [TearDown]
        public void TearDown() => ResetRunState();

        private static void ResetRunState()
        {
            ActiveRun.End();
            PendingRunSelection.Clear();
            PendingBattle.Clear();
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

        [UnityTest]
        public IEnumerator RegionHub_BootstrapsARun_AndOffersThreeDistinctLocations()
        {
            yield return LoadScene(ScenePath);

            Assert.IsTrue(ActiveRun.HasRun, "the hub is where Character Select's pair becomes a run");
            Assert.AreSame(RunBootstrapper.Instance.State, ActiveRun.State);

            var hub = Hub();
            Assert.AreEqual(LocationCatalog.OfferCount, hub.Offers.Count);
            CollectionAssert.AllItemsAreUnique(hub.Offers);

            for (int i = 0; i < hub.Offers.Count; i++)
            {
                var entry = LocationCatalog.Get(hub.Offers[i]);
                var card = Card(i);
                Assert.AreEqual(entry.DisplayName, card.Find("NameText").GetComponent<Text>().text);
                Assert.AreEqual(entry.Flavor, card.Find("FlavorText").GetComponent<Text>().text);
                var row = card.Find("TypesRow");
                int icons = row.Cast<Transform>().Count(t => t.gameObject.activeSelf);
                Assert.AreEqual(entry.TypeBias.Length, icons, $"{entry.DisplayName} should show one icon per type");
                Assert.IsTrue(card.Find("TravelButton").GetComponent<Button>().interactable);
            }

            StringAssert.Contains($"Gym 1 of {RunProgression.BadgesToWin}", Label("SubheaderText"));
            Assert.AreEqual($"Badges 0/{RunProgression.BadgesToWin}", Label("BadgesValue"));
        }

        [UnityTest]
        public IEnumerator RegionHub_ButtonsAreWired()
        {
            yield return LoadScene(ScenePath);

            var menu = FindButton("MenuButton");
            Assert.AreEqual(1, menu.onClick.GetPersistentEventCount());
            Assert.IsInstanceOf<SceneNavigator>(menu.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(SceneNavigator.GoToIngameMenu), menu.onClick.GetPersistentMethodName(0));

            for (int i = 0; i < LocationCatalog.OfferCount; i++)
            {
                var travel = Card(i).Find("TravelButton").GetComponent<Button>();
                Assert.AreEqual(1, travel.onClick.GetPersistentEventCount(), $"card {i}'s Travel button");
                Assert.IsInstanceOf<RegionHubController>(travel.onClick.GetPersistentTarget(0));
                Assert.AreEqual(nameof(RegionHubController.OnTravelClicked), travel.onClick.GetPersistentMethodName(0));
            }
        }

        [UnityTest]
        public IEnumerator Travelling_SetsTheRunsLocation_AndOpensItsMap()
        {
            yield return LoadScene(ScenePath);
            var chosen = Hub().Offers[1];

            Card(1).Find("TravelButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(chosen, ActiveRun.State.CurrentLocation);
            Assert.IsFalse(Card(0).Find("TravelButton").GetComponent<Button>().interactable, "one choice per visit");

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
            yield return null;
            StringAssert.StartsWith(LocationCatalog.Get(chosen).DisplayName, Label("TitleText"),
                "the map should name the Location the run travelled to");
        }

        [UnityTest]
        public IEnumerator AfterABadge_TheHubSaysSo_AndLeavesOutTheLocationJustBeaten()
        {
            var run = MakeRun();
            run.TravelTo(LocationType.Desert);
            run.EarnBadge();
            ActiveRun.Begin(run, MakeLibrary());

            yield return LoadScene(ScenePath);

            CollectionAssert.DoesNotContain(Hub().Offers, LocationType.Desert);
            StringAssert.Contains($"Gym 2 of {RunProgression.BadgesToWin}", Label("SubheaderText"));
            Assert.AreEqual($"Badges 1/{RunProgression.BadgesToWin}", Label("BadgesValue"));
        }

        [UnityTest]
        public IEnumerator RegionHub_ForARunAlreadyInALocation_SendsThePlayerToItsMap()
        {
            var run = MakeRun();
            run.TravelTo(LocationType.Plains);
            ActiveRun.Begin(run, MakeLibrary());

            yield return LoadScene(ScenePath);

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
        }

        /// <summary>The Ingame Menu's "Back to Map" has no map to go back to after a Gym — the hub is
        /// where the run actually is.</summary>
        [UnityTest]
        public IEnumerator GoToMap_WhileBetweenLocations_OpensTheRegionHub()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());
            yield return LoadScene(IngameMenuScenePath);

            Object.FindFirstObjectByType<SceneNavigator>().GoToMap();

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.RegionHub);
        }

        [UnityTest]
        public IEnumerator GoToMap_InsideALocation_OpensItsMap()
        {
            var run = MakeRun();
            run.TravelTo(LocationType.Forest);
            ActiveRun.Begin(run, MakeLibrary());
            yield return LoadScene(IngameMenuScenePath);

            Object.FindFirstObjectByType<SceneNavigator>().GoToMap();

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
        }

        private static RegionHubController Hub()
        {
            var hub = Object.FindFirstObjectByType<RegionHubController>();
            Assert.IsNotNull(hub, "the scene should have a RegionHubController");
            return hub;
        }

        private static Transform Card(int index)
        {
            var card = GameObject.Find($"LocationCard{index}");
            Assert.IsNotNull(card, $"expected LocationCard{index}");
            return card.transform;
        }

        private static string Label(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"expected a '{name}' label");
            return go.GetComponent<Text>().text;
        }

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"Expected a Button named '{name}'");
            return button;
        }

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = 10;
            species.BaseHealth = 50;
            species.BaseSpeed = 10;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary()
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = new List<PokemonSpeciesDefinitionAsset> { MakeSpecies(1, "Alpha"), MakeSpecies(2, "Beta") };
            return library;
        }

        private static RunState MakeRun()
        {
            var run = new RunState { RunSeed = 77 };
            run.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(1, "Alpha"), "mon-0"));
            run.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(2, "Beta"), "mon-1"));
            return run;
        }
    }
}
