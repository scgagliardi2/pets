using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>The Pokémon Center as a shop (ADR 0013) and what it sells: held items and how they reach
    /// a mon's derived stats (HeldItems), the shelf of Pokémon and how it's matched to the party, the
    /// purchases themselves (PokemonCenterShop), and the money the rest of the run earns to spend there.</summary>
    public class PokemonCenterTests
    {
        private const int BandAttack = 3;

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name, int tier = SpeciesTier.MinTier,
            bool legendary = false, PokemonSpeciesDefinitionAsset evolvesInto = null)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.Tier = tier;
            species.BaseAttack = 3;
            species.BaseHealth = 4;
            species.BaseSpeed = 1;
            species.HealthGrowthPercent = 50;
            species.IsLegendary = legendary;
            species.EvolvesInto = evolvesInto;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary(params PokemonSpeciesDefinitionAsset[] species)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = species.ToList();
            return library;
        }

        private static ItemDefinitionAsset MakeBand(string id = "band", int attack = BandAttack, int price = 8)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            item.Id = id;
            item.DisplayName = id;
            item.Price = price;
            item.StatModifiers = new Stats { Attack = attack };
            return item;
        }

        private static (RunState State, PokemonSpeciesLibrary Library) MakeRun(int partyCount = 2)
        {
            var species = MakeSpecies(1, "Holder");
            var library = MakeLibrary(species);
            var state = new RunState();
            for (int i = 0; i < partyCount; i++)
            {
                state.LineUp.Add(ExperienceResolver.CreateAtExp(species, $"mon-{i}", 0, library));
            }
            return (state, library);
        }

        // ---- held items -----------------------------------------------------------------------

        [Test]
        public void Equip_TakesTheItemOutOfTheBag_AndAddsItsStatsToTheMon()
        {
            var (state, library) = MakeRun();
            var band = MakeBand();
            state.Items.Add(band.Id);
            var mon = state.LineUp[0];
            var before = mon.CurrentStats;

            Assert.IsTrue(HeldItems.Equip(state, mon, band, library));

            Assert.AreEqual(band.Id, mon.HeldItemId);
            Assert.IsEmpty(state.Items, "the item is on the mon now, not in the bag as well");
            Assert.AreEqual(before.Attack + BandAttack, mon.CurrentStats.Attack);
            Assert.AreEqual(before.Health, mon.CurrentStats.Health);
            Assert.AreEqual(before.Speed, mon.CurrentStats.Speed);
        }

        [Test]
        public void Equip_WithNoneInTheBag_ChangesNothing()
        {
            var (state, library) = MakeRun();
            var mon = state.LineUp[0];
            var before = mon.CurrentStats;

            Assert.IsFalse(HeldItems.Equip(state, mon, MakeBand(), library));

            Assert.IsNull(mon.HeldItemId);
            Assert.AreEqual(before.Attack, mon.CurrentStats.Attack);
        }

        [Test]
        public void Equip_OntoAMonAlreadyHoldingOne_PutsTheOldOneBackInTheBag()
        {
            var (state, library) = MakeRun();
            var weak = MakeBand("weak", attack: 1);
            var strong = MakeBand("strong", attack: 5);
            state.Items.AddRange(new[] { weak.Id, strong.Id });
            var mon = state.LineUp[0];
            int baseAttack = mon.CurrentStats.Attack;

            HeldItems.Equip(state, mon, weak, library);
            HeldItems.Equip(state, mon, strong, library);

            Assert.AreEqual(strong.Id, mon.HeldItemId);
            CollectionAssert.AreEqual(new[] { weak.Id }, state.Items);
            Assert.AreEqual(baseAttack + 5, mon.CurrentStats.Attack, "only the item it holds counts — they don't stack");
        }

        /// <summary>The trap the derived-stats rule sets: a bonus written onto CurrentStats would be
        /// recomputed away by the next EXP grant. The item is an input to the derivation instead.</summary>
        [Test]
        public void AHeldItemsBonus_SurvivesGrowthAndEvolution()
        {
            var evolved = MakeSpecies(2, "Evolved");
            var baseForm = MakeSpecies(1, "Base", evolvesInto: evolved);
            var library = MakeLibrary(baseForm, evolved);
            var state = new RunState();
            var mon = ExperienceResolver.CreateAtExp(baseForm, "grower", 0, library);
            state.LineUp.Add(mon);
            state.Items.Add("band");
            HeldItems.Equip(state, mon, MakeBand(), library);

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution, library);

            Assert.AreEqual(evolved.Id, mon.SpeciesId, "the grant should have evolved it");
            var grown = StatGrowth.AtExp(baseForm, mon.InstanceId, mon.Exp, mon.TimesEvolved);
            Assert.AreEqual(grown.Attack + BandAttack, mon.CurrentStats.Attack);
            Assert.AreEqual(grown.Health, mon.CurrentHP, "HP is still topped up to the derived Health");
        }

        [Test]
        public void Unequip_PutsTheItemBackInTheBag_AndTakesItsStatsAway()
        {
            var (state, library) = MakeRun();
            var band = MakeBand();
            state.Items.Add(band.Id);
            var mon = state.LineUp[0];
            int baseAttack = mon.CurrentStats.Attack;
            HeldItems.Equip(state, mon, band, library);

            Assert.IsTrue(HeldItems.Unequip(state, mon, library));

            Assert.IsNull(mon.HeldItemId);
            CollectionAssert.AreEqual(new[] { band.Id }, state.Items);
            Assert.AreEqual(baseAttack, mon.CurrentStats.Attack);
            Assert.IsFalse(HeldItems.Unequip(state, mon, library), "nothing left to take off");
        }

        [Test]
        public void Move_HandsTheItemToAnotherMon_OrTradesWithOneAlreadyHoldingSomething()
        {
            var (state, library) = MakeRun();
            var band = MakeBand();
            var other = MakeBand("other", attack: 1);
            state.Items.AddRange(new[] { band.Id, other.Id });
            var first = state.LineUp[0];
            var second = state.LineUp[1];
            int firstBase = first.CurrentStats.Attack;
            int secondBase = second.CurrentStats.Attack;
            HeldItems.Equip(state, first, band, library);

            Assert.IsTrue(HeldItems.Move(first, second, library));
            Assert.IsNull(first.HeldItemId);
            Assert.AreEqual(band.Id, second.HeldItemId);
            Assert.AreEqual(firstBase, first.CurrentStats.Attack);
            Assert.AreEqual(secondBase + BandAttack, second.CurrentStats.Attack);

            HeldItems.Equip(state, first, other, library);
            Assert.IsTrue(HeldItems.Move(second, first, library));
            Assert.AreEqual(band.Id, first.HeldItemId);
            Assert.AreEqual(other.Id, second.HeldItemId, "the two mons trade");
            Assert.IsEmpty(state.Items);
        }

        [Test]
        public void ReleasingAMon_KeepsItsItemInTheBag()
        {
            var (state, library) = MakeRun();
            var band = MakeBand();
            state.Items.Add(band.Id);
            HeldItems.Equip(state, state.LineUp[1], band, library);

            Assert.IsTrue(state.ReleaseMon(RosterGroup.Party, 1));

            CollectionAssert.AreEqual(new[] { band.Id }, state.Items);
        }

        [Test]
        public void CombiningDuplicates_KeepsTheConsumedMonsItemInTheBag()
        {
            var (state, library) = MakeRun();
            var band = MakeBand();
            state.Items.Add(band.Id);
            HeldItems.Equip(state, state.LineUp[1], band, library);

            CombineResolver.Combine(state, RosterGroup.Party, 1, RosterGroup.Party, 0, library);

            Assert.AreEqual(1, state.LineUp.Count, "the duplicate was consumed");
            CollectionAssert.AreEqual(new[] { band.Id }, state.Items);
        }

        // ---- purchases ------------------------------------------------------------------------

        [Test]
        public void ANewRun_StartsWithMoney_AndAnEmptyBag()
        {
            var state = new RunState();

            Assert.AreEqual(RunState.StartingMoney, state.Money);
            Assert.IsEmpty(state.Items);
            Assert.GreaterOrEqual(state.Money, PokemonCenterShop.BallPrice(BallTier.Poke), "a run can afford a ball from the start");
        }

        [Test]
        public void BuyBall_AddsOneOfThatTier_AndIsRefusedWhenThePlayerCantAffordIt()
        {
            var state = new RunState { Money = PokemonCenterShop.BallPrice(BallTier.Great) };

            Assert.AreEqual(PokemonCenterShop.Purchase.Bought, PokemonCenterShop.BuyBall(state, BallTier.Great));
            Assert.AreEqual(0, state.Money);
            Assert.AreEqual(1, state.Balls.CountOf(BallTier.Great));
            Assert.AreEqual(0, state.Balls.CountOf(BallTier.Poke));

            Assert.AreEqual(PokemonCenterShop.Purchase.NotEnoughMoney, PokemonCenterShop.BuyBall(state, BallTier.Poke));
            Assert.AreEqual(1, state.Balls.Total());
        }

        [Test]
        public void BetterBalls_CostMore()
        {
            Assert.Less(PokemonCenterShop.BallPrice(BallTier.Poke), PokemonCenterShop.BallPrice(BallTier.Great));
            Assert.Less(PokemonCenterShop.BallPrice(BallTier.Great), PokemonCenterShop.BallPrice(BallTier.Ultra));
        }

        private static ItemLibrary MakeItemLibrary(params ItemDefinitionAsset[] items)
        {
            var library = ScriptableObject.CreateInstance<ItemLibrary>();
            library.AllItems = items.ToList();
            return library;
        }

        [Test]
        public void BuyItem_PutsTheOfferedItemInTheBag_OnlyOnce()
        {
            var (state, library) = MakeRun();
            state.Money = 20;
            var items = MakeItemLibrary(MakeBand("a", price: 8), MakeBand("b", price: 8), MakeBand("c", price: 8));
            var stock = PokemonCenterShop.OpenFor(state, "center", seed: 3, library, items);
            string offered = stock.Items[0];

            Assert.AreEqual(PokemonCenterShop.Purchase.Bought, PokemonCenterShop.BuyItem(state, 0, items));
            CollectionAssert.AreEqual(new[] { offered }, state.Items);
            Assert.IsNull(stock.Items[0], "a bought item is off the shelf");
            Assert.AreEqual(12, state.Money);

            Assert.AreEqual(PokemonCenterShop.Purchase.SoldOut, PokemonCenterShop.BuyItem(state, 0, items));
            Assert.AreEqual(12, state.Money, "a sold-out item costs nothing");
        }

        [Test]
        public void BuyItem_IsRefused_WhenThePlayerCantAffordIt()
        {
            var (state, library) = MakeRun();
            state.Money = 7;
            var items = MakeItemLibrary(MakeBand("a", price: 8));
            var stock = PokemonCenterShop.OpenFor(state, "center", seed: 3, library, items);

            Assert.AreEqual(PokemonCenterShop.Purchase.NotEnoughMoney, PokemonCenterShop.BuyItem(state, 0, items));
            Assert.IsEmpty(state.Items);
            Assert.IsNotNull(stock.Items[0]);
        }

        [Test]
        public void ItemOffers_AreDifferentItemsFromTheLibrary_ReproducibleFromTheSeed()
        {
            var items = MakeItemLibrary(Enumerable.Range(0, 6).Select(i => MakeBand($"item-{i}")).ToArray());

            var offers = PokemonCenterShop.GenerateItemOffers(items, seed: 42);

            Assert.AreEqual(PokemonCenterShop.ItemsOnOffer, offers.Count);
            CollectionAssert.AllItemsAreUnique(offers);
            CollectionAssert.AreEqual(offers, PokemonCenterShop.GenerateItemOffers(items, seed: 42));
            CollectionAssert.IsEmpty(PokemonCenterShop.GenerateItemOffers(null, seed: 42), "no library, nothing on the row");
            Assert.AreEqual(2, PokemonCenterShop.GenerateItemOffers(MakeItemLibrary(MakeBand("x"), MakeBand("y")), 1).Count,
                "fewer items than the row holds offers what there is");
        }

        [Test]
        public void BuyPokemon_AdoptsItIntoTheBox_OnlyOnce()
        {
            var (state, library) = MakeRun();
            state.Money = PokemonCenterShop.PokemonPrice * 2;
            var stock = PokemonCenterShop.OpenFor(state, "center", seed: 3, library);
            var offered = stock.Pokemon[0];

            Assert.AreEqual(PokemonCenterShop.Purchase.Bought, PokemonCenterShop.BuyPokemon(state, 0));
            Assert.AreSame(offered, state.Box.Single());
            Assert.IsNull(stock.Pokemon[0], "an adopted Pokémon is off the shelf");
            Assert.AreEqual(PokemonCenterShop.PokemonPrice, state.Money);

            Assert.AreEqual(PokemonCenterShop.Purchase.SoldOut, PokemonCenterShop.BuyPokemon(state, 0));
            Assert.AreEqual(PokemonCenterShop.PokemonPrice, state.Money, "a sold-out Pokémon costs nothing");
        }

        [Test]
        public void BuyPokemon_IsRefused_WhenThePlayerCantAffordIt()
        {
            var (state, library) = MakeRun();
            state.Money = PokemonCenterShop.PokemonPrice - 1;
            PokemonCenterShop.OpenFor(state, "center", seed: 3, library);

            Assert.AreEqual(PokemonCenterShop.Purchase.NotEnoughMoney, PokemonCenterShop.BuyPokemon(state, 0));
            Assert.IsEmpty(state.Box);
            Assert.IsNotNull(state.CenterStock.Pokemon[0]);
        }

        // ---- the shelf ------------------------------------------------------------------------

        /// <summary>"Matching the party" in the two terms a mon's strength is made of: its tier (which
        /// is its stat total — SpeciesTier) and its EXP.</summary>
        [Test]
        public void Offers_AreBaseForms_InThePartysTierRange_AtThePartysAverageExp()
        {
            var evolved = MakeSpecies(2, "EvolvedForm", tier: 2);
            var starter = MakeSpecies(1, "Starter", tier: 1, evolvesInto: evolved);
            var tierOne = MakeSpecies(3, "TierOne", tier: 1);
            var tierTwo = MakeSpecies(4, "TierTwo", tier: 2);
            var otherTwo = MakeSpecies(5, "OtherTwo", tier: 2);
            var legendary = MakeSpecies(6, "Legend", tier: 1, legendary: true);
            var tooStrong = MakeSpecies(7, "TooStrong", tier: 4);
            var library = MakeLibrary(starter, evolved, tierOne, tierTwo, otherTwo, legendary, tooStrong);

            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(starter, "a", 4, library));
            state.LineUp.Add(ExperienceResolver.CreateAtExp(tierTwo, "b", 1, library));

            var offers = PokemonCenterShop.GenerateOffers(state, "center", seed: 11, library);

            Assert.AreEqual(PokemonCenterShop.PokemonOnOffer, offers.Count);
            CollectionAssert.AllItemsAreUnique(offers.Select(m => m.SpeciesId).ToList(), "three different Pokémon");
            var allowed = new[] { starter.Id, tierOne.Id, tierTwo.Id, otherTwo.Id };
            foreach (var offer in offers)
            {
                CollectionAssert.Contains(allowed, offer.SpeciesId,
                    "never an evolved form, a Legendary, or a tier the party doesn't field");
                Assert.AreEqual(3, offer.Exp, "the line-up's average EXP, (4 + 1) / 2 rounded");
            }
        }

        [Test]
        public void Offers_WidenTheTierRange_WhenTooFewSpeciesMatchIt()
        {
            var lonely = MakeSpecies(1, "Lonely", tier: 5);
            var library = MakeLibrary(lonely, MakeSpecies(2, "Four", tier: 4), MakeSpecies(3, "Three", tier: 3),
                MakeSpecies(4, "One", tier: 1));
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(lonely, "a", 0, library));

            var offers = PokemonCenterShop.GenerateOffers(state, "center", seed: 1, library);

            Assert.AreEqual(PokemonCenterShop.PokemonOnOffer, offers.Count);
            CollectionAssert.DoesNotContain(offers.Select(m => m.SpeciesId).ToList(), 4,
                "widened a tier at a time — tier 1 is further out than it had to reach");
        }

        [Test]
        public void OpenFor_KeepsTheSameShelfAtTheSameCenter_AndRollsANewOneAtAnother()
        {
            var library = MakeLibrary(Enumerable.Range(1, 8).Select(i => MakeSpecies(i, $"Mon{i}")).ToArray());
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(library.AllSpecies[0], "a", 0, library));

            var first = PokemonCenterShop.OpenFor(state, "center-a", seed: 5, library);
            Assert.AreSame(first, PokemonCenterShop.OpenFor(state, "center-a", seed: 999, library),
                "coming back from the Team screen finds the same shelf");

            var second = PokemonCenterShop.OpenFor(state, "center-b", seed: 6, library);
            Assert.AreNotSame(first, second);
            Assert.AreSame(second, state.CenterStock);

            var replay = PokemonCenterShop.GenerateOffers(new RunState { LineUp = state.LineUp }, "center-a", 5, library);
            CollectionAssert.AreEqual(first.Pokemon.Select(m => m.SpeciesId).ToList(), replay.Select(m => m.SpeciesId).ToList(),
                "a shelf is reproducible from its seed");
        }

        [Test]
        public void TravellingOn_DropsTheShelf()
        {
            var state = new RunState { CenterStock = new PokemonCenterStock { NodeId = "center" } };
            state.TravelTo(LocationType.Forest);
            Assert.IsNull(state.CenterStock);
        }

        // ---- money ----------------------------------------------------------------------------

        [Test]
        public void AWin_PaysMoney_AndAGymPaysMore()
        {
            var state = new RunState { Money = 0 };

            Assert.AreEqual(BattleRewardResolver.MoneyPerWildWin, BattleRewardResolver.GrantWinMoney(state, isGym: false));
            Assert.AreEqual(BattleRewardResolver.MoneyPerGymWin, BattleRewardResolver.GrantWinMoney(state, isGym: true));

            Assert.AreEqual(BattleRewardResolver.MoneyPerWildWin + BattleRewardResolver.MoneyPerGymWin, state.Money);
            Assert.Greater(BattleRewardResolver.MoneyPerGymWin, BattleRewardResolver.MoneyPerWildWin);
        }
    }
}
