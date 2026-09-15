using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for the run as a whole (ADR 0007): the Region Hub's Locations and
    /// offers, earning badges toward an eight-badge win, and the shape of the difficulty curve.
    ///
    /// The curve tests pin *shape*, not exact numbers: that enemies climb every badge, that the
    /// evolution levels land in the third and sixth Locations, and that what a Location pays keeps a
    /// player on the level curve. A tuning change that breaks one of those should fail here rather
    /// than be discovered eight Gyms into a playtest.</summary>
    public class RunProgressionTests
    {
        // ---- RunState: Locations and badges ---------------------------------------------------

        [Test]
        public void RunState_StartsAtTheRegionHub()
        {
            var run = new RunState();

            Assert.IsNull(run.CurrentLocation);
            Assert.AreEqual(0, run.BadgeCount);
            Assert.IsTrue(run.NeedsLocationChoice);
            Assert.IsFalse(run.IsRunWon);
            Assert.AreEqual(RunState.StartingMorale, run.Morale);
        }

        [Test]
        public void TravelTo_SetsTheLocation_AndDropsTheOldMap()
        {
            var run = new RunState { LocationMap = LocationMapGenerator.Generate(seed: 1) };
            run.VisitedMapNodeIds.Add("L0-0");

            run.TravelTo(LocationType.Sea);

            Assert.AreEqual(LocationType.Sea, run.CurrentLocation);
            Assert.IsNull(run.LocationMap, "the new Location's map is generated when the Map scene shows it");
            Assert.IsEmpty(run.VisitedMapNodeIds);
            Assert.IsFalse(run.NeedsLocationChoice);
        }

        [Test]
        public void EarnBadge_CompletesTheLocation_RefillsMorale_AndReturnsToTheHub()
        {
            var run = new RunState();
            run.TravelTo(LocationType.Cave);
            run.LocationMap = LocationMapGenerator.Generate(seed: 2);
            run.Morale = 1;
            run.CenterStock = new PokemonCenterStock { NodeId = "center" };

            run.EarnBadge();

            Assert.AreEqual(1, run.BadgeCount);
            Assert.AreEqual(LocationType.Cave, run.CompletedLocations[0]);
            Assert.IsNull(run.CurrentLocation);
            Assert.IsNull(run.LocationMap);
            Assert.AreEqual(RunState.StartingMorale, run.Morale, "a badge refills Morale");
            Assert.IsNull(run.CenterStock, "a Location's Pokémon Center shelf doesn't carry into the next Location");
            Assert.IsTrue(run.NeedsLocationChoice);
        }

        /// <summary>A Map scene opened on its own fights Forest encounters with no Location chosen;
        /// beating its Gym must still count.</summary>
        [Test]
        public void EarnBadge_WithNoLocationChosen_CreditsTheFallback()
        {
            var run = new RunState();

            run.EarnBadge();

            Assert.AreEqual(1, run.BadgeCount);
            Assert.AreEqual(LocationCatalog.Fallback, run.CompletedLocations[0]);
        }

        [Test]
        public void IsRunWon_OnlyAfterTheFinalBadge()
        {
            var run = new RunState();
            for (int i = 0; i < RunProgression.BadgesToWin - 1; i++)
            {
                run.TravelTo(LocationType.Town);
                run.EarnBadge();
                Assert.IsFalse(run.IsRunWon, $"won after only {run.BadgeCount} badges");
            }

            run.TravelTo(LocationType.Town);
            run.EarnBadge();

            Assert.AreEqual(RunProgression.BadgesToWin, run.BadgeCount);
            Assert.IsTrue(run.IsRunWon);
            Assert.IsFalse(run.NeedsLocationChoice, "a won run has no next Location to pick");
        }

        [Test]
        public void NeedsLocationChoice_IsFalse_ForABrokenRun()
        {
            Assert.IsFalse(new RunState { Morale = 0 }.NeedsLocationChoice);
        }

        // ---- LocationCatalog ------------------------------------------------------------------

        [Test]
        public void LocationCatalog_HasANamedEntryWithATypeBias_ForEveryLocationType()
        {
            var types = Enum.GetValues(typeof(LocationType)).Cast<LocationType>().ToList();

            Assert.AreEqual(types.Count, LocationCatalog.All.Count);
            foreach (var type in types)
            {
                var entry = LocationCatalog.Get(type);
                Assert.IsFalse(string.IsNullOrEmpty(entry.DisplayName), $"{type} has no name");
                Assert.IsFalse(string.IsNullOrEmpty(entry.Flavor), $"{type} has no flavor line");
                Assert.IsNotEmpty(entry.TypeBias, $"{type} has no type bias");
            }
        }

        /// <summary>Derived from the seed rather than stored, so it has to come out the same every
        /// time it's asked — leaving the hub for the Team screen must not re-roll the choice.</summary>
        [Test]
        public void OffersFor_IsThreeDistinctLocations_TheSameEachTimeItsAsked()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var run = new RunState { RunSeed = seed };

                var offers = LocationCatalog.OffersFor(run);

                Assert.AreEqual(LocationCatalog.OfferCount, offers.Count, $"seed {seed}");
                CollectionAssert.AllItemsAreUnique(offers, $"seed {seed}");
                CollectionAssert.AreEqual(offers, LocationCatalog.OffersFor(run), $"seed {seed}");
            }
        }

        [Test]
        public void OffersFor_NeverOffersTheLocationJustCompleted()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var run = new RunState { RunSeed = seed };
                run.TravelTo(LocationType.Forest);
                run.EarnBadge();

                CollectionAssert.DoesNotContain(LocationCatalog.OffersFor(run), LocationType.Forest, $"seed {seed}");
            }
        }

        [Test]
        public void OffersFor_ChangesAsTheRunProgresses()
        {
            int changed = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = new RunState { RunSeed = seed };
                var before = LocationCatalog.OffersFor(run);
                run.TravelTo(before[0]);
                run.EarnBadge();
                if (!before.SequenceEqual(LocationCatalog.OffersFor(run)))
                {
                    changed++;
                }
            }
            Assert.Greater(changed, 0, "every badge showing the same three Locations would make the choice meaningless");
        }

        // ---- the curve ------------------------------------------------------------------------

        [Test]
        public void Enemies_ClimbWithEveryBadge_AndTheGymLeadsItsLocation()
        {
            int lastLayer = LocationMapGenerator.ChoiceLayerCount;
            for (int badges = 0; badges < RunProgression.BadgesToWin; badges++)
            {
                Assert.GreaterOrEqual(RunProgression.GymExp(badges), RunProgression.WildExp(badges, lastLayer),
                    $"the Gym should be at least as strong as the Location's toughest wild mons (badge {badges})");
                Assert.GreaterOrEqual(RunProgression.WildExp(badges, lastLayer), RunProgression.WildExp(badges, 1),
                    "wild mons shouldn't get weaker deeper into a Location");

                if (badges == 0)
                {
                    continue;
                }
                Assert.Greater(RunProgression.WildExp(badges, 1), RunProgression.WildExp(badges - 1, 1));
                Assert.Greater(RunProgression.GymExp(badges), RunProgression.GymExp(badges - 1));
                Assert.GreaterOrEqual(RunProgression.MaxTier(badges) ?? SpeciesTier.MaxTier,
                    RunProgression.MaxTier(badges - 1) ?? SpeciesTier.MaxTier,
                    "the tier the pool draws from shouldn't fall back");
                Assert.GreaterOrEqual(RunProgression.WildEncounterSize(badges), RunProgression.WildEncounterSize(badges - 1));
                Assert.GreaterOrEqual(RunProgression.GymTeamSize(badges, 1), RunProgression.GymTeamSize(badges - 1, 1));
            }
            Assert.IsNull(RunProgression.MaxTier(RunProgression.BadgesToWin - 1), "the final Location lifts the tier cap");
        }

        /// <summary>A starter evolves for the first time in the third Location and reaches its final
        /// form in the sixth — the mainline rhythm, spread across an eight-badge run. At one EXP a
        /// win and twelve to an evolution, that follows from how many fights a Location holds rather
        /// than from a fitted curve. Pinned here so a change to either number is a decision rather
        /// than a surprise (ADR 0009).</summary>
        [Test]
        public void Evolutions_LandInTheThirdAndSixthLocations()
        {
            AssertEvolutionFallsInLocation(1, expectedLocation: 3);
            AssertEvolutionFallsInLocation(2, expectedLocation: 6);
        }

        private static void AssertEvolutionFallsInLocation(int evolutionNumber, int expectedLocation)
        {
            int expNeeded = evolutionNumber * ExperienceResolver.ExpPerEvolution;
            // The Location a mon fighting a typical path is in when it earns its nth evolution.
            int location = 1 + (expNeeded - 1) / TypicalExpPerLocation;
            Assert.AreEqual(expectedLocation, location,
                $"evolution {evolutionNumber} needs {expNeeded} EXP, which a typical path reaches in Location {location}");
        }

        /// <summary>What a typical path through one Location pays: a few wild wins and the Gym. Every
        /// win pays one point (BattleRewardResolver), so this is just how many of those a Location
        /// holds.</summary>
        private const int TypicalExpPerLocation = RunProgression.ExpPerBadge;

        /// <summary>The opposition and the player have to climb at the same rate, or the run turns
        /// into a wall or a stroll. Both are now the same currency — EXP — so this is a direct
        /// comparison rather than a fitted one: a Location pays about
        /// <see cref="TypicalExpPerLocation"/>, and RunProgression.ExpPerBadge is what it pitches the
        /// next Location forward by.</summary>
        [Test]
        public void ExpectedRewards_KeepPaceWithTheOpposition_AtEveryBadge()
        {
            // A fight on every layer a player walks. The Pokémon Center pays no EXP (ADR 0013), so a
            // player who stops there gives that point up for whatever their money buys — an item or a
            // Pokémon, which is the Center's side of the trade and not something this test can count.
            const double ExpectedWildWins = 3.0;

            double perLocation = ExpectedWildWins * BattleRewardResolver.ExpPerWin
                + BattleRewardResolver.ExpPerWin;

            Assert.That(perLocation, Is.InRange(RunProgression.ExpPerBadge - 2, RunProgression.ExpPerBadge + 2),
                $"a Location pays {perLocation} EXP but pitches the next one {RunProgression.ExpPerBadge} forward");

            for (int badges = 1; badges < RunProgression.BadgesToWin; badges++)
            {
                int playerExp = (int)Math.Round(perLocation * badges);
                int gym = RunProgression.GymExp(badges);
                Assert.That(playerExp - gym, Is.InRange(-RunProgression.ExpPerBadge, RunProgression.ExpPerBadge),
                    $"Location {badges + 1}: the player is on {playerExp} EXP against a Gym on {gym}");
            }
        }

        private static RunState RunWithBadges(int badges)
        {
            var run = new RunState();
            for (int i = 0; i < badges; i++)
            {
                run.CompletedLocations.Add(LocationType.Town);
            }
            return run;
        }
    }
}
