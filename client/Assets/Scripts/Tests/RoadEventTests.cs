using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>The Event node's four encounters (Meta/RoadEvents, ADR 0014): what each choice costs and
    /// pays, that the chances are reproducible from the node's seed, and that a choice can be taken
    /// only once.</summary>
    public class RoadEventTests
    {
        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name, int tier = 1,
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

        private static ItemLibrary MakeItems(params string[] ids)
        {
            var library = ScriptableObject.CreateInstance<ItemLibrary>();
            foreach (var id in ids)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
                item.Id = id;
                item.DisplayName = id;
                item.Price = 5;
                library.AllItems.Add(item);
            }
            return library;
        }

        private const int StarterId = 1;
        private const int LegendId = 2;

        /// <summary>A one-mon run over a roster of a tier-1 starter, one tier-6 Legendary and a tier-2
        /// species, with three items in the library.</summary>
        private static (RunState State, PokemonSpeciesLibrary Library, ItemLibrary Items) MakeRun(int money = 10)
        {
            var starter = MakeSpecies(StarterId, "Starter");
            var library = MakeLibrary(starter, MakeSpecies(LegendId, "Legend", tier: 6, legendary: true),
                MakeSpecies(3, "TierTwo", tier: 2));
            var state = new RunState { Money = money };
            state.LineUp.Add(ExperienceResolver.CreateAtExp(starter, "lead", 3, library));
            return (state, library, MakeItems("band", "vest", "claw"));
        }

        // ---- rolling --------------------------------------------------------------------------

        [Test]
        public void Roll_IsReproducibleFromTheSeed_AndEveryEncounterTurnsUp()
        {
            var (state, library, items) = MakeRun();
            var seen = new HashSet<RoadEventKind>();
            for (int seed = 0; seed < 200; seed++)
            {
                var first = RoadEvents.Roll(state, library, items, seed);
                var again = RoadEvents.Roll(state, library, items, seed);
                Assert.AreEqual(first.Kind, again.Kind);
                Assert.AreEqual(first.Body, again.Body);
                seen.Add(first.Kind);
            }
            CollectionAssert.AreEquivalent(RoadEvents.AllKinds, seen);
        }

        [Test]
        public void EveryEncounter_HasATitle_AndTwoOrThreeChoices()
        {
            var (state, library, items) = MakeRun();
            foreach (var kind in RoadEvents.AllKinds)
            {
                var ev = RoadEvents.Build(kind, state, library, items, seed: 7);
                Assert.IsFalse(string.IsNullOrEmpty(ev.Title), kind.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(ev.Body), kind.ToString());
                Assert.That(ev.Choices.Count, Is.InRange(2, 3), kind.ToString());
            }
        }

        [Test]
        public void AChoice_CanOnlyBeTakenOnce()
        {
            var (state, library, items) = MakeRun();
            var ev = RoadEvents.Build(RoadEventKind.GameCorner, state, library, items, seed: 1);

            Assert.IsNotNull(RoadEvents.Resolve(ev, 1, state, library, items));
            Assert.IsNull(RoadEvents.Resolve(ev, 1, state, library, items));
            Assert.AreEqual(10 + RoadEvents.LooseCoins, state.Money);
        }

        // ---- Legendary Sighting ---------------------------------------------------------------

        [Test]
        public void Legendary_Challenge_StartsAFightAgainstOneLegendary_WithABounty()
        {
            var (state, library, items) = MakeRun();
            state.CompletedLocations.Add(LocationType.Forest);
            var ev = RoadEvents.Build(RoadEventKind.LegendarySighting, state, library, items, seed: 3);

            var outcome = RoadEvents.Resolve(ev, 0, state, library, items);

            Assert.IsTrue(outcome.StartsBattle);
            var foe = outcome.BattleLineUp.Single();
            Assert.AreEqual(LegendId, foe.SpeciesId);
            Assert.AreEqual(RunProgression.BaselineExp(state.BadgeCount), foe.Exp);
            StringAssert.StartsWith(RoadEvents.LegendaryInstancePrefix, foe.InstanceId);
            Assert.AreEqual(RoadEvents.LegendaryBountyMoney, outcome.Bounty.Money);
            CollectionAssert.Contains(items.AllItems.Select(i => i.Id).ToList(), outcome.Bounty.ItemId);
            Assert.AreEqual(10, state.Money, "nothing is paid until the fight is won");
        }

        [Test]
        public void Legendary_SlipAway_ChangesNothing()
        {
            var (state, library, items) = MakeRun();
            var ev = RoadEvents.Build(RoadEventKind.LegendarySighting, state, library, items, seed: 3);

            var outcome = RoadEvents.Resolve(ev, 1, state, library, items);

            Assert.IsFalse(outcome.StartsBattle);
            Assert.AreEqual(10, state.Money);
            Assert.AreEqual(RunState.StartingMorale, state.Morale);
        }

        [Test]
        public void Legendary_WithNoLegendaryInTheRoster_BecomesTheGameCorner()
        {
            var starter = MakeSpecies(StarterId, "Starter");
            var library = MakeLibrary(starter);
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(starter, "lead", 0, library));

            var ev = RoadEvents.Build(RoadEventKind.LegendarySighting, state, library, MakeItems("band"), seed: 1);

            Assert.AreEqual(RoadEventKind.GameCorner, ev.Kind);
        }

        [Test]
        public void GrantBounty_PaysTheMoneyAndTheItem()
        {
            var state = new RunState { Money = 0 };

            RoadEvents.GrantBounty(state, new LegendaryBounty { Money = 15, ItemId = "vest", ItemName = "Vest" });

            Assert.AreEqual(15, state.Money);
            CollectionAssert.AreEqual(new[] { "vest" }, state.Items);
        }

        // ---- Game Corner ----------------------------------------------------------------------

        [Test]
        public void GameCorner_Slots_CostTheStake_AndSometimesPayThePrize()
        {
            var outcomes = new HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                var (state, library, items) = MakeRun(money: 10);
                var ev = RoadEvents.Build(RoadEventKind.GameCorner, state, library, items, seed);
                RoadEvents.Resolve(ev, 0, state, library, items);
                outcomes.Add(state.Money);
            }
            CollectionAssert.AreEquivalent(
                new[] { 10 - RoadEvents.SlotsStake, 10 - RoadEvents.SlotsStake + RoadEvents.SlotsPrize }, outcomes,
                "a spin either loses the stake or wins the prize, and over 50 seeds both happen");
        }

        [Test]
        public void GameCorner_Slots_AreRefused_WithoutTheStake()
        {
            var (state, library, items) = MakeRun(money: RoadEvents.SlotsStake - 1);
            var ev = RoadEvents.Build(RoadEventKind.GameCorner, state, library, items, seed: 1);

            Assert.IsFalse(ev.Choices[0].Available);
            Assert.IsNull(RoadEvents.Resolve(ev, 0, state, library, items));
            Assert.AreEqual(RoadEvents.SlotsStake - 1, state.Money);
            Assert.IsFalse(ev.IsResolved, "a refused choice leaves the others open");
        }

        // ---- Traveling Trader -----------------------------------------------------------------

        [Test]
        public void Trader_SwapsTheLeastGrownMon_ForADifferentSpeciesATierUp_AtTheSameExp()
        {
            var evolved = MakeSpecies(10, "EvolvedTwo", tier: 2);
            var starter = MakeSpecies(1, "Starter", evolvesInto: evolved);
            var tierTwo = MakeSpecies(11, "TierTwo", tier: 2);
            var legendTwo = MakeSpecies(12, "LegendTwo", tier: 2, legendary: true);
            var library = MakeLibrary(starter, evolved, tierTwo, legendTwo, MakeSpecies(13, "TierOne"));
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(starter, "lead", 5, library));
            var benched = ExperienceResolver.CreateAtExp(starter, "benched", 1, library);
            state.Box.Add(benched);
            var band = MakeItems("band").AllItems[0];
            state.Items.Add(band.Id);
            HeldItems.Equip(state, benched, band, library);

            var ev = RoadEvents.Build(RoadEventKind.TravelingTrader, state, library, MakeItems("band"), seed: 9);
            RoadEvents.Resolve(ev, 0, state, library, null);

            var received = state.Box.Single();
            Assert.AreEqual(tierTwo.Id, received.SpeciesId, "a tier up — never an evolved form or a Legendary");
            Assert.AreEqual(1, received.Exp, "the traded mon's EXP");
            Assert.AreEqual("lead", state.LineUp.Single().InstanceId, "the more-grown mon is kept");
            CollectionAssert.AreEqual(new[] { band.Id }, state.Items, "the traded mon's item stays with the run");
        }

        [Test]
        public void Trader_CanTakeTheOnlyMon_AndTheLineUpKeepsItsSlotFilled()
        {
            var (state, library, items) = MakeRun();
            var ev = RoadEvents.Build(RoadEventKind.TravelingTrader, state, library, items, seed: 2);

            RoadEvents.Resolve(ev, 0, state, library, items);

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreNotEqual(StarterId, state.LineUp[0].SpeciesId);
            Assert.AreNotEqual(LegendId, state.LineUp[0].SpeciesId, "never a Legendary");
        }

        [Test]
        public void Trader_Decline_ChangesNothing()
        {
            var (state, library, items) = MakeRun();
            var lead = state.LineUp[0];
            var ev = RoadEvents.Build(RoadEventKind.TravelingTrader, state, library, items, seed: 2);

            RoadEvents.Resolve(ev, 1, state, library, items);

            Assert.AreSame(lead, state.LineUp.Single());
        }

        // ---- Team Rocket Ambush ---------------------------------------------------------------

        [Test]
        public void Rocket_Toll_TakesHalfTheMoney_RoundedUp()
        {
            var (state, library, items) = MakeRun(money: 7);
            var ev = RoadEvents.Build(RoadEventKind.RocketAmbush, state, library, items, seed: 1);

            RoadEvents.Resolve(ev, 0, state, library, items);

            Assert.AreEqual(3, state.Money);
        }

        [Test]
        public void Rocket_HandOverAnItem_TakesOneFromTheBag_AndIsRefusedWhenItsEmpty()
        {
            var (state, library, items) = MakeRun();
            var empty = RoadEvents.Build(RoadEventKind.RocketAmbush, state, library, items, seed: 1);
            Assert.IsFalse(empty.Choices[1].Available);

            state.Items.AddRange(new[] { "band", "vest" });
            var ev = RoadEvents.Build(RoadEventKind.RocketAmbush, state, library, items, seed: 1);
            RoadEvents.Resolve(ev, 1, state, library, items);

            Assert.AreEqual(1, state.Items.Count);
            Assert.AreEqual(10, state.Money);
        }

        [Test]
        public void Rocket_GrabTheLoot_EitherGainsAnItem_OrCostsMorale()
        {
            bool gained = false, caught = false;
            for (int seed = 0; seed < 50; seed++)
            {
                var (state, library, items) = MakeRun();
                var ev = RoadEvents.Build(RoadEventKind.RocketAmbush, state, library, items, seed);
                RoadEvents.Resolve(ev, 2, state, library, items);

                if (state.Items.Count == 1)
                {
                    gained = true;
                    Assert.AreEqual(RunState.StartingMorale, state.Morale, "a clean getaway costs nothing");
                }
                else
                {
                    caught = true;
                    Assert.AreEqual(RunState.StartingMorale - 1, state.Morale);
                }
            }
            Assert.IsTrue(gained && caught, "over 50 seeds both outcomes happen");
        }

        // ---- held items that take a stat away -------------------------------------------------

        [Test]
        public void AnItemThatTakesHealth_NeverLeavesAMonBelowOneHealth()
        {
            var (state, library, _) = MakeRun();
            var mon = state.LineUp[0];
            var brutal = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            brutal.Id = "brutal";
            brutal.StatModifiers = new Stats { Attack = 5, Health = -100 };
            state.Items.Add(brutal.Id);

            HeldItems.Equip(state, mon, brutal, library);

            Assert.AreEqual(ExperienceResolver.MinHeldItemHealth, mon.CurrentStats.Health);
            Assert.AreEqual(ExperienceResolver.MinHeldItemHealth, mon.CurrentHP);
        }
    }
}
