using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Gameplay;
using UnityEngine;

namespace Pets.Tests
{
    public class ShopEconomyTests
    {
        private ShopConfig config;
        private CreatureLibrary library;
        private CreatureDefinition tier1A;
        private CreatureDefinition tier1B;
        private CreatureDefinition tier2;

        [SetUp]
        public void SetUp()
        {
            Random.InitState(1);

            config = ScriptableObject.CreateInstance<ShopConfig>();
            config.StartingGold = 10;
            config.GoldPerRound = 10;
            config.ShopSize = 3;
            config.RerollCost = 1;
            config.BoardMaxSize = 5;
            config.StartingLives = 3;
            config.TierUnlockEveryNRounds = 3;
            config.MaxLevel = 3;
            config.BaseBuyCost = 2;
            config.BuyCostPerTier = 1;
            config.SellRefundDivisor = 2;

            tier1A = Creature("tier1a", tier: 1);
            tier1B = Creature("tier1b", tier: 1);
            tier2 = Creature("tier2", tier: 2);

            library = ScriptableObject.CreateInstance<CreatureLibrary>();
            library.AllCreatures = new System.Collections.Generic.List<CreatureDefinition> { tier1A, tier1B, tier2 };
        }

        private static CreatureDefinition Creature(string id, int tier)
        {
            var creature = ScriptableObject.CreateInstance<CreatureDefinition>();
            creature.Id = id;
            creature.DisplayName = id;
            creature.Tier = tier;
            creature.BaseAttack = 1;
            creature.BaseHealth = 1;
            return creature;
        }

        private RunState NewRun()
        {
            var state = new RunState();
            ShopEconomy.StartRun(state, config, library);
            return state;
        }

        [Test]
        public void StartRun_SetsGoldLivesAndFillsAllShopSlots()
        {
            var state = NewRun();

            Assert.AreEqual(config.StartingGold, state.Gold);
            Assert.AreEqual(config.StartingLives, state.Lives);
            Assert.AreEqual(config.ShopSize, state.ShopSlots.Count);
            Assert.IsTrue(state.ShopSlots.All(s => s.Offer != null));
        }

        [Test]
        public void Buy_DeductsGoldAndAddsToBoard()
        {
            var state = NewRun();
            state.ShopSlots[0].Offer = tier1A;
            int startingGold = state.Gold;

            bool bought = ShopEconomy.Buy(state, config, 0);

            Assert.IsTrue(bought);
            Assert.AreEqual(startingGold - config.BuyCost(1), state.Gold);
            Assert.AreEqual(1, state.Board.Count);
            Assert.AreEqual(tier1A, state.Board[0].Definition);
            Assert.IsNull(state.ShopSlots[0].Offer);
        }

        [Test]
        public void Buy_FailsWhenGoldInsufficient()
        {
            var state = NewRun();
            state.Gold = 0;
            state.ShopSlots[0].Offer = tier1A;

            Assert.IsFalse(ShopEconomy.Buy(state, config, 0));
            Assert.AreEqual(0, state.Board.Count);
        }

        [Test]
        public void Buy_FailsWhenBoardFull()
        {
            var state = NewRun();
            state.Gold = 1000;
            for (int i = 0; i < config.BoardMaxSize; i++)
            {
                state.Board.Add(new BoardCreature { Definition = tier1A, Level = 1 });
            }
            state.ShopSlots[0].Offer = tier1B;

            Assert.IsFalse(ShopEconomy.Buy(state, config, 0));
            Assert.AreEqual(config.BoardMaxSize, state.Board.Count);
        }

        [Test]
        public void Sell_RefundsGoldAndRemovesFromBoard()
        {
            var state = NewRun();
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 2 });
            int startingGold = state.Gold;

            bool sold = ShopEconomy.Sell(state, config, 0);

            Assert.IsTrue(sold);
            Assert.AreEqual(0, state.Board.Count);
            Assert.AreEqual(startingGold + config.SellRefund(1) * 2, state.Gold);
        }

        [Test]
        public void Reroll_CostsGoldAndKeepsFrozenSlotOffer()
        {
            var state = NewRun();
            state.ShopSlots[0].Offer = tier2;
            state.ShopSlots[0].Frozen = true;
            int startingGold = state.Gold;

            bool rerolled = ShopEconomy.Reroll(state, config, library);

            Assert.IsTrue(rerolled);
            Assert.AreEqual(startingGold - config.RerollCost, state.Gold);
            Assert.AreEqual(tier2, state.ShopSlots[0].Offer);
            Assert.IsTrue(state.ShopSlots[0].Frozen);
        }

        [Test]
        public void Reroll_FailsWhenGoldInsufficient()
        {
            var state = NewRun();
            state.Gold = 0;

            Assert.IsFalse(ShopEconomy.Reroll(state, config, library));
        }

        [Test]
        public void TryCombineAll_MergesThreeCopiesToNextLevel()
        {
            var state = NewRun();
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 1 });
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 1 });
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 1 });

            ShopEconomy.TryCombineAll(state, config);

            Assert.AreEqual(1, state.Board.Count);
            Assert.AreEqual(2, state.Board[0].Level);
            Assert.AreEqual(tier1A, state.Board[0].Definition);
        }

        [Test]
        public void TryCombineAll_DoesNotMergeBeyondMaxLevel()
        {
            var state = NewRun();
            config.MaxLevel = 2;
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 2 });
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 2 });
            state.Board.Add(new BoardCreature { Definition = tier1A, Level = 2 });

            ShopEconomy.TryCombineAll(state, config);

            Assert.AreEqual(3, state.Board.Count);
            Assert.IsTrue(state.Board.All(c => c.Level == 2));
        }

        [Test]
        public void TierForRound_GatesShopPoolToUnlockedTiers()
        {
            var state = NewRun();
            state.Round = 1; // tier 1 unlocked only

            ShopEconomy.RefreshShop(state, config, library, includeFrozen: true);

            Assert.IsTrue(state.ShopSlots.All(s => s.Offer.Tier <= 1));
        }
    }
}
