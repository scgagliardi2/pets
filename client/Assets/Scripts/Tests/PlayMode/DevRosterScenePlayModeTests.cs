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
    /// <summary>Drives the dev roster screen against the real scene and the real curated roster:
    /// clicking a card has to actually put a mon into the run in progress, since the whole point
    /// of the screen is setting up a team state by hand that would otherwise take a long play
    /// session to reach.</summary>
    public class DevRosterScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/DevRoster.unity";

        [SetUp]
        public void SetUp() => ActiveRun.End();

        [TearDown]
        public void TearDown() => ActiveRun.End();

        private static IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;
        }

        /// <summary>A run with one mon already in the line-up. Built in memory rather than from
        /// the curated library: this screen adds from its own serialized library and never reads
        /// ActiveRun.Library, so the run only has to exist and have a starting mon in it.</summary>
        private static void BeginRun()
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = 9001;
            species.DisplayName = "Starting";
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = 10;
            species.BaseHealth = 50;
            species.BaseSpeed = 10;

            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = new System.Collections.Generic.List<PokemonSpeciesDefinitionAsset> { species };

            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "starting-lead"));
            ActiveRun.Begin(state, library);
        }

        private static Button FirstCard()
        {
            var grid = GameObject.Find("Canvas").transform.Find("SpeciesScroll/Viewport/Content");
            Assert.IsNotNull(grid, "expected the species grid at SpeciesScroll/Viewport/Content");
            var card = grid.GetComponentsInChildren<Button>().FirstOrDefault();
            Assert.IsNotNull(card, "the grid should be full of species cards");
            return card;
        }

        private static Button FindButton(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);

        [UnityTest]
        public IEnumerator ClickingACard_AddsThatSpeciesToTheParty()
        {
            BeginRun();
            yield return LoadScene();

            FirstCard().onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, ActiveRun.State.LineUp.Count);
            Assert.IsEmpty(ActiveRun.State.Box);
            StringAssert.Contains("Party 2 / 6", GameObject.Find("StatusText").GetComponent<Text>().text);
        }

        /// <summary>Repeats are the point: a team of the same mon over and over is a legitimate
        /// thing to want to test against, and each copy still has to be its own instance.</summary>
        [UnityTest]
        public IEnumerator ClickingTheSameCardTwice_AddsTwoDistinctMons()
        {
            BeginRun();
            yield return LoadScene();

            var card = FirstCard();
            card.onClick.Invoke();
            card.onClick.Invoke();
            yield return null;

            Assert.AreEqual(3, ActiveRun.State.LineUp.Count);
            Assert.AreEqual(ActiveRun.State.LineUp[1].SpeciesId, ActiveRun.State.LineUp[2].SpeciesId);
            Assert.AreNotEqual(ActiveRun.State.LineUp[1].InstanceId, ActiveRun.State.LineUp[2].InstanceId,
                "each added copy should be its own instance");
        }

        [UnityTest]
        public IEnumerator WithTheBoxAsTheTarget_ClickingACard_AddsToTheBox()
        {
            BeginRun();
            yield return LoadScene();

            FindButton("TargetBoxButton").onClick.Invoke();
            FirstCard().onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count, "the party shouldn't have grown");
            Assert.AreEqual(1, ActiveRun.State.Box.Count);
        }

        /// <summary>A full party falls through to the Box instead of swallowing the click, and
        /// says so.</summary>
        [UnityTest]
        public IEnumerator WithAFullParty_ClickingACard_FallsBackToTheBox()
        {
            BeginRun();
            yield return LoadScene();

            var card = FirstCard();
            for (int i = ActiveRun.State.LineUp.Count; i < RunState.MaxPartySize; i++)
            {
                card.onClick.Invoke();
            }
            Assert.AreEqual(RunState.MaxPartySize, ActiveRun.State.LineUp.Count);

            card.onClick.Invoke();
            yield return null;

            Assert.AreEqual(RunState.MaxPartySize, ActiveRun.State.LineUp.Count);
            Assert.AreEqual(1, ActiveRun.State.Box.Count);
            StringAssert.Contains("party is full", GameObject.Find("StatusText").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator WithNoRunInProgress_ShowsTheEmptyStateInsteadOfTheGrid()
        {
            yield return LoadScene();

            Assert.IsNotNull(GameObject.Find("EmptyStateText"), "the empty state should be showing");
            var grid = GameObject.Find("Canvas").transform.Find("SpeciesScroll/Viewport/Content");
            Assert.IsFalse(grid.gameObject.activeInHierarchy,
                "there's nothing to add mons to, so the grid shouldn't be offering any");
            Assert.IsFalse(FindButton("TargetPartyButton").interactable);
        }

        [UnityTest]
        public IEnumerator BackButton_ReturnsToTheIngameMenu()
        {
            yield return LoadScene();

            var back = FindButton("BackButton");
            Assert.AreEqual(1, back.onClick.GetPersistentEventCount());
            Assert.IsInstanceOf<SceneNavigator>(back.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(SceneNavigator.GoToIngameMenu), back.onClick.GetPersistentMethodName(0));
        }
    }
}
