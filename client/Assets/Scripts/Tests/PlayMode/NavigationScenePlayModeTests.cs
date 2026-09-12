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
    /// <summary>Covers the screens a player moves between outside a fight — Home, the Map's way
    /// into the Ingame Menu, the menu itself, and Team — against the actual saved scenes.
    ///
    /// Navigation is wired as persistent onClick listeners by the scene builders, so most of these
    /// assert on the wiring (target component + method name) rather than clicking through: a click
    /// tears down the running scene mid-test, and the thing that actually breaks is a builder
    /// wiring a renamed or missing method. The Team assertions do drive real clicks, since that
    /// screen changes state in place.</summary>
    public class NavigationScenePlayModeTests
    {
        private const string HomeScenePath = "Assets/Scenes/Home.unity";
        private const string IngameMenuScenePath = "Assets/Scenes/IngameMenu.unity";
        private const string RegionMapScenePath = "Assets/Scenes/RegionMap.unity";
        private const string TeamScenePath = "Assets/Scenes/Team.unity";

        [TearDown]
        public void TearDown()
        {
            // ActiveRun is static and outlives the scene, so a run seeded by one test would
            // otherwise decide what the next one's Team screen shows.
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

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"Expected a Button named '{name}' in the loaded scene");
            return button;
        }

        /// <summary>Asserts the button's baked onClick calls <paramref name="methodName"/> on a
        /// SceneNavigator. Covers both halves of what a scene builder can get wrong: forgetting
        /// the listener at all, and pointing it at a method that no longer exists.</summary>
        private static void AssertNavigatesVia(string buttonName, string methodName)
        {
            var button = FindButton(buttonName);
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(),
                $"{buttonName} should have exactly one persistent onClick listener");
            Assert.IsInstanceOf<SceneNavigator>(button.onClick.GetPersistentTarget(0),
                $"{buttonName} should be wired to the scene's SceneNavigator");
            Assert.AreEqual(methodName, button.onClick.GetPersistentMethodName(0));
            Assert.IsNotNull(typeof(SceneNavigator).GetMethod(methodName),
                $"SceneNavigator.{methodName} should exist — the scene builder wires it by name");
        }

        [UnityTest]
        public IEnumerator HomeScene_NewGameAndQuitButtons_AreWiredToTheNavigator()
        {
            yield return LoadScene(HomeScenePath);

            AssertNavigatesVia("NewGameButton", nameof(SceneNavigator.StartNewGame));
            AssertNavigatesVia("QuitButton", nameof(SceneNavigator.QuitGame));
        }

        /// <summary>"New Game" has to mean a new game: a run left in ActiveRun by a previous
        /// session would otherwise be adopted by the Map's RunBootstrapper and the player would
        /// land back in it with someone else's line-up after picking fresh starters.</summary>
        [UnityTest]
        public IEnumerator HomeScene_StartNewGame_DropsAnyRunInProgress()
        {
            yield return LoadScene(HomeScenePath);
            ActiveRun.Begin(MakeRun(), MakeLibrary());
            Assert.IsTrue(ActiveRun.HasRun);

            Object.FindFirstObjectByType<SceneNavigator>().StartNewGame();
            yield return null;

            Assert.IsFalse(ActiveRun.HasRun, "StartNewGame should clear the run in progress");
            Assert.AreEqual(SceneNames.CharacterSelect, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator IngameMenuScene_ButtonsReachMapTeamAndHome()
        {
            yield return LoadScene(IngameMenuScenePath);

            AssertNavigatesVia("MapButton", nameof(SceneNavigator.GoToMap));
            AssertNavigatesVia("TeamButton", nameof(SceneNavigator.GoToTeam));
            AssertNavigatesVia("HomeButton", nameof(SceneNavigator.GoHome));
        }

        [UnityTest]
        public IEnumerator RegionMapScene_MenuButton_OpensTheIngameMenu()
        {
            yield return LoadScene(RegionMapScenePath);

            AssertNavigatesVia("MenuButton", nameof(SceneNavigator.GoToIngameMenu));
        }

        /// <summary>The Map is where a run is created now that Character Select hands off to it,
        /// so its bootstrapper has to publish that run for the Team screen in the next scene.</summary>
        [UnityTest]
        public IEnumerator RegionMapScene_BootstrapsARunAndPublishesItToActiveRun()
        {
            yield return LoadScene(RegionMapScenePath);

            Assert.IsTrue(ActiveRun.HasRun, "Loading the Map should start a run");
            Assert.AreEqual(2, ActiveRun.State.LineUp.Count);
            Assert.IsNotNull(ActiveRun.Library);
            Assert.AreSame(RunBootstrapper.Instance.State, ActiveRun.State,
                "The scene's bootstrapper and ActiveRun should be looking at the same run");
        }

        [UnityTest]
        public IEnumerator TeamScene_ShowsTheActiveRunsLineUp_AndSwapReordersIt()
        {
            var library = MakeLibrary();
            ActiveRun.Begin(MakeRun(), library);

            yield return LoadScene(TeamScenePath);

            var lineUpText = GameObject.Find("LineUpText").GetComponent<Text>();
            StringAssert.Contains("Lead: Alpha", lineUpText.text);
            StringAssert.Contains("Support: Beta", lineUpText.text);

            var swapButton = FindButton("SwapButton");
            Assert.IsTrue(swapButton.interactable, "Swap should be available with two mons in the line-up");
            swapButton.onClick.Invoke();
            yield return null;

            StringAssert.Contains("Lead: Beta", lineUpText.text);
            StringAssert.Contains("Support: Alpha", lineUpText.text);
        }

        [UnityTest]
        public IEnumerator TeamScene_BackButton_ReturnsToTheIngameMenu()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            AssertNavigatesVia("BackButton", nameof(SceneNavigator.GoToIngameMenu));
        }

        /// <summary>Opening Team.unity directly (no run) is routine while working on the scene, so
        /// it has to explain itself rather than throw on a null RunState.</summary>
        [UnityTest]
        public IEnumerator TeamScene_WithNoRunInProgress_ShowsTheEmptyState()
        {
            yield return LoadScene(TeamScenePath);

            var emptyState = GameObject.Find("EmptyStateText");
            Assert.IsNotNull(emptyState, "EmptyStateText should be active when there's no run");
            Assert.IsNull(GameObject.Find("LineUpText"),
                "The team readout should be hidden when there's no run to read out");
            Assert.IsFalse(FindButton("SwapButton").interactable);
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
            library.AllSpecies = new System.Collections.Generic.List<PokemonSpeciesDefinitionAsset>
            {
                MakeSpecies(1, "Alpha"),
                MakeSpecies(2, "Beta")
            };
            return library;
        }

        private static RunState MakeRun()
        {
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(1, "Alpha"), "lead"));
            state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(2, "Beta"), "support"));
            return state;
        }
    }
}
