using System.Collections;
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
    /// <summary>Exercises the actual hand-built Forest scene (Assets/Scenes/Game.unity, built by
    /// Pets.EditorTools.ForestSceneBuilder) end to end in Play Mode — the Meta layer's logic is
    /// already covered by EditMode tests (RunMetaTests.cs); this instead proves the *scene wiring*
    /// (button-&gt;controller listeners, serialized panel/controller references) survived being
    /// saved and reloaded, which hand-authored scenes are the most likely place to silently break.
    ///
    /// Uses Transform.Find from the (always-active) Canvas root rather than GameObject.Find,
    /// since the PvE/Camp/RunOver overlays start inactive and GameObject.Find cannot see through
    /// an inactive branch of the hierarchy.</summary>
    public class ForestScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private Transform canvas;

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

            canvas = GameObject.Find("Canvas").transform;
        }

        private Transform Find(string relativePath) => canvas.Find(relativePath);
        private Button FindButton(string relativePath) => Find(relativePath).GetComponent<Button>();

        [UnityTest]
        public IEnumerator SceneLoads_AndBootstrapsARunStateWithATwoMonLineUp()
        {
            Assert.IsNotNull(RunBootstrapper.Instance, "RunBootstrapper should exist in the scene");
            Assert.IsNotNull(RunBootstrapper.Instance.State);
            Assert.AreEqual(2, RunBootstrapper.Instance.State.LineUp.Count);
            Assert.AreEqual(5, RunBootstrapper.Instance.State.Nodes.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator ClickingMapTab_ThenGo_OnAPvENode_PlaysABattleAndReturnsToTheMap()
        {
            FindButton("TabBar/MapTabButton").onClick.Invoke();
            Assert.IsTrue(Find("Content/MapPanel").gameObject.activeInHierarchy);

            var state = RunBootstrapper.Instance.State;
            Assert.AreEqual(NodeType.PvE, state.CurrentNode.Type, "Forest's first node should be PvE");

            FindButton("Content/MapPanel/GoButton").onClick.Invoke();
            Assert.IsTrue(Find("PvEOverlay").gameObject.activeInHierarchy, "Go on a PvE node should show the PvE overlay");

            var continueButton = FindButton("PvEOverlay/ContinueButton");
            float elapsed = 0f;
            while (!continueButton.gameObject.activeInHierarchy && elapsed < 20f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Assert.IsTrue(continueButton.gameObject.activeInHierarchy, "Battle should resolve and reveal the Continue button within 20s");

            continueButton.onClick.Invoke();

            bool backAtMap = Find("Content/MapPanel").gameObject.activeInHierarchy;
            bool runOver = Find("RunOverOverlay").gameObject.activeInHierarchy;
            Assert.IsTrue(backAtMap || runOver, "After Continue, the player should see either the Map again or the Run Over screen");
        }

        [UnityTest]
        public IEnumerator TeamTab_ShowsTheStartingLineUpNames()
        {
            FindButton("TabBar/TeamTabButton").onClick.Invoke();
            Assert.IsTrue(Find("Content/TeamPanel").gameObject.activeInHierarchy);

            var lineUpText = Find("Content/TeamPanel/LineUpText").GetComponent<Text>();
            StringAssert.Contains("Lead", lineUpText.text);
            StringAssert.Contains("Support", lineUpText.text);
            yield break;
        }
    }
}
