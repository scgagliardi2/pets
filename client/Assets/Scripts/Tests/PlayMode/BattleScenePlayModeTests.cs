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

        // A node fight's foe, tuned so the fight is over in one Step and which way it went is never
        // in question: the weak one dies to a single hit without landing a meaningful one, the
        // strong one one-shots a party mon and shrugs off everything it takes.
        private const string WildName = "Wild";
        private const int WeakFoeAttack = 1;
        private const int WeakFoeHealth = 1;
        private const int StrongFoeAttack = TestHealth;
        private const int StrongFoeHealth = TestHealth * 10;

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
            // Static, so a node fight one test queued would otherwise be fought by the next one.
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
            // The two result buttons go through the controller, not straight to the navigator:
            // where they lead depends on how the fight ended and whether it was a node fight.
            AssertWired<SceneNavigator>("ResultBackButton", nameof(SceneNavigator.GoToTeam));
            AssertWired<SceneNavigator>("BattleAgainButton", nameof(SceneNavigator.GoToBattle));
            // The one button whose destination isn't fixed — it depends on how the node fight ended.
            AssertWired<BattleScreenController>("ResultActionButton", nameof(BattleScreenController.OnResultActionClicked));
            AssertWired<BattleScreenController>("PauseButton", nameof(BattleScreenController.OnPauseClicked));
            AssertWired<BattleScreenController>("StepButton", nameof(BattleScreenController.OnStepClicked));
            AssertWired<BattleScreenController>("PlayButton", nameof(BattleScreenController.OnPlayClicked));
            AssertWired<BattleScreenController>("SkipButton", nameof(BattleScreenController.OnSkipClicked));
        }

        /// <summary>A fight a map node started, rather than the dev random battle: the encounter is
        /// the one handed over, passives are left on (the dev battle strips them), and winning pays
        /// the run in EXP and offers the defeated wild mons to catch.</summary>
        [UnityTest]
        public IEnumerator NodeFight_FightsTheHandedOverTeam_WithPassivesLeftOn()
        {
            BeginNodeFight(WeakFoeAttack, WeakFoeHealth);

            yield return LoadScene(BattleScenePath);
            var controller = Controller();

            Assert.AreEqual(1, controller.State.LineUpB.Count, "the foe team is the one the node handed over");
            Assert.AreEqual(WildName, Stats("EnemyLeadStats").NameText.text);
            Assert.IsNotNull(controller.State.LineUpB[0].ResolvedPassive, "a node fight keeps its passives");
            Assert.IsFalse(PendingBattle.HasPending, "the pending fight is consumed once it starts");
            Assert.IsFalse(FindButton("BackButton", includeInactive: true).gameObject.activeInHierarchy,
                "a node fight is committed — there's no leaving it half-fought");
        }

        [UnityTest]
        public IEnumerator NodeFight_Won_GrantsExp_OffersACatch_AndContinuesToTheMap()
        {
            var run = BeginNodeFight(WeakFoeAttack, WeakFoeHealth);

            yield return LoadScene(BattleScenePath);
            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(BattleOutcome.SideAWins, Controller().Outcome);
            // StartsWith, not equality: the result panel now reports the EXP the win paid (and any
            // evolution it set off) under the outcome line.
            StringAssert.StartsWith("Victory!", GameObject.Find("ResultText").GetComponent<Text>().text);
            StringAssert.Contains($"gains {BattleRewardResolver.ExpPerWin} EXP",
                GameObject.Find("ResultText").GetComponent<Text>().text);
            Assert.AreEqual(3, run.Morale, "winning costs no Morale");
            Assert.IsTrue(run.LineUp.TrueForAll(m => m.Exp == BattleRewardResolver.ExpPerWin),
                "every mon in the line-up is paid in EXP, not just whoever was left standing");

            var catchButton = FindButton($"Catch_{WildName}");
            Assert.AreEqual("Continue", FindButton("ResultActionButton").GetComponentInChildren<Text>().text);
            foreach (string devOnly in new[] { "BattleAgainButton", "ResultBackButton" })
            {
                Assert.IsFalse(FindButton(devOnly, includeInactive: true).gameObject.activeInHierarchy,
                    $"{devOnly} belongs to the dev battle, not to a node fight");
            }

            catchButton.onClick.Invoke();
            Assert.AreEqual(1, run.Box.Count, "the caught mon joins the Box");

            FindButton("ResultActionButton").onClick.Invoke();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
            Assert.IsTrue(ActiveRun.HasRun, "the run carries on");
        }

        [UnityTest]
        public IEnumerator NodeFight_Lost_CostsMorale_AndLeavesTheRunGoing()
        {
            var run = BeginNodeFight(StrongFoeAttack, StrongFoeHealth);

            yield return LoadScene(BattleScenePath);
            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(BattleOutcome.SideBWins, Controller().Outcome);
            Assert.AreEqual(2, run.Morale, "a lost fight costs one Morale");
            Assert.IsFalse(run.IsRunOver);
            Assert.AreEqual(0, run.LineUp[0].Exp, "losing pays nothing");
            Assert.IsNull(FindCatchButton(), "nothing to catch out of a fight you lost");

            FindButton("ResultActionButton").onClick.Invoke();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
        }

        /// <summary>Morale is the run's life total (design doc §4): the loss that takes the last of
        /// it ends the run, and the way out is Home rather than back onto the map.</summary>
        [UnityTest]
        public IEnumerator NodeFight_LostWithNoMoraleLeft_EndsTheRunAtHome()
        {
            var run = BeginNodeFight(StrongFoeAttack, StrongFoeHealth);
            run.Morale = 1;

            yield return LoadScene(BattleScenePath);
            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(run.IsRunOver);
            StringAssert.Contains("run ends here", GameObject.Find("ResultText").GetComponent<Text>().text);

            FindButton("ResultActionButton").onClick.Invoke();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Home);
            Assert.IsFalse(ActiveRun.HasRun, "a broken run is over, not resumable");
        }

        [UnityTest]
        public IEnumerator GymFight_Won_CompletesTheLocation_AndEndsTheRunAtHome()
        {
            var run = BeginNodeFight(WeakFoeAttack, WeakFoeHealth, isGym: true);

            yield return LoadScene(BattleScenePath);
            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            StringAssert.Contains("Badge earned", GameObject.Find("ResultText").GetComponent<Text>().text);
            Assert.AreEqual(BattleRewardResolver.ExpPerWin, run.LineUp[0].Exp,
                "a Gym pays the same flat EXP as any other win");
            Assert.IsNull(FindCatchButton(), "a Gym Leader's team isn't wildlife to catch");

            FindButton("ResultActionButton").onClick.Invoke();
            // No Region Hub to pick the next Location from yet — see ADR 0003.
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Home);
            Assert.IsFalse(ActiveRun.HasRun);
        }

        /// <summary>The Gym is every path's terminus, so a loss there can't send the player back to a
        /// map with nowhere left to walk — it offers the fight again instead.</summary>
        [UnityTest]
        public IEnumerator GymFight_Lost_OffersTheFightAgain()
        {
            var run = BeginNodeFight(StrongFoeAttack, StrongFoeHealth, isGym: true);

            yield return LoadScene(BattleScenePath);
            FindButton("SkipButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, run.Morale);
            var retry = FindButton("ResultActionButton");
            Assert.AreEqual("Try Again", retry.GetComponentInChildren<Text>().text,
                "back to the map would be a dead end, so the Gym offers itself again");

            retry.onClick.Invoke();
            Assert.IsTrue(PendingBattle.HasPending, "the retry queues another Gym fight");
            Assert.IsTrue(PendingBattle.IsGym);
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

        private static PokemonSpeciesLibrary MakeLibrary(params PokemonSpeciesDefinitionAsset[] extra)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = new List<PokemonSpeciesDefinitionAsset>();
            for (int i = 0; i < MonNames.Length; i++)
            {
                library.AllSpecies.Add(MakeSpecies(i + 1, MonNames[i]));
            }
            library.AllSpecies.AddRange(extra);
            return library;
        }

        /// <summary>Queues the fight a map node would have queued: a single wild mon of the given
        /// stats, with a passive on it (a node fight keeps passives — the dev battle strips them),
        /// and a library that can name it so the screen and the Box both recognise it. The wild
        /// species is only ever added for these tests, so a random dev battle can't roll it.</summary>
        private static RunState BeginNodeFight(int foeAttack, int foeHealth, bool isGym = false)
        {
            var run = MakeRun(1);
            var wild = MakeSpecies(MonNames.Length + 1, WildName);
            wild.BaseAttack = foeAttack;
            wild.BaseHealth = foeHealth;
            wild.Passive = MakePassive();

            ActiveRun.Begin(run, MakeLibrary(wild));
            PendingBattle.Set(
                new List<PokemonInstance> { PokemonInstanceFactory.Create(wild, "wild-0") },
                isGym ? "L6-0" : "L1-0",
                isGym,
                seed: 1234);
            return run;
        }

        /// <summary>Effect-free on purpose: it proves the passive survived line-up assembly without
        /// changing how the fight goes.</summary>
        private static PassiveDefinitionAsset MakePassive()
        {
            var passive = ScriptableObject.CreateInstance<PassiveDefinitionAsset>();
            passive.Id = "test-passive";
            passive.DisplayName = "Test Passive";
            return passive;
        }

        /// <summary>The catch offer for the test's wild mon, or null when none was made.</summary>
        private static Button FindCatchButton() =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == $"Catch_{WildName}");

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
