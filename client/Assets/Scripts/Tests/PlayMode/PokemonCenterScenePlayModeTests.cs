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
using Pets.Data;
using Pets.Gameplay;
using Pets.Meta;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>The saved Pokémon Center scene against PokemonCenterController (ADR 0013, ADR 0014): its
    /// three rows — Pokémon, Poké Balls, held items — every Buy and Adopt button reaching the controller
    /// with its own card index, a purchase showing up in the run and on the screen at once, the shelf
    /// being the run's own, and Team/Leave navigating.</summary>
    public class PokemonCenterScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PokemonCenter.unity";

        [SetUp]
        public void SetUp() => ResetRunState();

        [TearDown]
        public void TearDown() => ResetRunState();

        private static void ResetRunState()
        {
            ActiveRun.End();
            PendingBattle.Clear();
        }

        private static IEnumerator LoadScene()
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
        public IEnumerator PokemonCenter_ButtonsAreWired()
        {
            BeginRun(money: 50);
            yield return LoadScene();

            AssertWired(FindButton("TeamButton"), typeof(SceneNavigator), nameof(SceneNavigator.GoToTeam));
            AssertWired(FindButton("LeaveButton"), typeof(SceneNavigator), nameof(SceneNavigator.ReturnToMap));

            AssertRowWired("PokemonCard", PokemonCenterShop.PokemonOnOffer, nameof(PokemonCenterController.OnAdoptClicked),
                i => PokemonCard(i).BuyButton);
            AssertRowWired("BallCard", BallCatalog.AllTiers.Length, nameof(PokemonCenterController.OnBuyBallClicked),
                i => BallCard(i).BuyButton);
            AssertRowWired("ItemCard", PokemonCenterShop.ItemsOnOffer, nameof(PokemonCenterController.OnBuyItemClicked),
                i => ItemCard(i).BuyButton);
            Assert.AreEqual(BallCatalog.AllTiers.Length + PokemonCenterShop.ItemsOnOffer,
                Object.FindObjectsByType<ShopItemCardView>(FindObjectsSortMode.None).Length, "one card per ball tier and per item on offer");
        }

        /// <summary>The item row once ran under the footer on a screen wider than 16:9, because the
        /// canvas matched width and came out ~600 units tall. Expand keeps it at least 1280x720, and the
        /// three shelves have to fit above the footer inside that.</summary>
        [UnityTest]
        public IEnumerator Shelves_ClearTheFooter_AndTheCanvasNeverShrinksBelowTheReference()
        {
            BeginRun(money: 50);
            yield return LoadScene();

            var scaler = GameObject.Find("Canvas").GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode,
                "match-width lets a wide screen cut the canvas below the 720 units the shelves need");

            var footerTop = Corners("BottomBar")[1].y;
            foreach (var shelf in new[] { "PokemonShelf", "BallShelf", "ItemShelf" })
            {
                Assert.GreaterOrEqual(Corners(shelf)[0].y, footerTop - 0.5f, $"{shelf} runs under the footer");
            }
        }

        private static Vector3[] Corners(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"expected {name}");
            var corners = new Vector3[4];
            go.GetComponent<RectTransform>().GetWorldCorners(corners);
            return corners;
        }

        [UnityTest]
        public IEnumerator PokemonCenter_ShowsTheRunsOwnShelf()
        {
            var run = BeginRun(money: 50);
            var stock = PokemonCenterShop.OpenFor(run, "center-node", seed: 21, ActiveRun.Library);
            yield return LoadScene();

            Assert.AreSame(stock, Controller().Stock, "the shelf the map rolled, not a new one");
            for (int i = 0; i < stock.Pokemon.Count; i++)
            {
                var species = ActiveRun.Library.GetById(stock.Pokemon[i].SpeciesId);
                var card = PokemonCard(i);
                Assert.AreEqual(species.DisplayName, card.StatsBox.NameText.text);
                Assert.AreEqual($"${PokemonCenterShop.PokemonPrice}", card.PriceText.text);
                Assert.IsTrue(card.BuyButton.interactable);
            }
            for (int i = 0; i < BallCatalog.AllTiers.Length; i++)
            {
                Assert.AreEqual(BallCatalog.DisplayName(BallCatalog.AllTiers[i]), BallCard(i).NameText.text);
            }
            Assert.AreEqual(PokemonCenterController.WelcomeLine, Controller().ClerkText.text);
            Assert.AreEqual("Money 50", Label("MoneyValue"));
        }

        [UnityTest]
        public IEnumerator ItemRow_ShowsThreeDifferentItemsRolledOntoTheShelf()
        {
            var run = BeginRun(money: 50);
            yield return LoadScene();

            var stock = Controller().Stock;
            Assert.AreSame(run.CenterStock, stock);
            Assert.AreEqual(PokemonCenterShop.ItemsOnOffer, stock.Items.Count, "the item library has at least as many items as the row");
            CollectionAssert.AllItemsAreUnique(stock.Items);
            for (int i = 0; i < stock.Items.Count; i++)
            {
                Assert.IsNotEmpty(ItemCard(i).NameText.text, $"ItemCard{i} should name its item");
                Assert.IsTrue(ItemCard(i).BuyButton.interactable);
            }
        }

        [UnityTest]
        public IEnumerator BuyingABall_AddsThatTierToTheInventory_AndShowsTheNewCount()
        {
            var run = BeginRun(money: 20);
            int greatCard = System.Array.IndexOf(BallCatalog.AllTiers, BallTier.Great);
            yield return LoadScene();

            BallCard(greatCard).BuyButton.onClick.Invoke();

            Assert.AreEqual(1, run.Balls.CountOf(BallTier.Great), "it goes into the inventory the battle tray throws from");
            Assert.AreEqual(20 - PokemonCenterShop.BallPrice(BallTier.Great), run.Money);
            Assert.AreEqual("Balls 1", Label("BallsValue"));
            Assert.AreEqual($"Money {run.Money}", Label("MoneyValue"));
            Assert.AreEqual("Have 1", BallCard(greatCard).OwnedText.text);
            Assert.IsFalse(BallCard(greatCard).IsSold, "balls never run out");
        }

        [UnityTest]
        public IEnumerator BuyingAnItem_PutsItInTheBag_AndStampsItsCard()
        {
            var run = BeginRun(money: 50);
            yield return LoadScene();
            string offered = Controller().Stock.Items[0];

            ItemCard(0).BuyButton.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { offered }, run.Items);
            Assert.Less(run.Money, 50);
            Assert.AreEqual("Items 1", Label("ItemsValue"));
            StringAssert.Contains("bag", Controller().ClerkText.text);
            Assert.IsTrue(ItemCard(0).IsSold);
            Assert.IsFalse(ItemCard(0).BuyButton.interactable, "each item on offer sells once");
        }

        [UnityTest]
        public IEnumerator AdoptingAPokemon_SendsItToTheBox_AndStampsItsCard()
        {
            int money = PokemonCenterShop.PokemonPrice + PokemonCenterShop.PokemonPrice / 2;
            var run = BeginRun(money);
            var stock = PokemonCenterShop.OpenFor(run, "center-node", seed: 21, ActiveRun.Library);
            var offered = stock.Pokemon[0];
            yield return LoadScene();

            PokemonCard(0).BuyButton.onClick.Invoke();

            Assert.AreSame(offered, run.Box.Single());
            Assert.AreEqual(money - PokemonCenterShop.PokemonPrice, run.Money);
            Assert.IsTrue(PokemonCard(0).IsSold);
            Assert.IsFalse(PokemonCard(0).BuyButton.interactable);
            Assert.IsFalse(PokemonCard(1).BuyButton.interactable,
                $"with ${run.Money} left, another ${PokemonCenterShop.PokemonPrice} Pokémon is out of reach");
        }

        [UnityTest]
        public IEnumerator WithNoMoney_EveryBuyButtonIsDead()
        {
            BeginRun(money: 0);
            yield return LoadScene();

            foreach (var card in Object.FindObjectsByType<ShopItemCardView>(FindObjectsSortMode.None))
            {
                Assert.IsFalse(card.BuyButton.interactable, $"{card.name} shouldn't be buyable with no money");
            }
            for (int i = 0; i < PokemonCenterShop.PokemonOnOffer; i++)
            {
                Assert.IsFalse(PokemonCard(i).BuyButton.interactable);
            }
        }

        [UnityTest]
        public IEnumerator WithNoRun_TheCenterExplainsItselfInsteadOfThrowing()
        {
            yield return LoadScene();

            StringAssert.Contains("closed", Controller().ClerkText.text);
            Assert.IsNull(GameObject.Find("PokemonCard0"));
            Assert.IsNull(GameObject.Find("ItemCard0"));
        }

        [UnityTest]
        public IEnumerator Leave_ReturnsToTheMap()
        {
            BeginRun(money: 0);
            yield return LoadScene();

            FindButton("LeaveButton").onClick.Invoke();
            yield return SceneTransitionWait.UntilActiveScene(SceneNames.Map);
        }

        // ---- helpers --------------------------------------------------------------------------

        private static RunState BeginRun(int money)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            string[] names = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta" };
            library.AllSpecies = names.Select((name, i) => MakeSpecies(i + 1, name)).ToList();

            var run = new RunState { RunSeed = 99, Money = money };
            run.LineUp.Add(ExperienceResolver.CreateAtExp(library.AllSpecies[0], "lead", 2, library));
            run.LineUp.Add(ExperienceResolver.CreateAtExp(library.AllSpecies[1], "support", 2, library));
            ActiveRun.Begin(run, library);
            return run;
        }

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = 3;
            species.BaseHealth = 4;
            species.BaseSpeed = 1;
            return species;
        }

        private static PokemonCenterController Controller()
        {
            var controller = Object.FindFirstObjectByType<PokemonCenterController>();
            Assert.IsNotNull(controller, "the scene has no PokemonCenterController");
            return controller;
        }

        private static ShopItemCardView BallCard(int index) => Card<ShopItemCardView>($"BallCard{index}");

        private static ShopItemCardView ItemCard(int index) => Card<ShopItemCardView>($"ItemCard{index}");

        private static ShopPokemonCardView PokemonCard(int index) => Card<ShopPokemonCardView>($"PokemonCard{index}");

        private static T Card<T>(string name) where T : Component
        {
            var card = GameObject.Find(name);
            Assert.IsNotNull(card, $"expected {name}");
            return card.GetComponent<T>();
        }

        private static void AssertRowWired(string prefix, int count, string methodName, System.Func<int, Button> buttonAt)
        {
            for (int i = 0; i < count; i++)
            {
                var button = buttonAt(i);
                AssertWired(button, typeof(PokemonCenterController), methodName);
                Assert.AreEqual(i, IntArgument(button), $"{prefix}{i} should act on its own card");
            }
        }

        private static string Label(string name)
        {
            var text = GameObject.Find(name);
            Assert.IsNotNull(text, $"expected a Text named {name}");
            return text.GetComponent<Text>().text;
        }

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"expected a Button named '{name}'");
            return button;
        }

        private static void AssertWired(Button button, System.Type targetType, string methodName)
        {
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), $"{button.name} should have one persistent listener");
            Assert.IsInstanceOf(targetType, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(methodName, button.onClick.GetPersistentMethodName(0));
        }

        /// <summary>The int a persistent listener was baked with — UnityEvent keeps it in a private
        /// serialized call, so this reads it the way the Inspector does.</summary>
        private static int IntArgument(Button button)
        {
            var calls = typeof(UnityEngine.Events.UnityEventBase)
                .GetField("m_PersistentCalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(button.onClick);
            var call = calls.GetType().GetMethod("GetListener").Invoke(calls, new object[] { 0 });
            var arguments = call.GetType().GetProperty("arguments").GetValue(call);
            return (int)arguments.GetType().GetProperty("intArgument").GetValue(arguments);
        }
    }
}
