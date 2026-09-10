using System.Collections;
using NUnit.Framework;
using Pets.Gameplay;
using Pets.Simulation;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pets.Tests
{
    /// <summary>
    /// Loads the generated Game scene and drives it through RunController's public methods
    /// (not simulated pointer input — that verifies logic/event wiring; actual click-feel is a
    /// human playtest per CLAUDE.md's UI rule). Requires Pets/Generate Starter Content and
    /// Pets/Generate Game Scene to have been run first.
    /// </summary>
    public class RunControllerPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Ensure every test starts a genuinely fresh run, not a leftover save from a
            // previous test/session.
            SaveSystem.DeleteSave();
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DeleteSave();
        }

        private static RunController FindRunController()
        {
            return Object.FindFirstObjectByType<RunController>();
        }

        private static int FindPurchasableSlot(RunController controller)
        {
            for (int i = 0; i < controller.State.ShopSlots.Count; i++)
            {
                if (controller.State.ShopSlots[i].Offer != null)
                {
                    return i;
                }
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator SceneLoads_RunControllerAndUIExist()
        {
            var controller = FindRunController();
            Assert.IsNotNull(controller, "RunController not found in Game scene");
            Assert.IsNotNull(controller.State);
            Assert.IsNotNull(GameObject.Find("Canvas/ShopPanel"));
            Assert.IsNotNull(GameObject.Find("EventSystem"));
            yield break;
        }

        [UnityTest]
        public IEnumerator Buy_UpdatesBoardAndGoldTextReflectsIt()
        {
            var controller = FindRunController();
            var goldText = GameObject.Find("Canvas/ShopPanel/Header/GoldText").GetComponent<Text>();

            int startingGold = controller.State.Gold;
            int startingBoardCount = controller.State.Board.Count;
            int slot = FindPurchasableSlot(controller);
            Assert.GreaterOrEqual(slot, 0, "No purchasable shop slot found");

            controller.Buy(slot);
            yield return null;

            Assert.AreEqual(startingBoardCount + 1, controller.State.Board.Count);
            Assert.Less(controller.State.Gold, startingGold);
            StringAssert.Contains(controller.State.Gold.ToString(), goldText.text);
        }

        [UnityTest]
        public IEnumerator Fight_ProducesBattleResultAndTransitionsPhase()
        {
            var controller = FindRunController();
            int slot = FindPurchasableSlot(controller);
            Assert.GreaterOrEqual(slot, 0);
            controller.Buy(slot);
            yield return null;
            Assert.Greater(controller.State.Board.Count, 0);

            BattleLog resolvedLog = null;
            bool? resolvedWon = null;
            controller.OnBattleResolved += (log, won) =>
            {
                resolvedLog = log;
                resolvedWon = won;
            };

            int startingRound = controller.State.Round;
            controller.Fight();
            yield return null;

            Assert.IsNotNull(resolvedLog, "OnBattleResolved never fired");
            Assert.IsTrue(resolvedLog.Events.Count > 0);
            Assert.IsNotNull(resolvedWon);

            var battlePanel = GameObject.Find("Canvas/BattleResultPanel");
            Assert.IsNotNull(battlePanel);
            Assert.IsTrue(battlePanel.activeSelf, "Battle result panel did not activate on OnBattleResolved");

            Assert.IsTrue(controller.State.Phase == GamePhase.Shop || controller.State.Phase == GamePhase.RunOver);
            if (controller.State.Phase == GamePhase.Shop)
            {
                Assert.AreEqual(startingRound + 1, controller.State.Round);
            }
        }

        [UnityTest]
        public IEnumerator FullRun_EventuallyEndsAndNewRunResetsToShopRoundOne()
        {
            var controller = FindRunController();

            bool ended = false;
            controller.OnRunEnded += _ => ended = true;

            for (int i = 0; i < 200 && !ended; i++)
            {
                for (int s = 0; s < controller.State.ShopSlots.Count; s++)
                {
                    if (controller.State.ShopSlots[s].Offer != null && controller.State.Board.Count < controller.Config.BoardMaxSize)
                    {
                        controller.Buy(s);
                    }
                }
                controller.Fight();
                yield return null;
            }

            Assert.IsTrue(ended, "Run never ended within 200 simulated rounds");

            controller.StartNewRun();
            yield return null;

            Assert.AreEqual(GamePhase.Shop, controller.State.Phase);
            Assert.AreEqual(1, controller.State.Round);
        }
    }
}
