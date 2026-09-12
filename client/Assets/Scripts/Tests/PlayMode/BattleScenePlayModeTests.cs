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
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>The saved Battle scene against BattleScreenController: the no-party redirect, the
    /// random enemy team, the step/skip controls and the end-of-fight buttons.
    ///
    /// Every test species has 10 attack and 200 HP, so a single Step is predictable (both Leads
    /// lose exactly 10) and a fight lasts long enough that autoplay can't have finished it before
    /// the test takes control.</summary>
    public class BattleScenePlayModeTests
    {
        private const string BattleScenePath = "Assets/Scenes/Battle.unity";
        private const int TestAttack = 10;
        private const int TestHealth = 200;

        private static readonly string[] MonNames = { "Alpha", "Beta", "Gamma", "Delta" };

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

        [UnityTest]
        public IEnumerator BattleScene_WithNoRun_SendsThePlayerToCharacterSelect()
        {
            yield return LoadScene(BattleScenePath);

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.CharacterSelect);
        }

        /// <summary>A run with nobody in it is dropped on the way out, or the Map would adopt it
        /// over the pair the player is about to pick.</summary>
        [UnityTest]
        public IEnumerator BattleScene_WithAnEmptyParty_SendsThePlayerToCharacterSelect_AndDropsTheRun()
        {
            ActiveRun.Begin(new RunState(), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.CharacterSelect);
            Assert.IsFalse(ActiveRun.HasRun);
        }

        [UnityTest]
        public IEnumerator BattleScene_WithAParty_FacesAnEnemyTeamOfTheSameSize()
        {
            ActiveRun.Begin(MakeRun(3), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            var controller = Controller();
            Assert.IsNotNull(controller.State, "a fight should have started");
            Assert.AreEqual(3, controller.State.LineUpA.Count);
            Assert.AreEqual(3, controller.State.LineUpB.Count);
            Assert.AreEqual(SceneNames.Battle, SceneManager.GetActiveScene().name);

            Assert.AreEqual("Alpha", PanelName("PlayerLead"));
            Assert.AreEqual("Beta", PanelName("PlayerSupport"));
            CollectionAssert.Contains(MonNames, PanelName("EnemyLead"), "the foe should be rolled from the library");
            CollectionAssert.Contains(MonNames, PanelName("EnemySupport"));

            // Only the third mon on each side is dormant, drawn in the queue rather than as a card.
            Assert.AreEqual(1, ActiveChildCount("PlayerReserve"));
            Assert.AreEqual(1, ActiveChildCount("EnemyReserve"));
        }

        [UnityTest]
        public IEnumerator StepButton_PlaysOneStep_AndBothLeadsTradeAttacks()
        {
            var run = MakeRun(2);
            ActiveRun.Begin(run, MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();
            yield return PauseAndSettle(controller);

            int step = controller.State.StepNumber;
            int playerHp = PanelHealth("PlayerLead").Current;
            int enemyHp = PanelHealth("EnemyLead").Current;

            FindButton("StepButton").onClick.Invoke();
            yield return SceneTransitionWait.Until(() => !controller.IsAnimating, "the Step should finish drawing");

            Assert.AreEqual(step + 1, controller.State.StepNumber);
            Assert.AreEqual(playerHp - TestAttack, PanelHealth("PlayerLead").Current);
            Assert.AreEqual(enemyHp - TestAttack, PanelHealth("EnemyLead").Current);
            Assert.AreEqual($"-{TestAttack}", GameObject.Find("PlayerLead").GetComponentsInChildren<Text>()
                .First(t => t.name == "DamageText").text);
            Assert.AreEqual(TestHealth, run.LineUp[0].CurrentHP, "the battle must not write damage back to the run");
        }

        [UnityTest]
        public IEnumerator AutoplayButton_TogglesAutoplay()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();
            Assert.IsTrue(controller.IsAutoplaying, "a battle starts playing on its own");

            FindButton("AutoplayButton").onClick.Invoke();
            Assert.IsFalse(controller.IsAutoplaying);
            Assert.AreEqual("Autoplay", FindButton("AutoplayButton").GetComponent<UiButton>().Text);

            FindButton("AutoplayButton").onClick.Invoke();
            Assert.IsTrue(controller.IsAutoplaying);
            Assert.AreEqual("Pause", FindButton("AutoplayButton").GetComponent<UiButton>().Text);
        }

        /// <summary>Identical species on both sides trade identical blows, so the last mons fall
        /// together — a Draw, and a line-up change along the way on both sides.</summary>
        [UnityTest]
        public IEnumerator SkipButton_FinishesTheFight_AndOffersAnotherBattle()
        {
            ActiveRun.Begin(MakeRun(1), MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();

            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(BattleOutcome.Draw, controller.Outcome);
            Assert.AreEqual("Draw", GameObject.Find("ResultText").GetComponent<Text>().text);
            StringAssert.Contains("fainted!", GameObject.Find("LogText").GetComponent<Text>().text);
            Assert.IsFalse(controller.IsAutoplaying);
            Assert.IsFalse(FindButton("StepButton", includeInactive: true).gameObject.activeInHierarchy);
            Assert.IsFalse(FindButton("SkipButton", includeInactive: true).gameObject.activeInHierarchy);
            Assert.IsTrue(FindButton("BattleAgainButton").gameObject.activeInHierarchy);
            Assert.AreEqual("No Lead", PanelText("PlayerLead", "RoleText"), "an emptied side draws an empty frame");
        }

        [UnityTest]
        public IEnumerator BattleScene_ButtonsAreWired()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            AssertWired<SceneNavigator>("BackButton", nameof(SceneNavigator.GoToTeam));
            AssertWired<SceneNavigator>("BattleAgainButton", nameof(SceneNavigator.GoToBattle));
            AssertWired<BattleScreenController>("StepButton", nameof(BattleScreenController.OnStepClicked));
            AssertWired<BattleScreenController>("AutoplayButton", nameof(BattleScreenController.OnAutoplayClicked));
            AssertWired<BattleScreenController>("SkipButton", nameof(BattleScreenController.OnSkipClicked));
        }

        /// <summary>Pauses autoplay and waits out any Step it had already started, so the test
        /// owns the board from here.</summary>
        private static IEnumerator PauseAndSettle(BattleScreenController controller)
        {
            if (controller.IsAutoplaying)
            {
                FindButton("AutoplayButton").onClick.Invoke();
            }
            yield return SceneTransitionWait.Until(() => !controller.IsAnimating, "autoplay's Step should finish drawing");
        }

        private static BattleScreenController Controller()
        {
            var controller = Object.FindFirstObjectByType<BattleScreenController>();
            Assert.IsNotNull(controller, "the Battle scene should have a BattleScreenController");
            return controller;
        }

        private static void AssertWired<T>(string buttonName, string methodName)
        {
            var button = FindButton(buttonName, includeInactive: true);
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), $"{buttonName} should have one persistent listener");
            Assert.IsInstanceOf<T>(button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(methodName, button.onClick.GetPersistentMethodName(0));
        }

        private static Button FindButton(string name, bool includeInactive = false)
        {
            var buttons = Object.FindObjectsByType<Button>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var button = buttons.FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"Expected a Button named '{name}' in the Battle scene");
            return button;
        }

        private static string PanelName(string slot) => PanelText(slot, "NameRow/Name");

        private static string PanelText(string slot, string path)
        {
            var slotGo = GameObject.Find(slot);
            Assert.IsNotNull(slotGo, $"Expected a '{slot}' slot");
            var line = slotGo.transform.Find($"Card/{path}");
            Assert.IsNotNull(line, $"{slot} has no Card/{path}");
            return line.GetComponent<Text>().text;
        }

        private static HealthBarView PanelHealth(string slot) =>
            GameObject.Find(slot).GetComponentInChildren<HealthBarView>();

        private static int ActiveChildCount(string rowName)
        {
            var row = GameObject.Find(rowName).transform;
            int count = 0;
            for (int i = 0; i < row.childCount; i++)
            {
                if (row.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = TestAttack;
            species.BaseHealth = TestHealth;
            species.BaseSpeed = 10;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary()
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = new List<PokemonSpeciesDefinitionAsset>();
            for (int i = 0; i < MonNames.Length; i++)
            {
                library.AllSpecies.Add(MakeSpecies(i + 1, MonNames[i]));
            }
            return library;
        }

        private static RunState MakeRun(int partyCount)
        {
            var state = new RunState();
            for (int i = 0; i < partyCount; i++)
            {
                state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(i + 1, MonNames[i]), $"mon-{i}"));
            }
            return state;
        }
    }
}
