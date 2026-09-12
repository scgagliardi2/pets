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
    /// <summary>Drives the real Region Map scene the way a player does — clicking actual node
    /// Buttons — to verify the scene's own wiring, not just the graph logic underneath it (that's
    /// RegionMapGeneratorTests / RegionMapTraversalTests).
    ///
    /// Two fixtures, because arriving at a node now resolves it (NodeResolutionController):
    /// - **Movement** tests run with no species library behind the run, which is the case a Map
    ///   opened on its own is in and which switches resolution off, so a click is just a walk.
    /// - **Resolution** tests run a real run on a *seeded* map, so the node type under test is
    ///   reachable on purpose rather than by luck, with a party nothing in the roster can beat so a
    ///   fight's outcome is never in question. The map seed is reported on failure either way.</summary>
    public class RegionMapScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RegionMap.unity";
        private const string NodesPath = "MapScroll/Viewport/Content/Nodes";
        // Headless batchmode runs frames uncapped (no vsync/target frame rate), so a
        // wall-clock MoveDuration of real seconds can take several thousand frames of
        // sub-millisecond deltaTime to accumulate. Generous on purpose; a genuinely stuck
        // coroutine still fails well within a test's own timeout.
        private const int MoveTimeoutFrames = 20000;

        /// <summary>EXP enough to put the test's mon's stats far beyond any curated species, so
        /// every fight these tests walk into is won in a Step or two and Morale never enters the
        /// picture.
        ///
        /// Granted as EXP rather than written straight onto CurrentStats: stats are *derived* from
        /// species + EXP (Meta/ExperienceResolver), so the first win's own EXP award would recompute
        /// a hand-set CurrentStats right back down to the species' base and lose every fight
        /// after it.</summary>
        private const int UnbeatableExp = 1000;

        private const int MapSeedSearchLimit = 500;

        private RegionMapController controller;
        private Transform nodeRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Each test needs a map at its start node. The screen shows the *run's* map and walked
            // path rather than generating its own (RegionMapTraversal.ForRun), which is the point —
            // re-entering the scene resumes the walk instead of rerolling it. That also means a run
            // left in the static ActiveRun by an earlier fixture, or by the previous test in this
            // one, would carry its half-walked map into the next test.
            ActiveRun.End();
            PendingBattle.Clear();

            yield return ReloadScene();
        }

        /// <summary>Loads the Map scene and re-resolves the fixture's handles onto it — the same
        /// thing returning to the Map from the Ingame Menu, Team or a finished fight does at
        /// runtime.</summary>
        private IEnumerator ReloadScene()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;

            ResolveHandles();
        }

        private void ResolveHandles()
        {
            controller = Object.FindFirstObjectByType<RegionMapController>();
            Assert.IsNotNull(controller, "scene has no RegionMapController");
            Assert.IsNotNull(controller.Traversal, "controller should have generated a map by its first frame");

            nodeRoot = GameObject.Find("Canvas").transform.Find(NodesPath);
            Assert.IsNotNull(nodeRoot, $"expected the map's node container at {NodesPath}");
        }

        [TearDown]
        public void TearDown()
        {
            ActiveRun.End();
            PendingBattle.Clear();
        }

        [UnityTest]
        public IEnumerator GeneratedMap_RendersOneNodeVisualPerGeneratedNode_AndExactlyOneGym()
        {
            var nodes = nodeRoot.Cast<Transform>().Where(t => t.name.StartsWith("Node_")).ToList();

            Assert.AreEqual(controller.LastGeneratedNodes.Count, nodes.Count, Seed());
            Assert.AreEqual(1, nodes.Count(t => t.name.EndsWith("_Gym")), Seed());
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryNode_HasEitherARealIconOrAFallbackColor_AndACaptionWithItsFlavorName()
        {
            // Real art is dropped into Resources/Sprites/Nodes by name (see that folder's README);
            // until it exists for a given type, the node still renders — as a flat color swatch —
            // and every node always gets a caption naming what it is.
            var expectedNames = new Dictionary<NodeType, string>
            {
                { NodeType.PvE, "Battle" },
                { NodeType.Event, "Encounter" },
                { NodeType.PvP, "Mystery Trainer" },
                { NodeType.Camp, "Pokémon Center" },
                { NodeType.Gym, "Gym" }
            };

            foreach (var node in controller.LastGeneratedNodes)
            {
                var image = NodeObject(node.Id).GetComponent<Image>();
                Assert.IsNotNull(image, $"{node.Id} has no Image. {Seed()}");
                Assert.IsTrue(image.sprite != null || image.color.a > 0f, $"{node.Id} renders nothing. {Seed()}");

                var caption = nodeRoot.Cast<Transform>().FirstOrDefault(t => t.name == $"Caption_{node.Id}");
                Assert.IsNotNull(caption, $"{node.Id} has no caption. {Seed()}");
                string expectedText = node.Layer == 0 ? "Start" : expectedNames[node.Type];
                Assert.AreEqual(expectedText, caption.GetComponent<Text>().text, $"{node.Id}. {Seed()}");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator AtTheStart_ExactlyTheThreeOpeningOptionsAreClickable()
        {
            var interactable = nodeRoot.Cast<Transform>()
                .Select(t => t.GetComponent<Button>())
                .Where(b => b != null && b.interactable)
                .ToList();

            Assert.AreEqual(RegionMapGenerator.StartingOptionCount, interactable.Count, Seed());
            foreach (var node in controller.Traversal.AvailableNextNodes)
            {
                Assert.IsTrue(ButtonFor(node.Id).interactable, $"{node.Id} should be clickable. {Seed()}");
            }
            yield break;
        }

        /// <summary>The run's life total belongs on the screen where the player picks which fight to
        /// take, so the Map's title bar carries it.</summary>
        [UnityTest]
        public IEnumerator TitleBar_ShowsTheRunsMorale()
        {
            Assert.AreEqual($"Morale {ActiveRun.State.Morale}", GameObject.Find("MoraleValue").GetComponent<Text>().text);
            Assert.AreEqual($"Money {ActiveRun.State.Money}", GameObject.Find("MoneyValue").GetComponent<Text>().text);
            yield break;
        }

        [UnityTest]
        public IEnumerator ClickingAnOfferedNode_MovesThePlayerTokenOntoIt()
        {
            yield return ReloadWithoutResolution();
            var token = GameObject.Find("PlayerToken").GetComponent<RectTransform>();
            Assert.IsNotNull(token, "scene has no PlayerToken");

            var target = controller.Traversal.AvailableNextNodes[0];
            Vector2 before = token.anchoredPosition;

            ButtonFor(target.Id).onClick.Invoke();
            yield return WaitForMoveToFinish();

            Assert.AreEqual(target.Id, controller.Traversal.CurrentNodeId, Seed());
            Assert.AreNotEqual(before, token.anchoredPosition, $"the token did not move. {Seed()}");
            AssertTokenIsStandingOn(token, target.Id);
        }

        /// <summary>Against the actual scene: walking a step, then reloading the Map the way
        /// returning from the Ingame Menu or Team does, resumes the same map at the same node. The
        /// map and walked path live on RunState (RegionMapTraversal.ForRun); before that, the
        /// controller owned them and re-entry silently rerolled the map.</summary>
        [UnityTest]
        public IEnumerator ReEnteringTheScene_ResumesTheRunsMapAndPosition()
        {
            yield return ReloadWithoutResolution();

            int seedBefore = controller.Traversal.Map.Seed;
            var target = controller.Traversal.AvailableNextNodes[0];
            ButtonFor(target.Id).onClick.Invoke();
            yield return WaitForMoveToFinish();
            Assert.AreEqual(target.Id, controller.Traversal.CurrentNodeId);

            yield return ReloadScene();

            Assert.AreEqual(seedBefore, controller.Traversal.Map.Seed,
                "re-entering the Map regenerated it instead of resuming the run's map");
            Assert.AreEqual(target.Id, controller.Traversal.CurrentNodeId,
                "re-entering the Map put the player back at the start");
            AssertTokenIsStandingOn(GameObject.Find("PlayerToken").GetComponent<RectTransform>(), target.Id);
        }

        /// <summary>Re-entering a node the player already walked onto must not re-run it: the node
        /// was resolved when they arrived, and the walk is forward-only.</summary>
        [UnityTest]
        public IEnumerator ReEnteringTheScene_DoesNotResolveTheNodeThePlayerIsStandingOn()
        {
            yield return ReloadWithSeededRun(SeedWithOpeningNode(NodeType.Event));
            yield return WalkOnto(OpeningNodeOfType(NodeType.Event).Id);

            yield return ReloadScene();
            yield return null;

            Assert.IsFalse(Resolution().IsResolvingInPlace, $"the node resolved itself a second time. {Seed()}");
            Assert.IsFalse(PendingBattle.HasPending, Seed());
        }

        /// <summary>Clicking New Map replaces the run's map, so the run and the screen can't
        /// disagree about which map is being walked.</summary>
        [UnityTest]
        public IEnumerator NewMapButton_ReplacesTheRunsMap()
        {
            yield return ReloadWithoutResolution();
            var run = ActiveRun.State;

            var firstMap = run.LocationMap;
            Assert.IsNotNull(firstMap, "arriving at the Map should have given the run one");

            GameObject.Find("NewMapButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreSame(run.LocationMap, controller.Traversal.Map);
            Assert.AreNotSame(firstMap, run.LocationMap, "New Map should have replaced the run's map");
            Assert.AreEqual(run.LocationMap.StartNodeId, controller.Traversal.CurrentNodeId);
        }

        [UnityTest]
        public IEnumerator ClickingAnUnreachableNode_DoesNothing()
        {
            yield return ReloadWithoutResolution();

            // A node two layers ahead is reachable eventually but never in one step, and its
            // button is disabled — invoking it directly proves the controller guards the move
            // itself rather than relying on the Button's interactable flag alone.
            var farNode = controller.Traversal.Map.NodesInLayer(2).First();
            string before = controller.Traversal.CurrentNodeId;

            Assert.IsFalse(ButtonFor(farNode.Id).interactable, Seed());
            ButtonFor(farNode.Id).onClick.Invoke();
            yield return null;

            Assert.AreEqual(before, controller.Traversal.CurrentNodeId, Seed());
            Assert.IsFalse(controller.IsMoving, Seed());
        }

        [UnityTest]
        public IEnumerator NewMapButton_GeneratesAFreshMap_AndPutsThePlayerBackAtTheStart()
        {
            yield return ReloadWithoutResolution();

            ButtonFor(controller.Traversal.AvailableNextNodes[0].Id).onClick.Invoke();
            yield return WaitForMoveToFinish();
            Assert.AreNotEqual(controller.Traversal.Map.StartNodeId, controller.Traversal.CurrentNodeId);

            GameObject.Find("NewMapButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            nodeRoot = GameObject.Find("Canvas").transform.Find(NodesPath);
            Assert.AreEqual(controller.Traversal.Map.StartNodeId, controller.Traversal.CurrentNodeId, Seed());
            Assert.AreEqual(1, controller.Traversal.VisitedNodeIds.Count, Seed());

            // Stale visuals from the previous map would leave more node objects than nodes.
            var nodes = nodeRoot.Cast<Transform>().Where(t => t.name.StartsWith("Node_")).ToList();
            Assert.AreEqual(controller.LastGeneratedNodes.Count, nodes.Count, $"old map's nodes were not cleared. {Seed()}");
            Assert.AreEqual(1, GameObject.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Count(r => r.name == "PlayerToken"), "the player token was duplicated");
        }

        /// <summary>The premise the movement tests above rest on: with no content behind the screen
        /// there's no encounter to roll, so arriving at a node does nothing at all.</summary>
        [UnityTest]
        public IEnumerator WithNoSpeciesLibrary_ArrivingAtANodeResolvesNothing()
        {
            yield return ReloadWithoutResolution();

            ButtonFor(controller.Traversal.AvailableNextNodes[0].Id).onClick.Invoke();
            yield return WaitForMoveToFinish();

            Assert.IsFalse(Resolution().IsResolvingInPlace, Seed());
            Assert.IsFalse(PendingBattle.HasPending, Seed());
            Assert.AreEqual(SceneNames.Map, SceneManager.GetActiveScene().name, Seed());
        }

        [UnityTest]
        public IEnumerator ArrivingAtAnEncounterNode_ShowsItsOverlay_UntilContinueIsPressed()
        {
            yield return ReloadWithSeededRun(SeedWithOpeningNode(NodeType.Event));
            var target = OpeningNodeOfType(NodeType.Event);

            ButtonFor(target.Id).onClick.Invoke();
            yield return WaitForMoveToFinish();

            Assert.IsTrue(Resolution().IsResolvingInPlace, $"the Encounter node resolved into nothing. {Seed()}");
            var overlay = GameObject.Find("NodeEventOverlay");
            Assert.IsNotNull(overlay, "the event overlay should be showing");
            Assert.AreEqual("Encounter", overlay.transform.Find("Dialog/TitleText").GetComponent<Text>().text);

            OverlayContinueButton().onClick.Invoke();
            yield return null;

            Assert.IsFalse(Resolution().IsResolvingInPlace, "Continue should dismiss the overlay");
            Assert.AreEqual(target.Id, controller.Traversal.CurrentNodeId, "the player stays where they walked to");
        }

        /// <summary>The Pokémon Center only ever lands mid-run (RegionMapGenerator keeps it to one
        /// fixed layer or the one before the Gym), so this walks to it, fighting whatever is in the
        /// way — which is also the closest thing here to playing a stretch of a real run.</summary>
        [UnityTest]
        public IEnumerator ArrivingAtThePokemonCenter_RestsTheTeam()
        {
            var (mapSeed, center) = SeedWithNode(NodeType.Camp);
            yield return ReloadWithSeededRun(mapSeed);
            var run = ActiveRun.State;

            // Everything before the Center is resolved and dismissed on the way; the last step is
            // walked without resolving it, so its overlay is still up to assert on.
            var path = PathTo(controller.Traversal.Map, center.Id);
            for (int i = 0; i < path.Count - 1; i++)
            {
                yield return WalkOnto(path[i]);
            }
            ButtonFor(center.Id).onClick.Invoke();
            yield return WaitForMoveToFinish();

            Assert.AreEqual(center.Id, controller.Traversal.CurrentNodeId, Seed());
            Assert.IsNotNull(GameObject.Find("CampOverlay"), $"the Pokémon Center overlay should be showing. {Seed()}");
            Assert.Greater(run.NextBattleAttackBonusPercent, 0f, "resting should buff the next fight");
            Assert.Greater(run.LineUp[0].Exp, 0, "resting should grant EXP");

            OverlayContinueButton().onClick.Invoke();
            yield return null;
            Assert.IsFalse(Resolution().IsResolvingInPlace);
        }

        [UnityTest]
        public IEnumerator ArrivingAtABattleNode_FightsTheEncounter_AndReturnsToTheMap()
        {
            yield return ReloadWithSeededRun(SeedWithOpeningNode(NodeType.PvE));
            var run = ActiveRun.State;
            var target = OpeningNodeOfType(NodeType.PvE);

            ButtonFor(target.Id).onClick.Invoke();
            yield return WaitForMoveToFinish();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Battle);

            var battle = Object.FindFirstObjectByType<BattleScreenController>();
            Assert.IsNotNull(battle, "the Battle scene should be running the node's fight");
            Assert.AreEqual(2, battle.State.LineUpB.Count, "a wild encounter is a pair (EncounterGenerator)");

            yield return FinishFightAndReturnToMap();

            Assert.AreEqual(target.Id, controller.Traversal.CurrentNodeId, "the fight left the player where it found them");
            Assert.AreEqual(3, run.Morale, "a won fight costs no Morale");
            Assert.Greater(run.LineUp[0].Exp, 0, "a won fight pays EXP");
        }

        [UnityTest]
        public IEnumerator WalkingTheOfferedNodes_ReachesTheGym_AndStartsTheGymFight()
        {
            yield return ReloadWithSeededRun(SeedWithOpeningNode(NodeType.PvE));
            int layerCount = controller.Traversal.Map.LayerCount;

            for (int step = 0; step < layerCount && !controller.Traversal.IsComplete; step++)
            {
                var options = controller.Traversal.AvailableNextNodes;
                Assert.IsNotEmpty(options, $"dead end at {controller.Traversal.CurrentNodeId}. {Seed()}");

                var next = options[options.Count - 1];
                if (next.Type == NodeType.Gym)
                {
                    // Stop at the Gym's own fight rather than resolving it — what beating it does is
                    // BattleScenePlayModeTests' business.
                    ButtonFor(next.Id).onClick.Invoke();
                    yield return WaitForMoveToFinish();
                    break;
                }
                yield return WalkOnto(next.Id);
            }

            Assert.IsTrue(controller.Traversal.IsComplete, $"never reached the Gym. {Seed()}");
            Assert.AreEqual(controller.Traversal.Map.GymNodeId, controller.Traversal.CurrentNodeId, Seed());
            Assert.AreEqual(layerCount, controller.Traversal.VisitedNodeIds.Count, Seed());

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Battle);
            var battle = Object.FindFirstObjectByType<BattleScreenController>();
            StringAssert.StartsWith(GymTeamGenerator.InstanceIdPrefix, battle.State.LineUpB[0].InstanceId,
                "the Gym node should fight a Gym team, not a wild encounter");
        }

        // ---- fixtures -------------------------------------------------------------------------

        /// <summary>Rebuilds the screen over a run with no species library, which is what a Map
        /// opened on its own has: NodeResolutionController then leaves every arrival alone, so these
        /// tests are about walking and nothing else.</summary>
        private IEnumerator ReloadWithoutResolution()
        {
            ActiveRun.End();
            ActiveRun.Begin(new RunState(), null);
            yield return ReloadScene();
        }

        /// <summary>Rebuilds the screen over a real run on a known map, with a one-mon party nothing
        /// in the roster can beat — so a test can walk onto a node type it chose, and any fight on
        /// the way is won. The library is the curated one the scene's own RunBootstrapper published
        /// on the previous load.</summary>
        private IEnumerator ReloadWithSeededRun(int mapSeed)
        {
            var library = ActiveRun.Library;
            Assert.IsNotNull(library, "the Map scene's RunBootstrapper should have published the curated library");

            var run = new RunState { RunSeed = 4242, LocationMap = RegionMapGenerator.Generate(mapSeed) };
            var mon = PokemonInstanceFactory.Create(library.AllSpecies[0], "test-lead");
            mon.Exp = UnbeatableExp;
            ExperienceResolver.Recompute(mon, library);
            run.LineUp.Add(mon);

            ActiveRun.End();
            ActiveRun.Begin(run, library);
            yield return ReloadScene();
        }

        /// <summary>Walks one step and resolves whatever the node turns out to be: dismissing an
        /// overlay, or fighting the battle it left the scene for and coming back.</summary>
        private IEnumerator WalkOnto(string nodeId)
        {
            ButtonFor(nodeId).onClick.Invoke();
            yield return WaitForMoveToFinish();

            if (Resolution().IsResolvingInPlace)
            {
                OverlayContinueButton().onClick.Invoke();
                yield return null;
                yield break;
            }

            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Battle);
            yield return FinishFightAndReturnToMap();
        }

        private IEnumerator FinishFightAndReturnToMap()
        {
            var battle = Object.FindFirstObjectByType<BattleScreenController>();
            Assert.IsNotNull(battle, "expected the Battle scene to be running a fight");

            FindActiveButton("SkipButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(BattleOutcome.SideAWins, battle.Outcome,
                "the test party is built to win every fight — check UnbeatableStat");

            FindActiveButton("ResultActionButton").onClick.Invoke();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
            yield return null;
            ResolveHandles();
        }

        // ---- map/seed helpers -----------------------------------------------------------------

        /// <summary>The first map seed whose opening layer offers this node type, so a test can
        /// reach it in one click instead of hoping a random map obliges.</summary>
        private static int SeedWithOpeningNode(NodeType type)
        {
            for (int seed = 1; seed < MapSeedSearchLimit; seed++)
            {
                if (RegionMapGenerator.Generate(seed).NodesInLayer(1).Any(n => n.Type == type))
                {
                    return seed;
                }
            }
            Assert.Fail($"no map seed under {MapSeedSearchLimit} opens onto a {type} node");
            return 0;
        }

        /// <summary>The first map seed containing this node type anywhere, with the node itself —
        /// for a type the generator never puts in the opening layer (the Pokémon Center).</summary>
        private static (int seed, RegionMapNode node) SeedWithNode(NodeType type)
        {
            for (int seed = 1; seed < MapSeedSearchLimit; seed++)
            {
                var match = RegionMapGenerator.Generate(seed).Nodes.FirstOrDefault(n => n.Type == type);
                if (match != null)
                {
                    return (seed, match);
                }
            }
            Assert.Fail($"no map seed under {MapSeedSearchLimit} contains a {type} node");
            return (0, null);
        }

        private RegionMapNode OpeningNodeOfType(NodeType type)
        {
            var node = controller.Traversal.AvailableNextNodes.FirstOrDefault(n => n.Type == type);
            Assert.IsNotNull(node, $"the seeded map should offer a {type} node from the start. {Seed()}");
            return node;
        }

        /// <summary>The node ids to click, in order, to get from where the player stands to
        /// <paramref name="targetId"/> — a breadth-first walk of the map's forward edges, excluding
        /// the node they're already on.</summary>
        private static List<string> PathTo(RegionMap map, string targetId)
        {
            var cameFrom = new Dictionary<string, string>();
            var queue = new Queue<string>();
            queue.Enqueue(map.StartNodeId);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == targetId)
                {
                    var path = new List<string>();
                    for (string step = targetId; step != map.StartNodeId; step = cameFrom[step])
                    {
                        path.Add(step);
                    }
                    path.Reverse();
                    return path;
                }

                foreach (string next in map.GetById(current).NextIds)
                {
                    if (!cameFrom.ContainsKey(next))
                    {
                        cameFrom[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }

            Assert.Fail($"'{targetId}' is not reachable from the map's start node");
            return null;
        }

        // ---- scene lookups --------------------------------------------------------------------

        private static NodeResolutionController Resolution()
        {
            var resolution = Object.FindFirstObjectByType<NodeResolutionController>();
            Assert.IsNotNull(resolution, "scene has no NodeResolutionController");
            return resolution;
        }

        /// <summary>The Continue button of whichever node overlay is up — only one is ever active,
        /// and an inactive overlay's button is skipped.</summary>
        private static Button OverlayContinueButton() => FindActiveButton("ContinueButton");

        private static Button FindActiveButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"expected an active Button named '{name}'");
            return button;
        }

        private IEnumerator WaitForMoveToFinish()
        {
            int frames = 0;
            while (controller.IsMoving)
            {
                Assert.Less(++frames, MoveTimeoutFrames, $"the player token never finished moving. {Seed()}");
                yield return null;
            }
        }

        private void AssertTokenIsStandingOn(RectTransform token, string nodeId)
        {
            var nodeRect = NodeObject(nodeId).GetComponent<RectTransform>();
            Assert.AreEqual(nodeRect.anchoredPosition.x, token.anchoredPosition.x, 0.5f, $"token is not above {nodeId}. {Seed()}");
            Assert.Greater(token.anchoredPosition.y, nodeRect.anchoredPosition.y, $"token should sit above {nodeId}. {Seed()}");
        }

        private Button ButtonFor(string nodeId) => NodeObject(nodeId).GetComponent<Button>();

        private Transform NodeObject(string nodeId)
        {
            var node = nodeRoot.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith($"Node_{nodeId}_"));
            Assert.IsNotNull(node, $"no rendered node for '{nodeId}'. {Seed()}");
            return node;
        }

        private string Seed() => $"(map seed {controller.Traversal.Map.Seed})";
    }
}
