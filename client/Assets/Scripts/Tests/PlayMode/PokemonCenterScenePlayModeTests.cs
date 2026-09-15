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
    /// <summary>The saved Pokémon Center scene against PokemonCenterController (ADR 0013): every Buy
    /// and Adopt button reaches the controller with its own card index, a purchase shows up in the
    /// run and on the screen at once, the shelf is the run's own, and Team/Leave navigate.</summary>
    public class PokemonCenterScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PokemonCenter.unity";
        private const string MuscleBandId = "muscle-band";

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

            var supplies = SupplyCards();
            Assert.AreEqual(PokemonCenterController.BallCardCount + 1, supplies.Length, "one card per ball tier, then the one item");
            for (int i = 0; i < supplies.Length; i++)
            {
                var button = SupplyCard(i).BuyButton;
                AssertWired(button, typeof(PokemonCenterController), nameof(PokemonCenterController.OnBuySupplyClicked));
                Assert.AreEqual(i, IntArgument(button), $"SupplyCard{i} should buy its own supply");
            }
            for (int i = 0; i < PokemonCenterShop.PokemonOnOffer; i++)
            {
                var button = PokemonCard(i).BuyButton;
                AssertWired(button, typeof(PokemonCenterController), nameof(PokemonCenterController.OnAdoptClicked));
                Assert.AreEqual(i, IntArgument(button), $"PokemonCard{i} should adopt its own Pokémon");
            }
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
            for (int i = 0; i < PokemonCenterController.BallCardCount; i++)
            {
                Assert.AreEqual(BallCatalog.DisplayName(BallCatalog.AllTiers[i]), SupplyCard(i).NameText.text);
            }
            Assert.AreEqual(PokemonCenterController.WelcomeLine, Controller().ClerkText.text);
            Assert.AreEqual("Money 50", Label("MoneyValue"));
        }

        [UnityTest]
        public IEnumerator BuyingABall_AddsThatTierToTheInventory_AndShowsTheNewCount()
        {
            var run = BeginRun(money: 20);
            int greatCard = System.Array.IndexOf(BallCatalog.AllTiers, BallTier.Great);
            yield return LoadScene();

            SupplyCard(greatCard).BuyButton.onClick.Invoke();

            Assert.AreEqual(1, run.Balls.CountOf(BallTier.Great), "it goes into the inventory the battle tray throws from");
            Assert.AreEqual(20 - PokemonCenterShop.BallPrice(BallTier.Great), run.Money);
            Assert.AreEqual("Balls 1", Label("BallsValue"));
            Assert.AreEqual($"Money {run.Money}", Label("MoneyValue"));
            Assert.AreEqual("Have 1", SupplyCard(greatCard).OwnedText.text);
        }

        [UnityTest]
        public IEnumerator BuyingTheMuscleBand_PutsItInTheBag()
        {
            var run = BeginRun(money: 20);
            yield return LoadScene();

            SupplyCard(PokemonCenterController.BallCardCount).BuyButton.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { MuscleBandId }, run.Items);
            Assert.Less(run.Money, 20);
            Assert.AreEqual("Items 1", Label("ItemsValue"));
            StringAssert.Contains("bag", Controller().ClerkText.text);
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

            foreach (var card in SupplyCards())
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

        private static ShopItemCardView[] SupplyCards() =>
            Object.FindObjectsByType<ShopItemCardView>(FindObjectsSortMode.None).OrderBy(c => c.name).ToArray();

        private static ShopItemCardView SupplyCard(int index)
        {
            var card = GameObject.Find($"SupplyCard{index}");
            Assert.IsNotNull(card, $"expected SupplyCard{index}");
            return card.GetComponent<ShopItemCardView>();
        }

        private static ShopPokemonCardView PokemonCard(int index)
        {
            var card = GameObject.Find($"PokemonCard{index}");
            Assert.IsNotNull(card, $"expected PokemonCard{index}");
            return card.GetComponent<ShopPokemonCardView>();
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
