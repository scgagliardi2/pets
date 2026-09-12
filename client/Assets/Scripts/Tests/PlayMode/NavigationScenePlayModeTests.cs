using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
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
    /// <summary>Covers the screens a player moves between outside a fight — Home, History,
    /// Credits, the Map's way into the Ingame Menu, the menu itself, and Team — against the actual
    /// saved scenes.
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
        private const string HistoryScenePath = "Assets/Scenes/History.unity";
        private const string CreditsScenePath = "Assets/Scenes/Credits.unity";

        // ActiveRun is static and outlives both the scene and the fixture, so a run seeded
        // anywhere earlier in the PlayMode session would otherwise decide what this fixture's Home
        // and Team screens show. Cleared before each test as well as after, since the tests that
        // assert on *not* having a run can't rely on whichever fixture ran before this one having
        // tidied up after itself.
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

        private static Button FindButton(string name, bool includeInactive = false)
        {
            var buttons = includeInactive
                ? Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                : Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var button = buttons.FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"Expected a Button named '{name}' in the loaded scene");
            return button;
        }

        /// <summary>Asserts the button's baked onClick calls <paramref name="methodName"/> on a
        /// SceneNavigator. Covers both halves of what a scene builder can get wrong: forgetting
        /// the listener at all, and pointing it at a method that no longer exists.</summary>
        private static void AssertNavigatesVia(string buttonName, string methodName)
        {
            var button = FindButton(buttonName, includeInactive: true);
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(),
                $"{buttonName} should have exactly one persistent onClick listener");
            Assert.IsInstanceOf<SceneNavigator>(button.onClick.GetPersistentTarget(0),
                $"{buttonName} should be wired to the scene's SceneNavigator");
            Assert.AreEqual(methodName, button.onClick.GetPersistentMethodName(0));
            Assert.IsNotNull(typeof(SceneNavigator).GetMethod(methodName),
                $"SceneNavigator.{methodName} should exist — the scene builder wires it by name");
        }

        [UnityTest]
        public IEnumerator HomeScene_EveryMenuButton_IsWiredToTheNavigator()
        {
            yield return LoadScene(HomeScenePath);

            AssertNavigatesVia("NewGameButton", nameof(SceneNavigator.StartNewGame));
            AssertNavigatesVia("ContinueButton", nameof(SceneNavigator.ContinueRun));
            AssertNavigatesVia("HistoryButton", nameof(SceneNavigator.GoToHistory));
            AssertNavigatesVia("CreditsButton", nameof(SceneNavigator.GoToCredits));
            AssertNavigatesVia("SettingsButton", nameof(SceneNavigator.GoToSettings));
            AssertNavigatesVia("QuitButton", nameof(SceneNavigator.QuitGame));
        }

        /// <summary>Continue is the one thing on Home that depends on state, and it's hidden
        /// rather than disabled — on a first launch there's nothing to continue into, and a
        /// greyed-out button would only raise a question the player can't act on.</summary>
        [UnityTest]
        public IEnumerator HomeScene_WithNoRunInProgress_HidesContinue()
        {
            yield return LoadScene(HomeScenePath);

            Assert.IsFalse(FindButton("ContinueButton", includeInactive: true).gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator HomeScene_WithARunInProgress_ShowsContinue()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(HomeScenePath);

            Assert.IsTrue(FindButton("ContinueButton").gameObject.activeInHierarchy);
        }

        /// <summary>Guarded in the navigator as well as hidden on screen: the button is the only
        /// caller today, but a run that has since been cleared must not load the Ingame Menu onto
        /// a null RunState.</summary>
        [UnityTest]
        public IEnumerator ContinueRun_WithNoRunInProgress_StaysOnHome()
        {
            yield return LoadScene(HomeScenePath);

            Object.FindFirstObjectByType<SceneNavigator>().ContinueRun();
            yield return null;

            Assert.AreEqual(SceneNames.Home, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator HistoryScene_BackButton_ReturnsHome()
        {
            yield return LoadScene(HistoryScenePath);

            AssertNavigatesVia("BackButton", nameof(SceneNavigator.GoHome));
        }

        [UnityTest]
        public IEnumerator CreditsScene_BackButton_ReturnsHome()
        {
            yield return LoadScene(CreditsScenePath);

            AssertNavigatesVia("BackButton", nameof(SceneNavigator.GoHome));
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

            // Cleared synchronously, before the transition starts — that's the part that matters
            // here, since the Map's RunBootstrapper adopts whatever ActiveRun holds.
            Assert.IsFalse(ActiveRun.HasRun, "StartNewGame should clear the run in progress");

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.CharacterSelect);
        }

        [UnityTest]
        public IEnumerator IngameMenuScene_ButtonsReachMapTeamAndHome()
        {
            yield return LoadScene(IngameMenuScenePath);

            AssertNavigatesVia("MapButton", nameof(SceneNavigator.GoToMap));
            AssertNavigatesVia("TeamButton", nameof(SceneNavigator.GoToTeam));
            AssertNavigatesVia("DevRosterButton", nameof(SceneNavigator.GoToDevRoster));
            AssertNavigatesVia("SettingsButton", nameof(SceneNavigator.GoToSettings));
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
        public IEnumerator TeamScene_ShowsTheActiveRunsPartyInSlots_AndSwapReordersIt()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            Assert.AreEqual("Alpha", SlotText("PartySlot0", "NameText"));
            Assert.AreEqual("Beta", SlotText("PartySlot1", "NameText"));
            StringAssert.StartsWith("Lead", SlotText("PartySlot0", "RoleText"));
            StringAssert.StartsWith("Support", SlotText("PartySlot1", "RoleText"));
            StringAssert.Contains("ATK 10", SlotText("PartySlot0", "StatsText"));

            var swapButton = FindButton("SwapButton");
            Assert.IsTrue(swapButton.interactable, "Swap should be available with two mons in the line-up");
            swapButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual("Beta", SlotText("PartySlot0", "NameText"));
            Assert.AreEqual("Alpha", SlotText("PartySlot1", "NameText"));
        }

        /// <summary>Six party slots and six Box slots are always drawn, filled or not: the point
        /// of showing the whole row is that a player can see the room they have, and the empty
        /// ones still say what they'd be.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DrawsSixPartyAndSixBoxSlots_WithTheUnfilledOnesEmpty()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            for (int i = 0; i < TeamPanelController.SlotsPerRow; i++)
            {
                Assert.IsNotNull(GameObject.Find($"PartySlot{i}"), $"PartySlot{i} should be drawn");
                Assert.IsNotNull(GameObject.Find($"BoxSlot{i}"), $"BoxSlot{i} should be drawn");
            }

            // The run's two mons fill Lead and Support; everything behind them is a labelled
            // reserve slot with no mon in it, and the Box is empty in a fresh run.
            Assert.IsNull(GameObject.Find("PartySlot2").transform.Find("Card/NameText"),
                "an unfilled party slot should hold no mon");
            StringAssert.StartsWith("Reserve", SlotText("PartySlot2", "RoleText"));
            Assert.AreEqual("Empty", SlotText("BoxSlot0", "RoleText"));

            // A filled slot shows its mon the way a Character Select card does — same builder, so
            // this is really checking the slot asked for the type row at all.
            var typesRow = GameObject.Find("PartySlot0").transform.Find("Card/TypesRow");
            Assert.IsNotNull(typesRow, "a filled party slot should show its type icons");
            // Active children, not all children: the row always builds both icon slots and hides
            // the second one, so it can be re-pointed at another species without instantiating
            // (PokemonCardBuilder.TypeIconRow). What matters is how many the player sees.
            int visibleIcons = 0;
            for (int i = 0; i < typesRow.childCount; i++)
            {
                if (typesRow.GetChild(i).gameObject.activeSelf)
                {
                    visibleIcons++;
                }
            }
            Assert.AreEqual(1, visibleIcons, "the test species is single-typed");
        }

        /// <summary>Dragging a card onto another slot is how the line-up is reordered, so this
        /// drives the real drag handlers on the real slots rather than calling RunState.MoveMon
        /// (RunMetaTests covers the rules themselves). Three mons so the party has both a filled
        /// slot to trade with and empty ones to drop onto.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DraggingAPartyCardOntoAnotherSlot_ReordersTheLineUp()
        {
            ActiveRun.Begin(MakeRun(3), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return Drag("PartySlot0", "PartySlot2");

            Assert.AreEqual("Gamma", SlotText("PartySlot0", "NameText"), "the third mon should now lead");
            Assert.AreEqual("Alpha", SlotText("PartySlot2", "NameText"));
            Assert.AreEqual(3, ActiveRun.State.LineUp.Count, "a trade shouldn't change the party size");
        }

        [UnityTest]
        public IEnumerator TeamScene_DraggingAPartyCardIntoTheBox_MovesItOutOfTheLineUp()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return Drag("PartySlot1", "BoxSlot0");

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count);
            Assert.AreEqual(1, ActiveRun.State.Box.Count);
            Assert.AreEqual("Beta", SlotText("BoxSlot0", "NameText"));
            Assert.IsNull(GameObject.Find("PartySlot1").transform.Find("Card/NameText"),
                "the slot the mon left should be drawn empty again");
            Assert.IsFalse(FindButton("SwapButton").interactable,
                "a one-mon line-up has nothing to swap");
        }

        /// <summary>The one hard rule on the screen: a run always keeps someone to send out, so
        /// the drag is refused and the card goes back where it was.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DraggingTheLastPartyMonIntoTheBox_IsRefused()
        {
            ActiveRun.Begin(MakeRun(1), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return Drag("PartySlot0", "BoxSlot0");

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count, "the party must never be emptied");
            Assert.IsEmpty(ActiveRun.State.Box);
            Assert.AreEqual("Alpha", SlotText("PartySlot0", "NameText"),
                "the refused card should be back in its own slot");
        }

        /// <summary>A card let go over nothing has to land back in its slot rather than stay
        /// stranded on the drag layer.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DroppingACardOnNothing_LeavesEverythingWhereItWas()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            var source = SlotView("PartySlot0");
            var eventData = new PointerEventData(EventSystem.current) { pointerDrag = source.gameObject };
            source.OnBeginDrag(eventData);
            source.OnDrag(eventData);
            source.OnEndDrag(eventData);
            yield return null;

            Assert.AreEqual("Alpha", SlotText("PartySlot0", "NameText"));
            Assert.AreEqual("Beta", SlotText("PartySlot1", "NameText"));
            Assert.AreEqual(0, GameObject.Find("DragLayer").transform.childCount,
                "no card should be left on the drag layer");
        }

        /// <summary>An empty slot can be dropped onto but not picked up.</summary>
        [UnityTest]
        public IEnumerator TeamScene_AnEmptySlot_CannotBeDragged()
        {
            ActiveRun.Begin(MakeRun(1), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            Assert.IsFalse(SlotView("PartySlot3").IsFilled);

            yield return Drag("PartySlot3", "PartySlot0");

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count);
            Assert.AreEqual("Alpha", SlotText("PartySlot0", "NameText"));
        }

        /// <summary>Releasing is irreversible, so the drop only asks the question — nothing leaves
        /// the run until the confirmation is accepted.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DroppingACardOnTheReleaseZone_AsksBeforeReleasing()
        {
            ActiveRun.Begin(MakeRun(3), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return DragToReleaseZone("PartySlot1");

            var dialog = GameObject.Find("ReleaseConfirm");
            Assert.IsNotNull(dialog, "the confirmation should be showing");
            StringAssert.Contains("Beta", GameObject.Find("MessageText").GetComponent<Text>().text);
            Assert.AreEqual(3, ActiveRun.State.LineUp.Count, "nothing should be released until it's confirmed");

            FindButton("ReleaseConfirmButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, ActiveRun.State.LineUp.Count);
            CollectionAssert.DoesNotContain(
                ActiveRun.State.LineUp.Select(m => m.SpeciesId).ToList(), 2, "Beta should be gone from the run");
            Assert.IsEmpty(ActiveRun.State.Box, "a release is not a move to the Box");
            Assert.AreEqual("Gamma", SlotText("PartySlot1", "NameText"), "the row should have closed up");
            Assert.IsNull(GameObject.Find("ReleaseConfirm"), "the confirmation should be closed again");
        }

        [UnityTest]
        public IEnumerator TeamScene_CancellingARelease_KeepsTheMon()
        {
            ActiveRun.Begin(MakeRun(2), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return DragToReleaseZone("PartySlot0");
            FindButton("ReleaseCancelButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, ActiveRun.State.LineUp.Count);
            Assert.AreEqual("Alpha", SlotText("PartySlot0", "NameText"));
            Assert.IsNull(GameObject.Find("ReleaseConfirm"));
        }

        [UnityTest]
        public IEnumerator TeamScene_ReleasingFromTheBox_Works()
        {
            var run = MakeRun(2);
            run.MoveMon(RosterGroup.Party, 1, RosterGroup.Box, 0);
            ActiveRun.Begin(run, MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return DragToReleaseZone("BoxSlot0");
            FindButton("ReleaseConfirmButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count);
            Assert.IsEmpty(ActiveRun.State.Box);
            Assert.AreEqual("Empty", SlotText("BoxSlot0", "RoleText"));
        }

        /// <summary>The refused case still opens the confirmation, with the reason and a dead
        /// Release button, rather than the drop appearing to do nothing at all.</summary>
        [UnityTest]
        public IEnumerator TeamScene_ReleasingTheLastPartyMon_IsRefusedWithAReason()
        {
            ActiveRun.Begin(MakeRun(1), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            yield return DragToReleaseZone("PartySlot0");

            Assert.IsNotNull(GameObject.Find("ReleaseConfirm"));
            StringAssert.Contains("last mon", GameObject.Find("MessageText").GetComponent<Text>().text);
            Assert.IsFalse(FindButton("ReleaseConfirmButton").interactable);

            FindButton("ReleaseCancelButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, ActiveRun.State.LineUp.Count);
        }

        [UnityTest]
        public IEnumerator TeamScene_BackButton_ReturnsToTheIngameMenu()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            AssertNavigatesVia("BackButton", nameof(SceneNavigator.GoToIngameMenu));
        }

        /// <summary>The dev way into a fight until map nodes start one.</summary>
        [UnityTest]
        public IEnumerator TeamScene_DevBattleButton_OpensTheBattleScreen()
        {
            ActiveRun.Begin(MakeRun(), MakeLibrary());

            yield return LoadScene(TeamScenePath);

            AssertNavigatesVia("DevBattleButton", nameof(SceneNavigator.GoToBattle));
        }

        /// <summary>Opening Team.unity directly (no run) is routine while working on the scene, so
        /// it has to explain itself rather than throw on a null RunState.</summary>
        [UnityTest]
        public IEnumerator TeamScene_WithNoRunInProgress_ShowsTheEmptyState()
        {
            yield return LoadScene(TeamScenePath);

            var emptyState = GameObject.Find("EmptyStateText");
            Assert.IsNotNull(emptyState, "EmptyStateText should be active when there's no run");
            Assert.IsNull(GameObject.Find("PartySlot0"),
                "The party slots should be hidden when there's no run to show in them");
            Assert.IsFalse(FindButton("SwapButton").interactable);
        }

        private static TeamSlotView SlotView(string slotName)
        {
            var slot = GameObject.Find(slotName);
            Assert.IsNotNull(slot, $"Expected a slot named '{slotName}' in the Team scene");
            var view = slot.GetComponent<TeamSlotView>();
            Assert.IsNotNull(view, $"{slotName} has no TeamSlotView");
            return view;
        }

        /// <summary>Runs a whole drag gesture the way uGUI does: begin and move on the source
        /// slot, drop on the target, then end on the source. pointerDrag is what OnDrop reads to
        /// find out which slot the card came from, exactly as the input module sets it.</summary>
        private static IEnumerator Drag(string fromSlot, string toSlot)
        {
            var source = SlotView(fromSlot);
            var target = SlotView(toSlot);
            var eventData = new PointerEventData(EventSystem.current) { pointerDrag = source.gameObject };

            source.OnBeginDrag(eventData);
            source.OnDrag(eventData);
            target.OnDrop(eventData);
            source.OnEndDrag(eventData);
            yield return null;
        }

        /// <summary>Picks a card up and drops it on the release zone, which is a drop target
        /// rather than a slot — so the drop goes to the zone's own handler.</summary>
        private static IEnumerator DragToReleaseZone(string fromSlot)
        {
            var source = SlotView(fromSlot);
            var zone = Object.FindFirstObjectByType<ReleaseZoneView>();
            Assert.IsNotNull(zone, "the Team scene should have a release zone");
            var eventData = new PointerEventData(EventSystem.current) { pointerDrag = source.gameObject };

            source.OnBeginDrag(eventData);
            source.OnDrag(eventData);
            zone.OnDrop(eventData);
            source.OnEndDrag(eventData);
            yield return null;
        }

        /// <summary>The text of one line inside a Team screen slot's card — the slots are built
        /// at runtime by TeamPanelController, so this is how a test reads what a slot ended up
        /// showing.</summary>
        private static string SlotText(string slotName, string lineName)
        {
            var slot = GameObject.Find(slotName);
            Assert.IsNotNull(slot, $"Expected a slot named '{slotName}' in the Team scene");
            var line = slot.transform.Find($"Card/{lineName}");
            Assert.IsNotNull(line, $"{slotName} has no {lineName}");
            return line.GetComponent<Text>().text;
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
            library.AllSpecies = new System.Collections.Generic.List<PokemonSpeciesDefinitionAsset>();
            for (int i = 0; i < MonNames.Length; i++)
            {
                library.AllSpecies.Add(MakeSpecies(i + 1, MonNames[i]));
            }
            return library;
        }

        private static readonly string[] MonNames = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta" };

        private static RunState MakeRun(int partyCount = 2)
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
