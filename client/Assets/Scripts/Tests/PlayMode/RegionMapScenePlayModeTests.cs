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
using Pets.Gameplay;
using Pets.Meta;

namespace Pets.Tests
{
    /// <summary>Drives the real Region Map scene (PLAN.md Phase 1) the way a player does — clicking
    /// actual node Buttons — to verify the scene's own wiring, not just the graph logic underneath
    /// it (that's RegionMapGeneratorTests / RegionMapTraversalTests). The map is randomly seeded per
    /// run, so these assert on structure and reachability rather than any particular layout; the
    /// seed under test is reported on failure so a bad map can be reproduced.</summary>
    public class RegionMapScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RegionMap.unity";
        private const string NodesPath = "MapScroll/Viewport/Content/Nodes";
        // Headless batchmode runs frames uncapped (no vsync/target frame rate), so a
        // wall-clock MoveDuration of real seconds can take several thousand frames of
        // sub-millisecond deltaTime to accumulate. Generous on purpose; a genuinely stuck
        // coroutine still fails well within a test's own timeout.
        private const int MoveTimeoutFrames = 20000;

        private RegionMapController controller;
        private Transform nodeRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
            yield return null;
            yield return null;

            controller = Object.FindFirstObjectByType<RegionMapController>();
            Assert.IsNotNull(controller, "scene has no RegionMapController");
            Assert.IsNotNull(controller.Traversal, "controller should have generated a map by its first frame");

            nodeRoot = GameObject.Find("Canvas").transform.Find(NodesPath);
            Assert.IsNotNull(nodeRoot, $"expected the map's node container at {NodesPath}");
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

        [UnityTest]
        public IEnumerator ClickingAnOfferedNode_MovesThePlayerTokenOntoIt()
        {
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

        [UnityTest]
        public IEnumerator ClickingAnUnreachableNode_DoesNothing()
        {
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
        public IEnumerator WalkingTheOfferedNodes_ReachesTheGym_AndEndsTheWalk()
        {
            var token = GameObject.Find("PlayerToken").GetComponent<RectTransform>();
            int layerCount = controller.Traversal.Map.LayerCount;

            for (int step = 0; step < layerCount; step++)
            {
                if (controller.Traversal.IsComplete)
                {
                    break;
                }

                var options = controller.Traversal.AvailableNextNodes;
                Assert.IsNotEmpty(options, $"dead end at {controller.Traversal.CurrentNodeId}. {Seed()}");

                ButtonFor(options[options.Count - 1].Id).onClick.Invoke();
                yield return WaitForMoveToFinish();
            }

            Assert.IsTrue(controller.Traversal.IsComplete, $"never reached the Gym. {Seed()}");
            Assert.AreEqual(controller.Traversal.Map.GymNodeId, controller.Traversal.CurrentNodeId, Seed());
            Assert.AreEqual(layerCount, controller.Traversal.VisitedNodeIds.Count, Seed());
            AssertTokenIsStandingOn(token, controller.Traversal.Map.GymNodeId);

            // Every path terminates at the Gym, so nothing is left to click once you're on it.
            Assert.IsFalse(nodeRoot.Cast<Transform>().Any(t => t.GetComponent<Button>() != null && t.GetComponent<Button>().interactable), Seed());
        }

        [UnityTest]
        public IEnumerator NewMapButton_GeneratesAFreshMap_AndPutsThePlayerBackAtTheStart()
        {
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
