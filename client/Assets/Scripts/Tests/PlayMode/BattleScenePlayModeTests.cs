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
    /// random enemy team, the stat boxes and party strip, the 2-second HP drain, the playback
    /// controls, the autoplay setting and the end-of-fight panel.
    ///
    /// Every test species has 10 attack and 200 HP, so a single Step is predictable (both Leads
    /// lose exactly 10) and a fight lasts long enough that autoplay can't have finished it before
    /// the test takes control. Autoplay is switched off by default here, and the player's own
    /// setting is put back afterwards — PlayerPrefs in the Editor are the real ones.</summary>
    public class BattleScenePlayModeTests
    {
        private const string BattleScenePath = "Assets/Scenes/Battle.unity";
        private const int TestAttack = 10;
        private const int TestHealth = 200;

        private static readonly string[] MonNames = { "Alpha", "Beta", "Gamma", "Delta" };

        private bool hadAutoplaySetting;
        private bool savedAutoplaySetting;

        [SetUp]
        public void SetUp()
        {
            ResetRunState();
            hadAutoplaySetting = PlayerPrefs.HasKey(GameSettings.AutoplayBattlesKey);
            savedAutoplaySetting = GameSettings.AutoplayBattles;
            GameSettings.AutoplayBattles = false;
        }

        [TearDown]
        public void TearDown()
        {
            ResetRunState();
            if (hadAutoplaySetting)
            {
                GameSettings.AutoplayBattles = savedAutoplaySetting;
            }
            else
            {
                PlayerPrefs.DeleteKey(GameSettings.AutoplayBattlesKey);
            }
        }

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
        public IEnumerator BattleScene_WithAParty_ShowsBothSidesAndThePartyStrip()
        {
            ActiveRun.Begin(MakeRun(3), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            var controller = Controller();
            Assert.IsNotNull(controller.State, "a fight should have started");
            Assert.AreEqual(3, controller.State.LineUpA.Count);
            Assert.AreEqual(3, controller.State.LineUpB.Count, "the foe team matches the party's size");

            Assert.AreEqual("Alpha", Stats("PlayerLeadStats").NameText.text);
            Assert.AreEqual("Beta", Stats("PlayerSupportStats").NameText.text);
            CollectionAssert.Contains(MonNames, Stats("EnemyLeadStats").NameText.text, "the foe is rolled from the library");
            CollectionAssert.Contains(MonNames, Stats("EnemySupportStats").NameText.text);
            Assert.AreEqual($"{TestHealth}/{TestHealth}", Stats("PlayerLeadStats").HealthBar.ValueLabel.text);
            Assert.AreEqual(TestAttack.ToString(), Stats("PlayerLeadStats").AttackText.text);

            foreach (var sprite in new[] { "PlayerLeadSprite", "PlayerSupportSprite", "EnemyLeadSprite", "EnemySupportSprite" })
            {
                Assert.IsTrue(GameObject.Find(sprite).GetComponent<Image>().enabled, $"{sprite} should be standing on the field");
            }

            Assert.AreEqual(PartySlotRole.Lead, Slot(0).Role);
            Assert.AreEqual(PartySlotRole.Support, Slot(1).Role);
            Assert.AreEqual(PartySlotRole.Reserve, Slot(2).Role);
            Assert.AreEqual(PartySlotRole.Empty, Slot(5).Role, "six slots are always drawn, empty past the party");
            Assert.AreEqual("Gamma", Slot(2).NameText.text);
            Assert.AreSame(Theme.SlotGoldSprite, Slot(0).Frame.sprite, "the Lead is framed gold");
            Assert.AreSame(Theme.SlotBlueSprite, Slot(1).Frame.sprite, "the Support is framed blue");

            Assert.IsFalse(FindButton("ThrowButton").interactable, "catching isn't built yet");
            Assert.IsFalse(GameObject.Find("ResultPanel"), "no result before the fight ends");
        }

        [UnityTest]
        public IEnumerator AutoplaySetting_On_StartsTheBattlePlaying()
        {
            GameSettings.AutoplayBattles = true;
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            Assert.IsTrue(Controller().IsAutoplaying);
            Assert.IsTrue(FindButton("PauseButton").interactable);
            Assert.IsFalse(FindButton("PlayButton").interactable);
        }

        [UnityTest]
        public IEnumerator AutoplaySetting_Off_WaitsForThePlayer()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();

            Assert.IsFalse(controller.IsAutoplaying);
            Assert.AreEqual(0, controller.State.StepNumber);
            Assert.IsTrue(FindButton("PlayButton").interactable);

            FindButton("PlayButton").onClick.Invoke();
            Assert.IsTrue(controller.IsAutoplaying);
            FindButton("PauseButton").onClick.Invoke();
            Assert.IsFalse(controller.IsAutoplaying);
        }

        /// <summary>The drain is the point of this test: a Step's damage is applied at once, but the
        /// bar and its readout count down to it over the drain time rather than jumping.</summary>
        [UnityTest]
        public IEnumerator StepButton_DrainsBothLeadsHp_OverTwoSeconds()
        {
            var run = MakeRun(2);
            ActiveRun.Begin(run, MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();
            var playerBar = Stats("PlayerLeadStats").HealthBar;
            var enemyBar = Stats("EnemyLeadStats").HealthBar;

            float started = Time.realtimeSinceStartup;
            FindButton("StepButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, controller.State.StepNumber);
            Assert.IsTrue(playerBar.IsAnimating, "the player's HP should be draining");
            Assert.IsTrue(enemyBar.IsAnimating, "the foe's HP should be draining");
            Assert.AreEqual(TestHealth - TestAttack, playerBar.Current, "the drain is heading for the Step's result");
            Assert.Greater(playerBar.DisplayedHealth, TestHealth - TestAttack, "but hasn't got there yet");
            Assert.AreEqual($"-{TestAttack}", GameObject.Find("PlayerLeadSprite").GetComponentInChildren<Text>().text);
            Assert.IsTrue(Slot(0).IsAnimating, "the party strip drains along with the box");

            yield return SceneTransitionWait.UntilWithinSeconds(() => !controller.IsAnimating,
                "the Step should finish drawing", controller.HpDrainSeconds + 3f);

            Assert.GreaterOrEqual(Time.realtimeSinceStartup - started, controller.HpDrainSeconds - 0.1f,
                "the drain should take the full drain time");
            Assert.AreEqual($"{TestHealth - TestAttack}/{TestHealth}", playerBar.ValueLabel.text);
            Assert.AreEqual(TestHealth - TestAttack, enemyBar.DisplayedHealth);
            Assert.AreEqual((TestHealth - TestAttack) / (float)TestHealth, Slot(0).HealthFraction, 0.001f);
            Assert.AreEqual(string.Empty, GameObject.Find("PlayerLeadSprite").GetComponentInChildren<Text>().text,
                "the damage number clears once the Step is drawn");
            Assert.AreEqual(TestHealth, run.LineUp[0].CurrentHP, "the battle must not write damage back to the run");
        }

        /// <summary>Identical species on both sides trade identical blows, so the last mons fall
        /// together — a Draw.</summary>
        [UnityTest]
        public IEnumerator SkipButton_FinishesTheFight_AndShowsTheResultPanel()
        {
            ActiveRun.Begin(MakeRun(1), MakeLibrary());

            yield return LoadScene(BattleScenePath);
            var controller = Controller();

            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(BattleOutcome.Draw, controller.Outcome);
            Assert.IsNotNull(GameObject.Find("ResultPanel"), "the result panel should be up");
            Assert.AreEqual("Draw", GameObject.Find("ResultText").GetComponent<Text>().text);
            Assert.IsTrue(FindButton("BattleAgainButton").gameObject.activeInHierarchy);
            foreach (var control in new[] { "PauseButton", "StepButton", "PlayButton", "SkipButton" })
            {
                Assert.IsFalse(FindButton(control).interactable, $"{control} has nothing left to do");
            }
            Assert.AreEqual("No Lead", Stats("PlayerLeadStats").NameText.text, "an emptied side draws an empty box");
            Assert.IsFalse(GameObject.Find("PlayerLeadSprite").GetComponent<Image>().enabled);
            Assert.AreEqual(PartySlotRole.Fainted, Slot(0).Role);
            Assert.AreEqual(0f, Slot(0).HealthFraction);
        }

        [UnityTest]
        public IEnumerator BattleScene_ButtonsAreWired()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(BattleScenePath);

            AssertWired<SceneNavigator>("BackButton", nameof(SceneNavigator.GoToTeam));
            AssertWired<SceneNavigator>("ResultBackButton", nameof(SceneNavigator.GoToTeam));
            AssertWired<SceneNavigator>("BattleAgainButton", nameof(SceneNavigator.GoToBattle));
            AssertWired<BattleScreenController>("PauseButton", nameof(BattleScreenController.OnPauseClicked));
            AssertWired<BattleScreenController>("StepButton", nameof(BattleScreenController.OnStepClicked));
            AssertWired<BattleScreenController>("PlayButton", nameof(BattleScreenController.OnPlayClicked));
            AssertWired<BattleScreenController>("SkipButton", nameof(BattleScreenController.OnSkipClicked));
        }

        private static BattleScreenController Controller()
        {
            var controller = Object.FindFirstObjectByType<BattleScreenController>();
            Assert.IsNotNull(controller, "the Battle scene should have a BattleScreenController");
            return controller;
        }

        private static BattleStatsBoxView Stats(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"Expected a '{name}' stat box");
            return go.GetComponent<BattleStatsBoxView>();
        }

        private static BattlePartySlotView Slot(int index)
        {
            var go = GameObject.Find($"PartySlot{index}");
            Assert.IsNotNull(go, $"Expected PartySlot{index} in the party strip");
            return go.GetComponent<BattlePartySlotView>();
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
