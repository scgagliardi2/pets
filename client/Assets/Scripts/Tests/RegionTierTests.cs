using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for how hard a Location's opposition is (Meta/RegionTier, ADR
    /// 0006): the level enemies are built at, and the slice of the roster they're drawn from.
    ///
    /// Both are new. Every enemy in the game used to be a base-stats instance of any of the 183
    /// species, which is why a run's difficulty was a species lottery in Location 1 and nothing at
    /// all by Location 3.</summary>
    public class RegionTierTests
    {
        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name, int stage = 0,
            bool legendary = false, PokemonType type = PokemonType.Grass, int health = 50)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = type;
            species.BaseAttack = 50;
            species.BaseHealth = health;
            species.BaseSpeed = 50;
            species.EvolutionStage = stage;
            species.IsLegendary = legendary;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary(params PokemonSpeciesDefinitionAsset[] species)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = species.ToList();
            return library;
        }

        private static RunState RunAtLevel(int level, int regionIndex)
        {
            return new RunState
            {
                RunExp = LevelCurve.TotalExpForLevel(level),
                RegionIndex = regionIndex
            };
        }

        [Test]
        public void MaxEvolutionStage_OpensUpAsTheRunGoesOn()
        {
            Assert.AreEqual(0, RegionTier.MaxEvolutionStageFor(1), "Location 1 is base forms");
            Assert.AreEqual(0, RegionTier.MaxEvolutionStageFor(2));
            Assert.AreEqual(1, RegionTier.MaxEvolutionStageFor(3));
            Assert.AreEqual(2, RegionTier.MaxEvolutionStageFor(RegionTier.RegionsPerRun));
        }

        [Test]
        public void Legendaries_AreTheFinalGymsAlone()
        {
            Assert.IsFalse(RegionTier.AllowsLegendaries(1, NodeType.PvE));
            Assert.IsFalse(RegionTier.AllowsLegendaries(RegionTier.RegionsPerRun, NodeType.PvE),
                "a Legendary is not wildlife, even in the last Location");
            Assert.IsFalse(RegionTier.AllowsLegendaries(RegionTier.RegionsPerRun - 1, NodeType.Gym));
            Assert.IsTrue(RegionTier.AllowsLegendaries(RegionTier.RegionsPerRun, NodeType.Gym));
        }

        [Test]
        public void EnemyLevel_TracksThePartyAndGetsHarderTowardTheGym()
        {
            var run = RunAtLevel(8, regionIndex: 3);

            int wild = RegionTier.EnemyLevel(run, NodeType.PvE);
            int trainer = RegionTier.EnemyLevel(run, NodeType.PvP);
            int gym = RegionTier.EnemyLevel(run, NodeType.Gym);

            Assert.AreEqual(8 + RegionTier.WildLevelDelta, wild);
            Assert.AreEqual(8 + RegionTier.TrainerLevelDelta, trainer);
            Assert.AreEqual(8 + RegionTier.GymLevelDelta, gym);
            Assert.Less(wild, gym, "the Gym is the fight a Location is tested by");
        }

        [Test]
        public void EnemyLevel_NeverDropsBelowTheStartingLevel()
        {
            Assert.AreEqual(LevelCurve.StartingLevel, RegionTier.EnemyLevel(new RunState(), NodeType.PvE));
        }

        [Test]
        public void Filter_KeepsBaseFormsAndDropsLegendaries_ForAnEarlyLocation()
        {
            var basic = MakeSpecies(1, "Basic");
            var evolved = MakeSpecies(2, "Evolved", stage: 1);
            var legend = MakeSpecies(3, "Legend", legendary: true);
            var tier = RegionTier.For(RunAtLevel(2, regionIndex: 1), NodeType.PvE);

            var pool = RegionTier.Filter(new List<PokemonSpeciesDefinitionAsset> { basic, evolved, legend }, tier);

            CollectionAssert.AreEquivalent(new[] { basic }, pool);
        }

        /// <summary>A type bias with nothing in it at this tier should make an encounter odd, not
        /// impossible — the alternative is a node that can't resolve.</summary>
        [Test]
        public void Filter_FallsBackToTheWholePool_WhenNothingSurvivesTheTier()
        {
            var evolved = MakeSpecies(2, "Evolved", stage: 2);
            var tier = RegionTier.For(RunAtLevel(2, regionIndex: 1), NodeType.PvE);

            var pool = RegionTier.Filter(new List<PokemonSpeciesDefinitionAsset> { evolved }, tier);

            CollectionAssert.AreEquivalent(new[] { evolved }, pool);
        }

        [Test]
        public void WildEncounters_AreBuiltAtTheTiersLevel_FromTheSpeciesItAllows()
        {
            var basic = MakeSpecies(1, "Basic");
            var evolved = MakeSpecies(2, "Evolved", stage: 1);
            var library = MakeLibrary(basic, evolved);
            var run = RunAtLevel(8, regionIndex: 1);
            var tier = RegionTier.For(run, NodeType.PvE);

            var lineUp = EncounterGenerator.GenerateWildLineUp(
                library, new[] { PokemonType.Grass }, seed: 5, instanceIdPrefix: "wild", tier);

            Assert.IsTrue(lineUp.All(m => m.SpeciesId == basic.Id),
                "an early Location's wildlife is base forms");
            var expected = StatGrowth.StatsFor(
                new Stats { Attack = basic.BaseAttack, Health = basic.BaseHealth, Speed = basic.BaseSpeed },
                tier.Level);
            Assert.IsTrue(lineUp.All(m => m.CurrentStats.Attack == expected.Attack),
                "wild mons scale with the run rather than staying at base stats");
            Assert.AreEqual(8 + RegionTier.WildLevelDelta, ExperienceResolver.LevelOf(lineUp[0]));
        }

        [Test]
        public void AGymTeam_IsBuiltAtItsOwnTier_OnTopOfTheHealthBonus()
        {
            var basic = MakeSpecies(1, "Basic", health: 100);
            var library = MakeLibrary(basic, MakeSpecies(2, "Legend", legendary: true));
            var run = RunAtLevel(8, regionIndex: 3);
            var tier = RegionTier.For(run, NodeType.Gym);

            var team = GymTeamGenerator.Generate(library, count: 2, seed: 7, tier);

            Assert.IsTrue(team.All(m => m.SpeciesId == basic.Id), "no Legendaries before the last Gym");
            int scaled = StatGrowth.StatsFor(
                new Stats { Attack = basic.BaseAttack, Health = basic.BaseHealth, Speed = basic.BaseSpeed },
                tier.Level).Health;
            int expected = scaled + (int)(scaled * GymTeamGenerator.HealthBonusPercent);
            Assert.IsTrue(team.All(m => m.CurrentStats.Health == expected));
        }

        /// <summary>The whole point of the tier: the opposition keeps pace with the party instead of
        /// being left behind by it. Before this, a party a few Locations in beat every fight on the
        /// board without trying.</summary>
        [Test]
        public void OppositionKeepsPace_AsTheRunGrows()
        {
            var early = RegionTier.EnemyLevel(RunAtLevel(3, regionIndex: 1), NodeType.PvE);
            var late = RegionTier.EnemyLevel(RunAtLevel(LevelCurve.MaxLevel, regionIndex: 6), NodeType.PvE);

            Assert.Greater(late, early);
            Assert.AreEqual(LevelCurve.MaxLevel + RegionTier.WildLevelDelta, late);
        }
    }
}
