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
            // Ensure every test starts a genuinely fresh run, not a leftover save/history from a
            // previous test/session.
            SaveSystem.DeleteSave();
            SaveSystem.DeleteHistory();
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DeleteSave();
            SaveSystem.DeleteHistory();
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

        /// <summary>Buys whatever's purchasable and fights every round until the run ends
        /// (win, loss, or the 200-round safety cap).</summary>
        private static IEnumerator PlayUntilRunEnds(RunController controller)
        {
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
        }

        [UnityTest]
        public IEnumerator SceneLoads_ShowsHomeScreenWithNoActiveRun()
        {
            var controller = FindRunController();
            Assert.IsNotNull(controller, "RunController not found in Game scene");
            Assert.IsNull(controller.State, "No run should be active until Continue/New Run is chosen");
            Assert.AreEqual(AppScreen.Home, controller.Screen);
            Assert.IsNotNull(GameObject.Find("EventSystem"));

            var homePanel = GameObject.Find("Canvas/HomePanel");
            Assert.IsNotNull(homePanel);
            Assert.IsTrue(homePanel.activeSelf, "HomePanel should be visible at launch");

            // ShopPanel exists but starts hidden — GameObject.Find skips inactive objects, so
            // Transform.Find (which doesn't) is needed to check it's actually inactive.
            var shopPanel = GameObject.Find("Canvas").transform.Find("ShopPanel").gameObject;
            Assert.IsFalse(shopPanel.activeSelf, "ShopPanel should be hidden until a run starts");
            yield break;
        }

        [UnityTest]
        public IEnumerator StartNewRun_FromHome_ShowsShopPanelAndHidesHome()
        {
            var controller = FindRunController();
            controller.StartNewRun();
            yield return null;

            Assert.IsNotNull(controller.State);
            Assert.AreEqual(AppScreen.InRun, controller.Screen);
            Assert.IsNotNull(GameObject.Find("Canvas/ShopPanel"), "ShopPanel should be active after starting a run");

            var homePanel = GameObject.Find("Canvas").transform.Find("HomePanel").gameObject;
            Assert.IsFalse(homePanel.activeSelf, "HomePanel should hide once a run starts");
        }

        [UnityTest]
        public IEnumerator Buy_UpdatesBoardAndGoldTextReflectsIt()
        {
            var controller = FindRunController();
            controller.StartNewRun();
            yield return null;

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
            controller.StartNewRun();
            yield return null;

            int slot = FindPurchasableSlot(controller);
            Assert.GreaterOrEqual(slot, 0);
            controller.Buy(slot);
            yield return null;
            Assert.Greater(controller.State.Board.Count, 0);

            BattleLog resolvedLog = null;
            bool? resolvedWon = null;
            controller.OnBattleResolved += (playerLineup, enemyLineup, log, won) =>
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
            controller.StartNewRun();
            yield return null;

            yield return PlayUntilRunEnds(controller);

            controller.StartNewRun();
            yield return null;

            Assert.AreEqual(AppScreen.InRun, controller.Screen);
            Assert.AreEqual(GamePhase.Shop, controller.State.Phase);
            Assert.AreEqual(1, controller.State.Round);
        }

        [UnityTest]
        public IEnumerator RunEnd_ThenGoHome_RecordsHistoryAndReturnsToHomeScreen()
        {
            var controller = FindRunController();
            controller.StartNewRun();
            yield return null;

            yield return PlayUntilRunEnds(controller);

            var history = SaveSystem.LoadHistory();
            Assert.AreEqual(1, history.Count, "Ending a run should append exactly one history entry");
            Assert.AreEqual(controller.State.Victory, history[0].Victory);
            Assert.AreEqual(controller.State.Round, history[0].RoundReached);

            controller.GoHome();
            yield return null;

            Assert.AreEqual(AppScreen.Home, controller.Screen);
            var homePanel = GameObject.Find("Canvas/HomePanel");
            Assert.IsNotNull(homePanel);
            Assert.IsTrue(homePanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator Layout_KeyRowsAndSlotsAreSizedAndNotOverlapping()
        {
            var controller = FindRunController();
            controller.StartNewRun();
            yield return null;

            int slot = FindPurchasableSlot(controller);
            Assert.GreaterOrEqual(slot, 0);
            controller.Buy(slot);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var header = GameObject.Find("Canvas/ShopPanel/Header").GetComponent<RectTransform>();
            var shopRow = GameObject.Find("Canvas/ShopPanel/ShopRow").GetComponent<RectTransform>();
            var boardRow = GameObject.Find("Canvas/ShopPanel/BoardRow").GetComponent<RectTransform>();
            var footer = GameObject.Find("Canvas/ShopPanel/Footer").GetComponent<RectTransform>();

            AssertHasArea(header, "Header");
            AssertHasArea(shopRow, "ShopRow");
            AssertHasArea(boardRow, "BoardRow");
            AssertHasArea(footer, "Footer");

            // Rows should stack top-to-bottom without overlapping (a small tolerance covers
            // floating point/pixel rounding, not a real overlap).
            Assert.LessOrEqual(WorldTop(shopRow), WorldBottom(header) + 1f, "ShopRow overlaps Header");
            Assert.LessOrEqual(WorldTop(boardRow), WorldBottom(shopRow) + 1f, "BoardRow overlaps ShopRow");
            Assert.LessOrEqual(WorldTop(footer), WorldBottom(boardRow) + 1f, "Footer overlaps BoardRow");

            // Shop slots should be spread out horizontally, not stacked at the same position.
            var shopRowTransform = shopRow.transform;
            Assert.GreaterOrEqual(shopRowTransform.childCount, 2, "Expected at least 2 shop slot instances");
            float firstX = shopRowTransform.GetChild(0).position.x;
            float secondX = shopRowTransform.GetChild(1).position.x;
            Assert.AreNotEqual(firstX, secondX, "First two shop slots are stacked at the same X position");
        }

        private static void AssertHasArea(RectTransform rt, string label)
        {
            Assert.Greater(rt.rect.width, 1f, $"{label} has ~zero width");
            Assert.Greater(rt.rect.height, 1f, $"{label} has ~zero height");
        }

        private static float WorldTop(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return corners[1].y;
        }

        private static float WorldBottom(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return corners[0].y;
        }
    }
}
