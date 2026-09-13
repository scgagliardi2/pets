using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
            run.NextBattleAttackBonusPercent = 0.2f;

            run.EarnBadge();

            Assert.AreEqual(1, run.BadgeCount);
            Assert.AreEqual(LocationType.Cave, run.CompletedLocations[0]);
            Assert.IsNull(run.CurrentLocation);
            Assert.IsNull(run.LocationMap);
            Assert.AreEqual(RunState.StartingMorale, run.Morale, "a badge refills Morale");
            Assert.AreEqual(0f, run.NextBattleAttackBonusPercent, "a Pokémon Center buff doesn't carry into the next Location");
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
                Assert.GreaterOrEqual(RunProgression.GymLevel(badges), RunProgression.WildLevel(badges, lastLayer),
                    $"the Gym should be at least as strong as the Location's toughest wild mons (badge {badges})");
                Assert.GreaterOrEqual(RunProgression.WildLevel(badges, lastLayer), RunProgression.WildLevel(badges, 1),
                    "wild mons shouldn't get weaker deeper into a Location");

                if (badges == 0)
                {
                    continue;
                }
                Assert.Greater(RunProgression.WildLevel(badges, 1), RunProgression.WildLevel(badges - 1, 1));
                Assert.Greater(RunProgression.GymLevel(badges), RunProgression.GymLevel(badges - 1));
                Assert.GreaterOrEqual(RunProgression.WildEncounterSize(badges), RunProgression.WildEncounterSize(badges - 1));
                Assert.GreaterOrEqual(RunProgression.GymTeamSize(badges, 1), RunProgression.GymTeamSize(badges - 1, 1));
            }
            Assert.IsNull(RunProgression.MaxBaseStatTotal(RunProgression.BadgesToWin - 1), "the final Location lifts the stat cap");
        }

        /// <summary>A starter should evolve for the first time in the third Location and for the
        /// second time in the sixth — the mainline rhythm, spread across an eight-badge run.</summary>
        [Test]
        public void EvolutionLevels_LandInTheThirdAndSixthLocations()
        {
            Assert.AreEqual(2, ExperienceResolver.EvolutionLevels.Length);
            AssertLevelFallsInLocation(ExperienceResolver.EvolutionLevels[0], location: 3);
            AssertLevelFallsInLocation(ExperienceResolver.EvolutionLevels[1], location: 6);
        }

        private static void AssertLevelFallsInLocation(int level, int location)
        {
            int badges = location - 1;
            Assert.GreaterOrEqual(level, RunProgression.BaselineLevel(badges), $"Lv {level} comes before Location {location}");
            Assert.Less(level, RunProgression.BaselineLevel(badges + 1), $"Lv {level} comes after Location {location}");
        }

        /// <summary>The EXP curve and the rewards were fitted together: a player who starts a
        /// Location a level above its baseline and plays a typical path through it — a few wild
        /// wins, sometimes a Pokémon Center, and the Gym — should come out two to four levels up,
        /// at every badge. Too few and the late game becomes a wall; too many and it's a stroll.</summary>
        [Test]
        public void ExpectedRewards_KeepPaceWithTheLevelCurve_AtEveryBadge()
        {
            const double ExpectedWildWins = 2.5;
            const double ExpectedCenterVisits = 0.5;
            int layers = LocationMapGenerator.ChoiceLayerCount;

            for (int badges = 0; badges < RunProgression.BadgesToWin; badges++)
            {
                double exp = 0;
                for (int layer = 1; layer <= layers; layer++)
                {
                    exp += ExpectedWildWins / layers
                        * BattleRewardResolver.ExpForWin(FoesAt(RunProgression.WildLevel(badges, layer)), isGym: false);
                }
                exp += ExpectedCenterVisits * CampResolver.ExpFor(RunWithBadges(badges));
                exp += BattleRewardResolver.ExpForWin(FoesAt(RunProgression.GymLevel(badges)), isGym: true);

                int startLevel = RunProgression.BaselineLevel(badges) + 1;
                int endLevel = ExperienceResolver.LevelForExp(ExperienceResolver.ExpToReachLevel(startLevel) + (int)exp);

                Assert.That(endLevel - startLevel, Is.InRange(2, 4),
                    $"Location {badges + 1}: a typical path took the player from Lv {startLevel} to Lv {endLevel}");
            }
        }

        private static List<PokemonInstance> FoesAt(int level) =>
            new List<PokemonInstance> { new PokemonInstance { Exp = ExperienceResolver.ExpToReachLevel(level) } };

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
