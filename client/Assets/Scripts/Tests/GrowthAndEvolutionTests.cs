using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for the growth model (ADR 0006): what a level costs
    /// (Meta/LevelCurve), what it's worth (Data/StatGrowth), when a mon evolves
    /// (Meta/ExperienceResolver), and the run-level floor that keeps a late catch playable
    /// (Meta/RunProgression).
    ///
    /// The last two tests here are the design's own: they walk six Locations' worth of nodes and
    /// assert the run lands on the level cap with its starter fully evolved. Those are the numbers
    /// the model was sized for, and they're the ones that silently rot the first time a node weight
    /// or an award changes — which is exactly what happened to the model this one replaced.</summary>
    public class GrowthAndEvolutionTests
    {
        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name,
            int attack = 50, int health = 50, int speed = 50)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Normal;
            species.BaseAttack = attack;
            species.BaseHealth = health;
            species.BaseSpeed = speed;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary(params PokemonSpeciesDefinitionAsset[] species)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = species.ToList();
            return library;
        }

        /// <summary>A two-stage line: "Basic" evolves into "Evolved", both registered so the
        /// resolver can look either up by id.</summary>
        private static (PokemonSpeciesDefinitionAsset first, PokemonSpeciesDefinitionAsset second, PokemonSpeciesLibrary library)
            MakeEvolutionLine()
        {
            var second = MakeSpecies(2, "Evolved", attack: 80, health: 80, speed: 80);
            var first = MakeSpecies(1, "Basic", attack: 40, health: 40, speed: 40);
            first.EvolvesInto = second;
            second.EvolutionStage = 1;
            return (first, second, MakeLibrary(first, second));
        }

        private static int ExpForLevel(int level) => LevelCurve.TotalExpForLevel(level);

        // --- LevelCurve ------------------------------------------------------------------------

        [Test]
        public void LevelCurve_CostsRise_SoEarlyLevelsArriveFasterThanLateOnes()
        {
            for (int level = LevelCurve.StartingLevel; level < LevelCurve.MaxLevel - 1; level++)
            {
                Assert.Less(LevelCurve.ExpToAdvanceFrom(level), LevelCurve.ExpToAdvanceFrom(level + 1),
                    $"level {level + 1} should cost more than level {level}");
            }
        }

        [Test]
        public void LevelCurve_LevelForExp_IsTheInverseOfTotalExpForLevel()
        {
            for (int level = LevelCurve.StartingLevel; level <= LevelCurve.MaxLevel; level++)
            {
                int exact = ExpForLevel(level);
                Assert.AreEqual(level, LevelCurve.LevelForExp(exact),
                    $"{exact} EXP should be exactly level {level}");
                if (level > LevelCurve.StartingLevel)
                {
                    Assert.AreEqual(level - 1, LevelCurve.LevelForExp(exact - 1),
                        "one EXP short of a level is still the level below");
                }
            }
        }

        /// <summary>EXP past the cap is banked, not refused — a mon that caps in Location 5 is still
        /// paid for Location 6's fights, so no award site has to know the cap exists.</summary>
        [Test]
        public void LevelCurve_ExpPastTheCap_LeavesTheMonAtTheCap()
        {
            Assert.AreEqual(LevelCurve.MaxLevel, LevelCurve.LevelForExp(ExpForLevel(LevelCurve.MaxLevel) * 10));
            Assert.AreEqual((0, 0), LevelCurve.ProgressInLevel(ExpForLevel(LevelCurve.MaxLevel)),
                "a capped mon has no progress bar to draw");
        }

        [Test]
        public void LevelCurve_ProgressInLevel_ReportsHowFarThroughTheCurrentLevelAMonIs()
        {
            int intoLevelThree = ExpForLevel(3) + 2;

            var (into, cost) = LevelCurve.ProgressInLevel(intoLevelThree);

            Assert.AreEqual(2, into);
            Assert.AreEqual(LevelCurve.ExpToAdvanceFrom(3), cost);
        }

        // --- StatGrowth ------------------------------------------------------------------------

        [Test]
        public void StatGrowth_AtTheStartingLevel_IsTheSheetsStats_WithTheHealthScalar()
        {
            var stats = StatGrowth.StatsFor(new Stats { Attack = 40, Health = 50, Speed = 60 },
                StatGrowth.StartingLevel);

            Assert.AreEqual(40, stats.Attack);
            Assert.AreEqual(50 * StatGrowth.HealthScalar, stats.Health);
            Assert.AreEqual(60, stats.Speed);
        }

        /// <summary>The flat +10-per-EXP model this replaced converged the whole roster on the same
        /// numbers — after enough growth a Caterpie and a Charizard were indistinguishable, which is
        /// the opposite of what a collection game wants. Growth proportional to the species' own
        /// base keeps them apart.</summary>
        [Test]
        public void StatGrowth_KeepsAStrongSpeciesAheadOfAWeakOne_RatherThanConvergingThem()
        {
            var strong = new Stats { Attack = 100, Health = 100, Speed = 50 };
            var weak = new Stats { Attack = 20, Health = 20, Speed = 50 };
            int gapAtStart = StatGrowth.StatsFor(strong, StatGrowth.StartingLevel).Attack
                - StatGrowth.StatsFor(weak, StatGrowth.StartingLevel).Attack;

            int gapAtCap = StatGrowth.StatsFor(strong, LevelCurve.MaxLevel).Attack
                - StatGrowth.StatsFor(weak, LevelCurve.MaxLevel).Attack;

            Assert.Greater(gapAtCap, gapAtStart,
                "the stronger species should pull further ahead as both grow, not be caught up to");
        }

        /// <summary>Speed feeds the charge meter (battle-sim-spec.md §3-§4) against a fixed
        /// threshold, so growing it would end a run with every mon firing its passive every Step and
        /// no fast/slow distinction left. Evolving is what makes a mon faster.</summary>
        [Test]
        public void StatGrowth_DoesNotGrowSpeed_SoPassiveCadenceStaysASpeciesTrait()
        {
            var baseStats = new Stats { Attack = 50, Health = 50, Speed = 60 };

            Assert.AreEqual(60, StatGrowth.StatsFor(baseStats, LevelCurve.MaxLevel).Speed);
        }

        // --- Growth on a mon -------------------------------------------------------------------

        /// <summary>Stats are derived from species + level rather than accumulated, so the same
        /// total EXP has to produce the same stats however it was granted (ADR 0005's rule, kept).</summary>
        [Test]
        public void ExperienceResolver_StatsDependOnTotalExpOnly_NotOnHowItArrived()
        {
            var (species, _, library) = MakeEvolutionLine();
            var atOnce = PokemonInstanceFactory.Create(species, "at-once");
            var piecemeal = PokemonInstanceFactory.Create(species, "piecemeal");

            ExperienceResolver.GrantExp(atOnce, 6, library);
            ExperienceResolver.GrantExp(piecemeal, 2, library);
            ExperienceResolver.GrantExp(piecemeal, 4, library);

            Assert.AreEqual(2, ExperienceResolver.LevelOf(atOnce));
            Assert.AreEqual(atOnce.CurrentStats.Attack, piecemeal.CurrentStats.Attack);
            Assert.AreEqual(atOnce.CurrentStats.Health, piecemeal.CurrentStats.Health);
        }

        [Test]
        public void ExperienceResolver_EvolvesAtTheFirstEvolutionLevel_AndRebasesStatsOnTheNewSpecies()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            int firstEvolutionLevel = ExperienceResolver.EvolutionLevels[0];
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var below = ExperienceResolver.GrantExp(mon, ExpForLevel(firstEvolutionLevel) - 1, library);
            CollectionAssert.IsEmpty(below, "nothing should evolve before the threshold level");
            Assert.AreEqual(species.Id, mon.SpeciesId);

            var evolutions = ExperienceResolver.GrantExp(mon, 1, library);

            Assert.AreEqual(1, evolutions.Count);
            Assert.AreEqual("Basic", evolutions[0].FromName);
            Assert.AreEqual("Evolved", evolutions[0].ToName);
            Assert.AreEqual(evolved.Id, mon.SpeciesId);
            Assert.AreEqual(1, mon.TimesEvolved);

            var expected = StatGrowth.StatsFor(
                new Stats { Attack = evolved.BaseAttack, Health = evolved.BaseHealth, Speed = evolved.BaseSpeed },
                firstEvolutionLevel);
            Assert.AreEqual(expected.Attack, mon.CurrentStats.Attack,
                "stats should rebase on the new species, not keep the old base");
        }

        /// <summary>The thresholds are a sequence, not a repeating interval — reaching the first one
        /// must not run a mon up its whole chain and leave the middle stage never existing.</summary>
        [Test]
        public void ExperienceResolver_DoesNotChainStraightThroughTheNextEvolution()
        {
            var third = MakeSpecies(3, "Final");
            var second = MakeSpecies(2, "Middle");
            var first = MakeSpecies(1, "Basic");
            first.EvolvesInto = second;
            second.EvolvesInto = third;
            var library = MakeLibrary(first, second, third);
            var mon = PokemonInstanceFactory.Create(first, "mon-1");

            ExperienceResolver.GrantExp(mon, ExpForLevel(ExperienceResolver.EvolutionLevels[0]), library);
            Assert.AreEqual(second.Id, mon.SpeciesId, "the first threshold is one step, not the whole chain");

            ExperienceResolver.GrantExp(mon,
                ExpForLevel(ExperienceResolver.EvolutionLevels[1]) - mon.Exp, library);
            Assert.AreEqual(third.Id, mon.SpeciesId, "the second threshold is another step further");
            Assert.AreEqual(2, mon.TimesEvolved);
        }

        [Test]
        public void ExperienceResolver_LeavesAFinalFormAlone_HoweverMuchExpItEarns()
        {
            var species = MakeSpecies(1, "FinalForm");
            var library = MakeLibrary(species);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var evolutions = ExperienceResolver.GrantExp(mon, ExpForLevel(LevelCurve.MaxLevel), library);

            CollectionAssert.IsEmpty(evolutions);
            Assert.AreEqual(species.Id, mon.SpeciesId);
            Assert.AreEqual(0, mon.TimesEvolved);
            Assert.IsFalse(ExperienceResolver.CanEverEvolve(mon, library));
        }

        /// <summary>A curated base form whose real pre-evolution isn't in the roster (Pikachu, whose
        /// chain starts at Pichu) is stage 1 but has still evolved zero times — so it must reach its
        /// first evolution at the first threshold like anything else.</summary>
        [Test]
        public void ExperienceResolver_CountsEvolutionsPerInstance_NotFromTheSpeciesChainDepth()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            species.EvolutionStage = 1;
            evolved.EvolutionStage = 2;
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, ExpForLevel(ExperienceResolver.EvolutionLevels[0]), library);

            Assert.AreEqual(evolved.Id, mon.SpeciesId);
        }

        [Test]
        public void ExperienceResolver_MinLevel_HoldsAMonAboveWhatItsOwnExpBought()
        {
            var (species, _, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.SetMinLevel(mon, 3, library);

            Assert.AreEqual(0, mon.Exp, "the floor is not earned EXP");
            Assert.AreEqual(3, ExperienceResolver.LevelOf(mon));
            Assert.AreEqual(StatGrowth.StatsFor(
                new Stats { Attack = species.BaseAttack, Health = species.BaseHealth, Speed = species.BaseSpeed }, 3).Attack,
                mon.CurrentStats.Attack);
        }

        [Test]
        public void ExperienceResolver_MinLevel_NeverPullsAVeteranBackDown()
        {
            var (species, _, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");
            ExperienceResolver.GrantExp(mon, ExpForLevel(6), library);

            ExperienceResolver.SetMinLevel(mon, 3, library);

            Assert.AreEqual(6, ExperienceResolver.LevelOf(mon));
        }

        // --- The run's floor -------------------------------------------------------------------

        [Test]
        public void RunProgression_PaysTheLineUp_AndPullsTheBoxUpToTheFloor()
        {
            var (species, _, library) = MakeEvolutionLine();
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "lead"));
            state.Box.Add(PokemonInstanceFactory.Create(species, "boxed"));

            RunProgression.GrantToLineUp(state, ExpForLevel(5), library);

            Assert.AreEqual(ExpForLevel(5), state.LineUp[0].Exp, "the line-up earns the EXP");
            Assert.AreEqual(0, state.Box[0].Exp, "a mon in the Box didn't fight");
            Assert.AreEqual(5, ExperienceResolver.LevelOf(state.LineUp[0]));
            Assert.AreEqual(4, ExperienceResolver.LevelOf(state.Box[0]),
                "a Box mon is held one level behind the run rather than left at 1");
        }

        /// <summary>The rule that makes catching worth doing after Location 1: a mon that joins a
        /// run in progress arrives at the floor, which can be far enough along to run it up its
        /// whole chain at once.</summary>
        [Test]
        public void RunProgression_OnboardsANewMonAtTheFloor_EvolvingItAsFarAsThatReaches()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "lead"));
            RunProgression.GrantToLineUp(state, ExpForLevel(LevelCurve.MaxLevel), library);

            var joined = PokemonInstanceFactory.Create(species, "joined");
            var evolutions = RunProgression.Onboard(state, joined, library);

            Assert.AreEqual(state.FloorLevel, ExperienceResolver.LevelOf(joined));
            Assert.AreEqual(evolved.Id, joined.SpeciesId, "the floor is past its evolution level");
            Assert.AreEqual(1, evolutions.Count, "the caller is told what it evolved into");
        }

        [Test]
        public void RunState_FloorLevel_TrailsTheRunByOneLevel_AndNeverDropsBelowTheStart()
        {
            var state = new RunState();

            Assert.AreEqual(LevelCurve.StartingLevel, state.FloorLevel, "a fresh run floors at level 1");

            state.RunExp = ExpForLevel(7);
            Assert.AreEqual(6, state.FloorLevel);
        }

        // --- The design's own test: six Locations ----------------------------------------------

        /// <summary>One Location's worth of EXP as the map generator actually shapes it: five choice
        /// nodes the player walks (three wild fights, a trainer fight and a Pokémon Center — the
        /// weighted mix RegionMapGenerator produces) plus the mandatory Gym.</summary>
        private static void WalkOneLocation(RunState state, PokemonSpeciesLibrary library)
        {
            for (int i = 0; i < 3; i++)
            {
                BattleRewardResolver.GrantWinRewards(state, library, NodeType.PvE);
            }
            BattleRewardResolver.GrantWinRewards(state, library, NodeType.PvP);
            CampResolver.Resolve(state, library);
            BattleRewardResolver.GrantWinRewards(state, library, NodeType.Gym);
        }

        [Test]
        public void ARunOfSixLocations_FinishesAtTheLevelCap_AndNoSooner()
        {
            var (species, _, library) = MakeEvolutionLine();
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "starter"));
            var levelsByLocation = new List<int>();

            for (int location = 0; location < 6; location++)
            {
                WalkOneLocation(state, library);
                levelsByLocation.Add(ExperienceResolver.LevelOf(state.LineUp[0]));
            }

            Assert.AreEqual(LevelCurve.MaxLevel, levelsByLocation[5],
                "six Locations should land the run exactly on the cap");
            Assert.Less(levelsByLocation[4], LevelCurve.MaxLevel,
                "and not reach it a whole Location early");
            CollectionAssert.IsOrdered(levelsByLocation, "a run never goes backwards");
        }

        /// <summary>The arc the thresholds are for: a starter that is fully evolved for the back
        /// third of the run, rather than for all of it (the old model finished every evolution in
        /// Location 1) or none of it.</summary>
        [Test]
        public void ARunOfSixLocations_FullyEvolvesTheStarter_ByTheFourthLocation()
        {
            var third = MakeSpecies(3, "Final", attack: 120, health: 120, speed: 120);
            var second = MakeSpecies(2, "Middle", attack: 80, health: 80, speed: 80);
            var first = MakeSpecies(1, "Basic", attack: 40, health: 40, speed: 40);
            first.EvolvesInto = second;
            second.EvolvesInto = third;
            var library = MakeLibrary(first, second, third);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(first, "starter"));
            var speciesByLocation = new List<int>();

            for (int location = 0; location < 6; location++)
            {
                WalkOneLocation(state, library);
                speciesByLocation.Add(state.LineUp[0].SpeciesId);
            }

            Assert.AreEqual(first.Id, speciesByLocation[0], "still a base form through Location 1");
            Assert.AreEqual(second.Id, speciesByLocation[1], "first evolution lands in Location 2");
            Assert.AreEqual(third.Id, speciesByLocation[3], "final form by the end of Location 4");
        }
    }
}
