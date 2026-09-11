using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Pets.Gameplay;

namespace Pets.Tests
{
    /// <summary>Structural check for the Region Map preview scene (PLAN.md Phase 1, item 5) — the
    /// map is randomly generated each run (RegionMapGeneratorTests.cs covers the graph logic
    /// itself), so this just confirms the scene actually renders one node visual per generated
    /// node and exactly one Gym node, rather than asserting on any particular layout.</summary>
    public class RegionMapScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RegionMap.unity";

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
        }

        [UnityTest]
        public IEnumerator GeneratedMap_RendersExactlyOneNodeVisualPerGeneratedNode_AndExactlyOneGym()
        {
            var controller = Object.FindFirstObjectByType<RegionMapController>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.LastGeneratedNodes, "RegionMapController should have generated a map by its first frame");

            var content = GameObject.Find("Canvas").transform.Find("MapScroll/Viewport/Content");
            int nodeVisualCount = 0;
            int gymVisualCount = 0;
            foreach (Transform child in content)
            {
                if (!child.name.StartsWith("Node_"))
                {
                    continue;
                }
                nodeVisualCount++;
                if (child.name.Contains("Gym"))
                {
                    gymVisualCount++;
                }
            }

            Assert.AreEqual(controller.LastGeneratedNodes.Count, nodeVisualCount);
            Assert.AreEqual(1, gymVisualCount);
            yield break;
        }
    }
}
