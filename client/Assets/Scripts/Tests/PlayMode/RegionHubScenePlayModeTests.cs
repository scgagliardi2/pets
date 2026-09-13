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
using Pets.Data;
using Pets.Gameplay;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>Drives the saved Region Hub scene (RegionHubSceneBuilder): the screen a run passes
    /// through between Locations. The wiring between a generated scene and its controller is
    /// exactly what compiles fine and does nothing, so these click the real cards.</summary>
    public class RegionHubScenePlayModeTests
    {
        private const string RegionHubScenePath = "Assets/Scenes/" + SceneNames.RegionHub + ".unity";

        [SetUp]
        public void SetUp() => ResetRunState();

        [TearDown]
        public void TearDown() => ResetRunState();

        private static void ResetRunState()
        {
            ActiveRun.End();
            PendingRunSelection.Clear();
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

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = 20;
            species.BaseHealth = 40;
            species.BaseSpeed = 20;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary()
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = new System.Collections.Generic.List<PokemonSpeciesDefinitionAsset>
            {
                MakeSpecies(1, "Alpha"),
                MakeSpecies(2, "Beta")
            };
            return library;
        }

        /// <summary>A run standing between Locations, with <paramref name="badges"/> already
        /// earned — what the hub is for.</summary>
        private static RunState BeginRun(int badges = 0)
        {
            var library = MakeLibrary();
            var run = new RunState { RunSeed = 31337, Badges = badges, RegionIndex = badges + 1 };
            run.LineUp.Add(PokemonInstanceFactory.Create(library.AllSpecies[0], "lead"));
            ActiveRun.Begin(run, library);
            return run;
        }

        private static RegionHubController Controller() =>
            Object.FindFirstObjectByType<RegionHubController>();

        [UnityTest]
        public IEnumerator RegionHubScene_ShowsOneCardPerOfferedLocation()
        {
            BeginRun();

            yield return LoadScene(RegionHubScenePath);

            var controller = Controller();
            Assert.IsNotNull(controller, "the Region Hub scene should be running its controller");
            Assert.AreEqual(RegionHubGenerator.OfferCount, controller.Offers.Count);

            var cards = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.name.StartsWith("OfferCard")).ToList();
            Assert.AreEqual(RegionHubGenerator.OfferCount, cards.Count,
                "every offer should be a card the player can press");

            var names = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).ToList();
            foreach (var offer in controller.Offers)
            {
                CollectionAssert.Contains(names, LocationCatalog.DisplayNameFor(offer.Type));
            }
        }

        [UnityTest]
        public IEnumerator RegionHubScene_ShowsHowFarThroughTheRunTheseBadgesAre()
        {
            BeginRun(badges: 2);

            yield return LoadScene(RegionHubScenePath);

            string progress = GameObject.Find("ProgressText").GetComponent<Text>().text;
            StringAssert.Contains($"Badges 2 / {RegionTier.RegionsPerRun}", progress);
            StringAssert.Contains($"Location 3 of {RegionTier.RegionsPerRun}", progress);
        }

        [UnityTest]
        public IEnumerator RegionHubScene_PressingACard_EntersThatLocationAndLeavesForTheMap()
        {
            var run = BeginRun();

            yield return LoadScene(RegionHubScenePath);

            var controller = Controller();
            var chosen = controller.Offers[1];
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(b => b.name == "OfferCard1").onClick.Invoke();

            Assert.AreEqual(chosen.Type, run.CurrentLocation,
                "the Location picked is the one whose wildlife the map will draw from");
            Assert.IsNotNull(run.LocationMap, "picking a Location generates its node-map");
            Assert.AreEqual(chosen.MapSeed, run.LocationMap.Seed);

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
        }

        /// <summary>Opened on its own, the scene bootstraps a run like the Map scene does, so it
        /// stands up and offers Locations rather than throwing on a missing run — this is the
        /// screen a designer opens directly.</summary>
        [UnityTest]
        public IEnumerator RegionHubScene_OpenedWithNoRun_BootstrapsOneAndStillOffersLocations()
        {
            yield return LoadScene(RegionHubScenePath);

            Assert.IsTrue(ActiveRun.HasRun, "the scene's RunBootstrapper should have built a run");
            Assert.AreEqual(RegionHubGenerator.OfferCount, Controller().Offers.Count);
        }
    }
}
