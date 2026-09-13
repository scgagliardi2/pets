using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Meta;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for the multi-Location run loop (ADR 0006): the Region Hub's
    /// offer, entering a Location, banking a badge, and what ends a run.
    ///
    /// Until this existed a run was one Location long — beating its Gym ended everything at Home
    /// (ADR 0003) — which is why the growth curve had nowhere to happen.</summary>
    public class RunLoopTests
    {
        [Test]
        public void EveryLocationType_HasATypeBiasAndAName()
        {
            foreach (var type in LocationCatalog.AllTypes)
            {
                var entry = LocationCatalog.For(type);
                Assert.IsNotEmpty(entry.DisplayName, $"{type} needs a name to show the player");
                Assert.IsNotEmpty(entry.Flavor);
                Assert.IsNotEmpty(entry.TypeBias, $"{type} needs a type bias — it's what its wildlife is drawn from");
            }
        }

        [Test]
        public void TheHub_OffersThreeDistinctLocations()
        {
            var offers = RegionHubGenerator.Offers(new RunState { RunSeed = 12345 });

            Assert.AreEqual(RegionHubGenerator.OfferCount, offers.Count);
            CollectionAssert.AllItemsAreUnique(offers.Select(o => o.Type),
                "a choice between two of the same Location isn't a choice");
        }

        /// <summary>Leaving the hub and coming back must show the same three Locations — otherwise
        /// the choice is a reroll button, and the screen's whole job goes away.</summary>
        [Test]
        public void TheHubsOffer_IsTheSameEveryTimeItIsAsked_ForTheSameRunAndLocation()
        {
            var run = new RunState { RunSeed = 777, RegionIndex = 3 };

            var first = RegionHubGenerator.Offers(run);
            var second = RegionHubGenerator.Offers(run);

            CollectionAssert.AreEqual(first.Select(o => o.Type), second.Select(o => o.Type));
            CollectionAssert.AreEqual(first.Select(o => o.MapSeed), second.Select(o => o.MapSeed));
        }

        [Test]
        public void TheHubsOffer_ChangesBetweenLocations()
        {
            var run = new RunState { RunSeed = 777, RegionIndex = 1 };
            var first = RegionHubGenerator.Offers(run);

            run.RegionIndex = 2;
            var second = RegionHubGenerator.Offers(run);

            CollectionAssert.AreNotEqual(first.Select(o => o.MapSeed), second.Select(o => o.MapSeed));
        }

        [Test]
        public void StartingALocation_GeneratesItsMap_AndSetsWhatTheWildlifeIsDrawnFrom()
        {
            var run = new RunState { RunSeed = 4242 };
            var offer = new RegionHubGenerator.Offer(LocationType.Sea, mapSeed: 99);

            run.StartLocation(offer, RegionMapGenerator.DefaultLayerCount);

            Assert.AreEqual(LocationType.Sea, run.CurrentLocation);
            Assert.IsNotNull(run.LocationMap);
            Assert.AreEqual(99, run.LocationMap.Seed);
            Assert.IsFalse(run.IsBetweenLocations);
            CollectionAssert.IsEmpty(run.VisitedMapNodeIds, "a new Location starts its walk over");
        }

        [Test]
        public void CompletingALocation_BanksABadge_AndSendsTheRunBackToTheHub()
        {
            var run = new RunState();
            run.StartLocation(new RegionHubGenerator.Offer(LocationType.Cave, 5), RegionMapGenerator.DefaultLayerCount);
            run.VisitedMapNodeIds.Add("L1-0");

            run.CompleteLocation();

            Assert.AreEqual(1, run.Badges);
            Assert.AreEqual(2, run.RegionIndex, "the next Location is a tier harder");
            Assert.IsTrue(run.IsBetweenLocations, "no map means the hub is where the run resumes");
            CollectionAssert.IsEmpty(run.VisitedMapNodeIds);
            Assert.IsFalse(run.IsRunWon);
        }

        /// <summary>The run's win condition, which design doc §20 left open: a fixed badge count,
        /// one per Location.</summary>
        [Test]
        public void ARunIsWon_OnTheLastLocationsBadge_AndNotBefore()
        {
            var run = new RunState();
            var visited = new List<LocationType>();

            for (int location = 0; location < RegionTier.RegionsPerRun; location++)
            {
                var offer = RegionHubGenerator.Offers(run)[0];
                visited.Add(offer.Type);
                run.StartLocation(offer, RegionMapGenerator.DefaultLayerCount);
                Assert.IsFalse(run.IsRunWon, $"the run can't be won inside Location {location + 1}");
                run.CompleteLocation();
            }

            Assert.IsTrue(run.IsRunWon);
            Assert.AreEqual(RegionTier.RegionsPerRun, run.Badges);
            Assert.AreEqual(RegionTier.RegionsPerRun, visited.Count);
        }

        /// <summary>The two halves of the model meeting: a full run's worth of Locations is also a
        /// full run's worth of levels (GrowthAndEvolutionTests covers the curve itself), and the
        /// opposition in the last one is built off that rather than off base stats.</summary>
        [Test]
        public void ByTheLastLocation_TheOppositionHasKeptPaceWithTheParty()
        {
            var run = new RunState { RunExp = LevelCurve.TotalExpForLevel(LevelCurve.MaxLevel) };
            run.Badges = RegionTier.RegionsPerRun - 1;
            run.RegionIndex = RegionTier.RegionsPerRun;

            Assert.AreEqual(LevelCurve.MaxLevel, RegionTier.PartyLevel(run));
            Assert.AreEqual(LevelCurve.MaxLevel + RegionTier.GymLevelDelta,
                RegionTier.EnemyLevel(run, NodeType.Gym));
            Assert.AreEqual(2, RegionTier.MaxEvolutionStageFor(run.RegionIndex),
                "the last Location's opposition is fully evolved");
            Assert.IsTrue(RegionTier.AllowsLegendaries(run.RegionIndex, NodeType.Gym));
        }
    }
}
