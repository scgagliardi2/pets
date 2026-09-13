using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for the run layer (PLAN.md §6): line-up management, encounter and
    /// Gym generation, EXP/levels/evolution, catch-up, Camp, combining and the stubbed catch flow.
    /// Builds ScriptableObject content in-memory via CreateInstance rather than loading
    /// Assets/Content, so these tests don't depend on the curated roster's exact contents.
    /// RunProgressionTests covers the run-scale curve, the Region Hub's offers and badges.</summary>
    public class RunMetaTests
    {
        private static readonly PokemonType[] Grass = { PokemonType.Grass };

        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name, PokemonType type1, int attack = 10, int health = 50, int speed = 10)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = type1;
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

        [Test]
        public void RunState_SwapLeadAndSupport_ExchangesTheTwoActiveSlots()
        {
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(1, "Alpha", PokemonType.Fire), "lead"));
            state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(2, "Beta", PokemonType.Water), "support"));

            state.SwapLeadAndSupport();

            Assert.AreEqual(2, state.LineUp[0].SpeciesId, "The old Support should now lead");
            Assert.AreEqual(1, state.LineUp[1].SpeciesId);
        }

        private static RunState MakeRun(int partyCount, int boxCount)
        {
            var state = new RunState();
            for (int i = 0; i < partyCount; i++)
            {
                state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(100 + i, $"Party{i}", PokemonType.Normal), $"party-{i}"));
            }
            for (int i = 0; i < boxCount; i++)
            {
                state.Box.Add(PokemonInstanceFactory.Create(MakeSpecies(200 + i, $"Box{i}", PokemonType.Normal), $"box-{i}"));
            }
            return state;
        }

        [Test]
        public void MoveMon_OntoAnOccupiedSlot_TradesThePlacesOfTheTwoMons()
        {
            var state = MakeRun(partyCount: 3, boxCount: 0);

            Assert.IsTrue(state.MoveMon(RosterGroup.Party, 0, RosterGroup.Party, 2));

            Assert.AreEqual(102, state.LineUp[0].SpeciesId, "the third mon should now lead");
            Assert.AreEqual(100, state.LineUp[2].SpeciesId);
            Assert.AreEqual(3, state.LineUp.Count, "a trade should never change either count");
        }

        [Test]
        public void MoveMon_FromTheBoxOntoAnOccupiedPartySlot_TradesAcrossTheTwoCollections()
        {
            var state = MakeRun(partyCount: 2, boxCount: 1);

            Assert.IsTrue(state.MoveMon(RosterGroup.Box, 0, RosterGroup.Party, 0));

            Assert.AreEqual(200, state.LineUp[0].SpeciesId, "the Box mon should now lead");
            Assert.AreEqual(100, state.Box[0].SpeciesId, "the old Lead should be in the Box");
            Assert.AreEqual(2, state.LineUp.Count);
            Assert.AreEqual(1, state.Box.Count);
        }

        /// <summary>Slots fill from the front: the collections stay gap-free, so a drop onto any
        /// empty slot means "append", wherever in the row that slot happened to be.</summary>
        [Test]
        public void MoveMon_OntoAnEmptySlot_AppendsToThatCollection()
        {
            var state = MakeRun(partyCount: 3, boxCount: 0);

            Assert.IsTrue(state.MoveMon(RosterGroup.Party, 0, RosterGroup.Box, 4));

            Assert.AreEqual(2, state.LineUp.Count);
            Assert.AreEqual(101, state.LineUp[0].SpeciesId, "the mons behind the one that left should close up");
            Assert.AreEqual(1, state.Box.Count);
            Assert.AreEqual(100, state.Box[0].SpeciesId);
        }

        [Test]
        public void MoveMon_WithinTheParty_OntoAnEmptySlot_MovesItToTheBack()
        {
            var state = MakeRun(partyCount: 3, boxCount: 0);

            Assert.IsTrue(state.MoveMon(RosterGroup.Party, 0, RosterGroup.Party, 5));

            Assert.AreEqual(3, state.LineUp.Count);
            Assert.AreEqual(101, state.LineUp[0].SpeciesId);
            Assert.AreEqual(100, state.LineUp[2].SpeciesId);
        }

        /// <summary>The rule the Team screen leans on: a run always has someone to send out, so
        /// the last mon in the line-up can't be moved to the Box.</summary>
        [Test]
        public void MoveMon_MovingTheLastPartyMonToTheBox_IsRefused()
        {
            var state = MakeRun(partyCount: 1, boxCount: 0);

            Assert.IsFalse(state.MoveMon(RosterGroup.Party, 0, RosterGroup.Box, 0));

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.IsEmpty(state.Box);
        }

        /// <summary>Trading the last party mon for a Box mon is fine, though — the party still
        /// has one afterwards, which is the actual rule.</summary>
        [Test]
        public void MoveMon_TradingTheLastPartyMonForABoxMon_IsAllowed()
        {
            var state = MakeRun(partyCount: 1, boxCount: 1);

            Assert.IsTrue(state.MoveMon(RosterGroup.Party, 0, RosterGroup.Box, 0));

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(200, state.LineUp[0].SpeciesId);
            Assert.AreEqual(100, state.Box[0].SpeciesId);
        }

        [Test]
        public void MoveMon_ToAnEmptySlotPastThePartysCapacity_IsRefused()
        {
            var state = MakeRun(partyCount: RunState.MaxPartySize, boxCount: 1);

            Assert.IsFalse(state.MoveMon(RosterGroup.Box, 0, RosterGroup.Party, RunState.MaxPartySize));

            Assert.AreEqual(RunState.MaxPartySize, state.LineUp.Count);
            Assert.AreEqual(1, state.Box.Count);
        }

        [Test]
        public void MoveMon_OntoItsOwnSlot_ChangesNothing()
        {
            var state = MakeRun(partyCount: 2, boxCount: 0);

            Assert.IsFalse(state.MoveMon(RosterGroup.Party, 1, RosterGroup.Party, 1));

            Assert.AreEqual(100, state.LineUp[0].SpeciesId);
            Assert.AreEqual(101, state.LineUp[1].SpeciesId);
        }

        [Test]
        public void MoveMon_FromAnEmptySlot_ChangesNothing()
        {
            var state = MakeRun(partyCount: 1, boxCount: 0);

            Assert.IsFalse(state.MoveMon(RosterGroup.Box, 0, RosterGroup.Party, 1));

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.IsEmpty(state.Box);
        }

        [Test]
        public void ReleaseMon_TakesTheMonOutOfTheRunAndClosesTheGap()
        {
            var state = MakeRun(partyCount: 3, boxCount: 0);

            Assert.IsTrue(state.ReleaseMon(RosterGroup.Party, 1));

            Assert.AreEqual(2, state.LineUp.Count);
            Assert.AreEqual(100, state.LineUp[0].SpeciesId);
            Assert.AreEqual(102, state.LineUp[1].SpeciesId, "the mon behind the released one should close up");
            Assert.IsEmpty(state.Box, "a release is not a move to the Box");
        }

        [Test]
        public void ReleaseMon_FromTheBox_DoesNotTouchTheParty()
        {
            var state = MakeRun(partyCount: 1, boxCount: 2);

            Assert.IsTrue(state.ReleaseMon(RosterGroup.Box, 0));

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(1, state.Box.Count);
            Assert.AreEqual(201, state.Box[0].SpeciesId);
        }

        /// <summary>Same invariant the drags obey: a run always keeps someone to send out, even
        /// when there are mons sitting in the Box.</summary>
        [Test]
        public void ReleaseMon_TheLastPartyMon_IsRefused()
        {
            var state = MakeRun(partyCount: 1, boxCount: 2);

            Assert.IsFalse(state.CanReleaseMon(RosterGroup.Party, 0));
            Assert.IsFalse(state.ReleaseMon(RosterGroup.Party, 0));

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(2, state.Box.Count);
        }

        [Test]
        public void CanReleaseMon_ForAnEmptySlot_IsFalse()
        {
            var state = MakeRun(partyCount: 2, boxCount: 0);

            Assert.IsFalse(state.CanReleaseMon(RosterGroup.Party, 5));
            Assert.IsFalse(state.CanReleaseMon(RosterGroup.Box, 0));
            Assert.IsFalse(state.ReleaseMon(RosterGroup.Box, 0));
            Assert.AreEqual(2, state.LineUp.Count);
        }

        /// <summary>A run that's down to one mon still has a Team screen with a Swap button on it,
        /// so the no-second-slot case has to be a no-op rather than an index error.</summary>
        [Test]
        public void RunState_SwapLeadAndSupport_WithASingleMon_IsANoOp()
        {
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(MakeSpecies(1, "Alpha", PokemonType.Fire), "lead"));

            state.SwapLeadAndSupport();

            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(1, state.LineUp[0].SpeciesId);
        }

        // ---- encounters -----------------------------------------------------------------------

        [Test]
        public void EncounterGenerator_OnlyPicksFromTheBiasedTypes_WhenAnyExist()
        {
            var grass = MakeSpecies(1, "Grassy", PokemonType.Grass);
            var fire = MakeSpecies(2, "Firey", PokemonType.Fire);
            var library = MakeLibrary(grass, fire);

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, Grass, badges: 1, layer: 1, seed: 42, instanceIdPrefix: "wild");

            Assert.AreEqual(RunProgression.WildEncounterSize(1), lineUp.Count);
            Assert.IsTrue(lineUp.All(m => m.SpeciesId == grass.Id));
        }

        [Test]
        public void EncounterGenerator_IsDeterministic_ForTheSameSeed()
        {
            var library = MakeLibrary(
                MakeSpecies(1, "A", PokemonType.Grass),
                MakeSpecies(2, "B", PokemonType.Bug),
                MakeSpecies(3, "C", PokemonType.Flying));
            var bias = LocationCatalog.Get(LocationType.Forest).TypeBias;

            var a = EncounterGenerator.GenerateWildLineUp(library, bias, badges: 5, layer: 3, seed: 99, instanceIdPrefix: "wild");
            var b = EncounterGenerator.GenerateWildLineUp(library, bias, badges: 5, layer: 3, seed: 99, instanceIdPrefix: "wild");

            Assert.AreEqual(a.Select(m => m.SpeciesId), b.Select(m => m.SpeciesId));
        }

        [Test]
        public void EncounterGenerator_FallsBackToTheFilteredRoster_WhenNoSpeciesMatchTheBias()
        {
            var library = MakeLibrary(MakeSpecies(1, "Rocky", PokemonType.Rock));

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, new[] { PokemonType.Water }, badges: 1, layer: 1, seed: 1, instanceIdPrefix: "wild");

            Assert.IsNotEmpty(lineUp);
            Assert.IsTrue(lineUp.All(m => m.SpeciesId == 1));
        }

        /// <summary>Encounters are pitched by the run's progress: level from the badge count and the
        /// node's depth into the map, group size from the badge count.</summary>
        [Test]
        public void EncounterGenerator_BuildsAtTheWildLevel_AndItsGroupGrowsWithBadges()
        {
            var species = MakeSpecies(1, "Wild", PokemonType.Grass);
            var library = MakeLibrary(species);

            var early = EncounterGenerator.GenerateWildLineUp(library, Grass, badges: 0, layer: 1, seed: 3, instanceIdPrefix: "wild");
            var late = EncounterGenerator.GenerateWildLineUp(library, Grass, badges: 5, layer: 5, seed: 3, instanceIdPrefix: "wild");

            Assert.AreEqual(RunProgression.WildEncounterSize(0), early.Count);
            Assert.AreEqual(RunProgression.WildEncounterSize(5), late.Count);
            Assert.Greater(late.Count, early.Count);
            Assert.IsTrue(early.All(m => ExperienceResolver.LevelOf(m) == RunProgression.WildLevel(0, 1)));
            Assert.IsTrue(late.All(m => ExperienceResolver.LevelOf(m) == RunProgression.WildLevel(5, 5)));
            Assert.AreEqual(StatGrowth.AtLevel(species, RunProgression.WildLevel(5, 5)).Attack, late[0].CurrentStats.Attack);
        }

        /// <summary>The pool holds base forms only, but an encounter past a threshold is evolved — a
        /// wild mon follows exactly the rules the player's do.</summary>
        [Test]
        public void EncounterGenerator_EvolvesAWildMonPastItsThreshold()
        {
            var (basic, evolved, library) = MakeEvolutionLine();
            basic.Type1 = PokemonType.Grass;
            int badges = 3;
            Assert.GreaterOrEqual(RunProgression.WildLevel(badges, 1), ExperienceResolver.EvolutionLevels[0], "precondition");

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, Grass, badges, layer: 1, seed: 5, instanceIdPrefix: "wild");

            Assert.IsTrue(lineUp.All(m => m.SpeciesId == evolved.Id));
            Assert.IsTrue(lineUp.All(m => m.TimesEvolved == 1));
        }

        [Test]
        public void EncounterPool_ExcludesEvolvedForms_Legendaries_AndHighTotals_Early()
        {
            var basic = MakeSpecies(1, "Basic", PokemonType.Grass);
            var evolved = MakeSpecies(2, "Evolved", PokemonType.Grass);
            basic.EvolvesInto = evolved;
            var legendary = MakeSpecies(3, "Legend", PokemonType.Grass);
            legendary.IsLegendary = true;
            var bulky = MakeSpecies(4, "Bulky", PokemonType.Grass, attack: 100, health: 100, speed: 100);
            var library = MakeLibrary(basic, evolved, legendary, bulky);
            int finalBadges = RunProgression.BadgesToWin - 1;

            CollectionAssert.AreEquivalent(new[] { basic }, EncounterPool.For(library, Grass, badges: 0, isGym: false));
            CollectionAssert.AreEquivalent(new[] { basic, bulky }, EncounterPool.For(library, Grass, finalBadges, isGym: false),
                "the final Location lifts the stat cap, but a Legendary still never turns up in the wild");
            CollectionAssert.AreEquivalent(new[] { basic, legendary, bulky }, EncounterPool.For(library, Grass, finalBadges, isGym: true),
                "the final Gym Leader may field a Legendary");
        }

        // ---- Gyms -----------------------------------------------------------------------------

        [Test]
        public void GymTeamGenerator_FieldsAtLeastTheLineUp_AtTheGymLevel()
        {
            var library = MakeLibrary(MakeSpecies(1, "Leader", PokemonType.Rock));
            var rock = new[] { PokemonType.Rock };

            var team = GymTeamGenerator.Generate(library, rock, badges: 0, lineUpCount: 4, seed: 7);
            var late = GymTeamGenerator.Generate(library, rock, badges: 6, lineUpCount: 1, seed: 7);

            Assert.AreEqual(4, team.Count, "the Leader matches a longer line-up");
            Assert.IsTrue(team.All(m => ExperienceResolver.LevelOf(m) == RunProgression.GymLevel(0)));
            foreach (var mon in team)
            {
                StringAssert.StartsWith(GymTeamGenerator.InstanceIdPrefix, mon.InstanceId);
                Assert.AreEqual(mon.CurrentStats.Health, mon.CurrentHP, "a Gym member starts its fight at full health");
            }
            Assert.AreEqual(RunProgression.GymTeamSize(6, 1), late.Count);
            Assert.Greater(late.Count, 1, "the team grows with badges even against a one-mon line-up");
        }

        [Test]
        public void GymTeamGenerator_IsDeterministic_ForTheSameSeed()
        {
            var library = MakeLibrary(
                MakeSpecies(1, "A", PokemonType.Grass),
                MakeSpecies(2, "B", PokemonType.Rock),
                MakeSpecies(3, "C", PokemonType.Water));

            var a = GymTeamGenerator.Generate(library, Grass, badges: 2, lineUpCount: 3, seed: 123);
            var b = GymTeamGenerator.Generate(library, Grass, badges: 2, lineUpCount: 3, seed: 123);

            Assert.AreEqual(a.Select(m => m.SpeciesId), b.Select(m => m.SpeciesId));
        }

        // ---- EXP, levels, growth and evolution ------------------------------------------------

        /// <summary>A two-stage line built for the EXP tests: base evolves into evolved, and both
        /// are registered so ExperienceResolver can look either up by id.</summary>
        private static (PokemonSpeciesDefinitionAsset first, PokemonSpeciesDefinitionAsset second, PokemonSpeciesLibrary library)
            MakeEvolutionLine(int baseStat = 10, int evolvedStat = 40)
        {
            var second = MakeSpecies(2, "Evolved", PokemonType.Normal, attack: evolvedStat, health: evolvedStat, speed: evolvedStat);
            var first = MakeSpecies(1, "Basic", PokemonType.Normal, attack: baseStat, health: baseStat, speed: baseStat);
            first.EvolvesInto = second;
            second.EvolutionStage = 1;
            return (first, second, MakeLibrary(first, second));
        }

        private static (PokemonSpeciesDefinitionAsset first, PokemonSpeciesDefinitionAsset second,
            PokemonSpeciesDefinitionAsset third, PokemonSpeciesLibrary library) MakeThreeStageLine()
        {
            var third = MakeSpecies(3, "Final", PokemonType.Normal);
            var second = MakeSpecies(2, "Middle", PokemonType.Normal);
            var first = MakeSpecies(1, "Basic", PokemonType.Normal);
            first.EvolvesInto = second;
            second.EvolvesInto = third;
            return (first, second, third, MakeLibrary(first, second, third));
        }

        [Test]
        public void ExperienceResolver_LevelCurve_CostsMoreForEachLevel()
        {
            Assert.AreEqual(1, ExperienceResolver.LevelForExp(0));
            Assert.AreEqual(0, ExperienceResolver.ExpToReachLevel(1));

            for (int level = 1; level < 40; level++)
            {
                int cost = ExperienceResolver.ExpToReachLevel(level + 1) - ExperienceResolver.ExpToReachLevel(level);
                Assert.AreEqual(ExperienceResolver.ExpForLevelUp(level), cost, $"level {level}");
                Assert.Greater(ExperienceResolver.ExpForLevelUp(level + 1), cost, "each level should cost more than the last");
                Assert.AreEqual(level, ExperienceResolver.LevelForExp(ExperienceResolver.ExpToReachLevel(level + 1) - 1));
                Assert.AreEqual(level + 1, ExperienceResolver.LevelForExp(ExperienceResolver.ExpToReachLevel(level + 1)));
            }
        }

        /// <summary>Growth keeps a species' shape — each level adds a share of its own base stats —
        /// and Health is tripled so fights last more than a Step or two.</summary>
        [Test]
        public void StatGrowth_ScalesWithTheSpecies_AndMultipliesHealth()
        {
            var species = MakeSpecies(1, "Shape", PokemonType.Normal, attack: 50, health: 40, speed: 30);

            var atOne = StatGrowth.AtLevel(species, 1);
            Assert.AreEqual(50, atOne.Attack);
            Assert.AreEqual(40 * StatGrowth.HealthMultiplier, atOne.Health);
            Assert.AreEqual(30, atOne.Speed);

            // Ten levels at 10% and +2 a level: base x2, plus 20.
            var atEleven = StatGrowth.AtLevel(species, 11);
            Assert.AreEqual(120, atEleven.Attack);
            Assert.AreEqual((80 + 20) * StatGrowth.HealthMultiplier, atEleven.Health);
            Assert.AreEqual(80, atEleven.Speed);
        }

        [Test]
        public void ExperienceResolver_StatsComeFromStatGrowth_AtTheMonsLevel()
        {
            var (species, _, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpToReachLevel(5), library);

            Assert.AreEqual(5, ExperienceResolver.LevelOf(mon));
            var expected = StatGrowth.AtLevel(species, 5);
            Assert.AreEqual(expected.Attack, mon.CurrentStats.Attack);
            Assert.AreEqual(expected.Health, mon.CurrentStats.Health);
            Assert.AreEqual(expected.Speed, mon.CurrentStats.Speed);
            Assert.AreEqual(mon.CurrentStats.Health, mon.CurrentHP, "growing should leave the mon at full HP");
        }

        /// <summary>Stats are derived from species + EXP rather than accumulated, so the same total
        /// EXP has to produce the same stats however it was granted.</summary>
        [Test]
        public void ExperienceResolver_StatsDependOnTotalExpOnly_NotOnHowItArrived()
        {
            var (species, _, library) = MakeEvolutionLine();
            var atOnce = PokemonInstanceFactory.Create(species, "at-once");
            var piecemeal = PokemonInstanceFactory.Create(species, "piecemeal");

            ExperienceResolver.GrantExp(atOnce, 30, library);
            for (int i = 0; i < 30; i++)
            {
                ExperienceResolver.GrantExp(piecemeal, 1, library);
            }

            Assert.AreEqual(atOnce.SpeciesId, piecemeal.SpeciesId);
            Assert.AreEqual(atOnce.CurrentStats.Attack, piecemeal.CurrentStats.Attack);
            Assert.AreEqual(atOnce.CurrentStats.Health, piecemeal.CurrentStats.Health);
            Assert.AreEqual(atOnce.CurrentStats.Speed, piecemeal.CurrentStats.Speed);
        }

        [Test]
        public void ExperienceResolver_EvolvesAtTheFirstEvolutionLevel_AndReportsIt()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");
            int evolveAt = ExperienceResolver.EvolutionLevels[0];

            var below = ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpToReachLevel(evolveAt) - 1, library);
            CollectionAssert.IsEmpty(below.Evolutions, "nothing should evolve before the level");
            Assert.AreEqual(species.Id, mon.SpeciesId);

            var report = ExperienceResolver.GrantExp(mon, 1, library);

            Assert.AreEqual(1, report.Evolutions.Count);
            Assert.AreEqual(evolved.Id, mon.SpeciesId);
            Assert.AreEqual(1, mon.TimesEvolved);
            Assert.AreEqual(StatGrowth.AtLevel(evolved, evolveAt).Attack, mon.CurrentStats.Attack,
                "stats should rebase on the new species, not keep the old base");
            Assert.AreEqual(1, report.LevelUps.Count);
            Assert.AreEqual(evolveAt, report.LevelUps[0].ToLevel);
            StringAssert.Contains($"Basic Lv {evolveAt}", report.Describe());
            StringAssert.Contains("Basic evolved into Evolved!", report.Describe());
        }

        /// <summary>Each threshold is its own step: crossing the first one evolves once, and a mon
        /// needs to reach the second level to evolve again — unless one grant crosses both.</summary>
        [Test]
        public void ExperienceResolver_EvolvesOncePerThresholdCrossed()
        {
            var (first, second, third, library) = MakeThreeStageLine();

            var stepwise = PokemonInstanceFactory.Create(first, "stepwise");
            ExperienceResolver.GrantExp(stepwise, ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[0]), library);
            Assert.AreEqual(second.Id, stepwise.SpeciesId, "the first threshold is one step, not the whole chain");
            ExperienceResolver.GrantExp(stepwise, ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[1]) - stepwise.Exp, library);
            Assert.AreEqual(third.Id, stepwise.SpeciesId);
            Assert.AreEqual(2, stepwise.TimesEvolved);

            var atOnce = PokemonInstanceFactory.Create(first, "at-once");
            var report = ExperienceResolver.GrantExp(atOnce, ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[1]), library);
            Assert.AreEqual(2, report.Evolutions.Count);
            Assert.AreEqual(third.Id, atOnce.SpeciesId);
        }

        [Test]
        public void ExperienceResolver_LeavesAFinalFormAlone_HoweverMuchExpItEarns()
        {
            var species = MakeSpecies(1, "FinalForm", PokemonType.Normal);
            var library = MakeLibrary(species);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var report = ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpToReachLevel(30), library);

            CollectionAssert.IsEmpty(report.Evolutions);
            Assert.AreEqual(species.Id, mon.SpeciesId);
            Assert.AreEqual(0, mon.TimesEvolved);
            Assert.IsFalse(ExperienceResolver.CanEverEvolve(mon, library));
            Assert.IsNull(ExperienceResolver.NextEvolutionLevel(mon, library));
        }

        /// <summary>A curated base form whose real pre-evolution isn't in the roster (Pikachu, whose
        /// chain starts at Pichu) is stage 1 but has still evolved zero times — so it must evolve at
        /// the first threshold like anything else.</summary>
        [Test]
        public void ExperienceResolver_CountsThresholdsPerInstance_NotFromTheSpeciesChainDepth()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            species.EvolutionStage = 1;
            evolved.EvolutionStage = 2;
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[0]), library);

            Assert.AreEqual(evolved.Id, mon.SpeciesId);
        }

        /// <summary>A mon created at a level arrives evolved as far as that level allows — and an
        /// evolved species arrives with its earlier evolutions already counted, so a middle stage
        /// handed out past the first threshold doesn't evolve again on the spot.</summary>
        [Test]
        public void ExperienceResolver_CreateAtLevel_CountsEvolutionsAlreadyMade()
        {
            var (first, second, third, library) = MakeThreeStageLine();
            int between = ExperienceResolver.EvolutionLevels[0] + 1;

            var fromBase = ExperienceResolver.CreateAtLevel(first, "a", between, library);
            Assert.AreEqual(second.Id, fromBase.SpeciesId);
            Assert.AreEqual(between, ExperienceResolver.LevelOf(fromBase));

            var middle = ExperienceResolver.CreateAtLevel(second, "b", between, library);
            Assert.AreEqual(second.Id, middle.SpeciesId, "a middle stage has already used the first threshold");
            Assert.AreEqual(1, middle.TimesEvolved);
            Assert.AreEqual(ExperienceResolver.EvolutionLevels[1], ExperienceResolver.NextEvolutionLevel(middle, library));

            var late = ExperienceResolver.CreateAtLevel(first, "c", ExperienceResolver.EvolutionLevels[1], library);
            Assert.AreEqual(third.Id, late.SpeciesId);
        }

        /// <summary>The fairness rule: nobody the run owns sits more than CatchUpLevelGap below its
        /// strongest mon, and catching up never takes a level away from anyone.</summary>
        [Test]
        public void ExperienceResolver_ApplyCatchUp_RaisesStragglers_AndNeverLowersAnyone()
        {
            var species = MakeSpecies(1, "Mon", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var veteran = ExperienceResolver.CreateAtLevel(species, "veteran", 12, library);
            var rookie = PokemonInstanceFactory.Create(species, "rookie");
            var boxed = ExperienceResolver.CreateAtLevel(species, "boxed", 11, library);
            state.LineUp.Add(veteran);
            state.LineUp.Add(rookie);
            state.Box.Add(boxed);

            ExperienceResolver.ApplyCatchUp(state, library);

            Assert.AreEqual(12, ExperienceResolver.LevelOf(veteran));
            Assert.AreEqual(12 - ExperienceResolver.CatchUpLevelGap, ExperienceResolver.LevelOf(rookie));
            Assert.AreEqual(StatGrowth.AtLevel(species, 12 - ExperienceResolver.CatchUpLevelGap).Health, rookie.CurrentHP);
            Assert.AreEqual(11, ExperienceResolver.LevelOf(boxed), "a mon already above the floor is left alone");
        }

        // ---- Camp -----------------------------------------------------------------------------

        [Test]
        public void CampResolver_GrantsExpToTheWholeLineUp_AndSetsANextBattleBuff()
        {
            var species = MakeSpecies(1, "Camper", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState
            {
                LineUp = new List<PokemonInstance>
                {
                    PokemonInstanceFactory.Create(species, "lead"),
                    PokemonInstanceFactory.Create(species, "support")
                }
            };
            int expected = CampResolver.ExpFor(state);

            var report = CampResolver.Resolve(state, library);

            Assert.AreEqual(expected, report.ExpGranted);
            Assert.IsTrue(state.LineUp.All(m => m.Exp == expected));
            Assert.Greater(state.NextBattleAttackBonusPercent, 0f);
        }

        // ---- battle boundary ------------------------------------------------------------------

        /// <summary>The roster-to-battle boundary: a combatant starts from the run's stats and HP
        /// with every battle-only field at its default, and keeps a way back to the mon it came
        /// from.</summary>
        [Test]
        public void BattleCombatant_FromInstance_StartsCleanAndRemembersItsSource()
        {
            var species = MakeSpecies(1, "Fighter", PokemonType.Normal, health: 80);
            var persisted = PokemonInstanceFactory.Create(species, "mon-1");
            int expectedHealth = StatGrowth.AtLevel(species, 1).Health;

            var combatant = BattleCombatant.FromInstance(persisted);

            Assert.AreEqual(expectedHealth, combatant.CurrentHP);
            Assert.AreEqual(expectedHealth, combatant.CurrentStats.Health);
            Assert.AreEqual(0, combatant.Charge);
            Assert.AreEqual(0, combatant.Shield);
            Assert.AreEqual(0, combatant.DamageReductionFlat);
            Assert.AreEqual(0, combatant.PoisonStacks);
            Assert.AreEqual(1f, combatant.ChargeRateMultiplier);
            Assert.AreEqual(0f, combatant.LifestealPercent);
            Assert.IsNull(combatant.Status);
            Assert.AreEqual("mon-1", combatant.InstanceId);
            Assert.AreSame(persisted, combatant.Source);
        }

        /// <summary>The reason the split exists: simulating must not write back onto the roster.</summary>
        [Test]
        public void RunningABattle_LeavesTheRosterInstancesUntouched()
        {
            var species = MakeSpecies(1, "Brawler", PokemonType.Normal, attack: 20, health: 40, speed: 10);
            var a = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "a-1") };
            var b = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "b-1") };
            int fullHealth = a[0].CurrentHP;
            int attack = a[0].CurrentStats.Attack;

            var log = PrecomputedStepLogRunner.Run(a, b, seed: 99);

            Assert.IsTrue(log.Events.Any(e => e.Kind == StepEventKind.Damage),
                "the fight should actually have done something");
            Assert.AreEqual(fullHealth, a[0].CurrentHP, "the roster mon took damage");
            Assert.AreEqual(fullHealth, b[0].CurrentHP, "the roster mon took damage");
            Assert.AreEqual(attack, a[0].CurrentStats.Attack, "the roster mon's stats changed");
        }

        // ---- win rewards ----------------------------------------------------------------------

        [Test]
        public void BattleRewardResolver_PaysByTheFoesLevel_AndMoreForAGym()
        {
            var species = MakeSpecies(1, "Foe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var foes = new List<PokemonInstance>
            {
                ExperienceResolver.CreateAtLevel(species, "f0", 4, library),
                ExperienceResolver.CreateAtLevel(species, "f1", 6, library)
            };

            int wild = BattleRewardResolver.ExpForWin(foes, isGym: false);

            Assert.AreEqual(BattleRewardResolver.BaseExpPerWin + 5, wild, "the base plus the foes' average level");
            Assert.AreEqual(wild * BattleRewardResolver.GymExpMultiplier, BattleRewardResolver.ExpForWin(foes, isGym: true));
        }

        /// <summary>Every mon in the line-up is paid, not just whoever was left standing — a
        /// Reserve behind a Lead that never faints would otherwise never grow. The Box isn't paid;
        /// it only catches up.</summary>
        [Test]
        public void BattleRewardResolver_PaysEveryMonInTheLineUp_ButNotTheBox()
        {
            var species = MakeSpecies(1, "Winner", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "lead"));
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "reserve"));
            state.Box.Add(PokemonInstanceFactory.Create(species, "boxed"));
            var foes = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "foe") };
            int expected = BattleRewardResolver.ExpForWin(foes, isGym: false);

            var report = BattleRewardResolver.GrantWinRewards(state, foes, isGym: false, library);

            Assert.AreEqual(expected, report.ExpGranted);
            Assert.IsTrue(state.LineUp.All(m => m.Exp == expected));
            Assert.AreEqual(0, state.Box[0].Exp, "a mon sitting in the Box didn't fight");
        }

        [Test]
        public void BattleRewardResolver_ReportsAnEvolutionTheWinSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var mon = PokemonInstanceFactory.Create(species, "lead");
            mon.Exp = ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[0]) - 1;
            state.LineUp.Add(mon);
            var foes = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "foe") };

            var report = BattleRewardResolver.GrantWinRewards(state, foes, isGym: false, library);

            Assert.AreEqual(1, report.Evolutions.Count);
            Assert.AreEqual(evolved.Id, report.Evolutions[0].Mon.SpeciesId);
        }

        // ---- combining ------------------------------------------------------------------------

        /// <summary>A combine is worth exactly one level, from anywhere inside the survivor's current
        /// level — a flat EXP amount would be worth a lot at level 3 and nothing at level 23.</summary>
        [Test]
        public void CombineResolver_ConsumesTheDuplicate_AndRaisesTheSurvivorOneLevel()
        {
            var species = MakeSpecies(1, "Dupe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var keeper = PokemonInstanceFactory.Create(species, "keeper");
            keeper.Exp = ExperienceResolver.ExpToReachLevel(4) + 3;
            state.LineUp.Add(keeper);
            state.Box.Add(PokemonInstanceFactory.Create(species, "spare"));

            CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.IsEmpty(state.Box, "the duplicate should be consumed");
            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(5, ExperienceResolver.LevelOf(state.LineUp[0]));
            Assert.AreEqual("keeper", state.LineUp[0].InstanceId, "the mon dropped onto is the one that survives");
        }

        [Test]
        public void CombineResolver_RefusesTwoDifferentSpecies()
        {
            var alpha = MakeSpecies(1, "Alpha", PokemonType.Normal);
            var beta = MakeSpecies(2, "Beta", PokemonType.Water);
            var library = MakeLibrary(alpha, beta);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(alpha, "alpha"));
            state.Box.Add(PokemonInstanceFactory.Create(beta, "beta"));

            var eligibility = CombineResolver.CanCombine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);
            CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.IsFalse(eligibility.Allowed);
            StringAssert.Contains("aren't the same Pokemon", eligibility.Reason);
            Assert.AreEqual(1, state.Box.Count, "nothing should have been consumed");
            Assert.AreEqual(0, state.LineUp[0].Exp);
        }

        /// <summary>The same rule releasing obeys: a run always keeps someone to send out, so the
        /// last party mon can't be fed to a Box duplicate.</summary>
        [Test]
        public void CombineResolver_RefusesToEmptyTheParty()
        {
            var species = MakeSpecies(1, "Dupe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "only"));
            state.Box.Add(PokemonInstanceFactory.Create(species, "spare"));

            var eligibility = CombineResolver.CanCombine(state, RosterGroup.Party, 0, RosterGroup.Box, 0, library);
            CombineResolver.Combine(state, RosterGroup.Party, 0, RosterGroup.Box, 0, library);

            Assert.IsFalse(eligibility.Allowed);
            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(0, state.Box[0].Exp);
        }

        [Test]
        public void CombineResolver_ReportsAnEvolutionTheLevelSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var keeper = PokemonInstanceFactory.Create(species, "keeper");
            keeper.Exp = ExperienceResolver.ExpToReachLevel(ExperienceResolver.EvolutionLevels[0] - 1);
            state.LineUp.Add(keeper);
            state.Box.Add(PokemonInstanceFactory.Create(species, "spare"));

            var report = CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.AreEqual(1, report.Evolutions.Count);
            Assert.AreEqual(evolved.Id, state.LineUp[0].SpeciesId);
        }

        // ---- catching -------------------------------------------------------------------------

        /// <summary>The Battle screen accumulates a fight's events Step by Step rather than holding a
        /// StepLog, so the same question — what fell on the wild side — has to be answerable from a
        /// plain event list.</summary>
        [Test]
        public void CatchResolver_ReadsDefeatedMonsFromABareEventList_Too()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var wildLineUp = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "wild-0") };
            var events = new List<StepEvent>
            {
                new StepEvent { Step = 1, Kind = StepEventKind.Damage, SourceSide = Side.A, TargetInstanceId = "wild-0" },
                new StepEvent { Step = 1, Kind = StepEventKind.Faint, SourceSide = Side.B, SourceInstanceId = "wild-0" }
            };

            var defeated = CatchResolver.GetDefeated(wildLineUp, events, Side.B);

            Assert.AreEqual(1, defeated.Count);
            Assert.AreEqual("wild-0", defeated[0].InstanceId);
        }

        /// <summary>Only what fainted is offered, and a catch joins the Box usable: at the run's
        /// catch-up level when the wild mon was below it.</summary>
        [Test]
        public void CatchResolver_OnlyOffersDefeatedMons_AndAddsThemAtTheCatchUpLevel()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtLevel(species, "veteran", 12, library));

            var wildLineUp = new List<PokemonInstance>
            {
                ExperienceResolver.CreateAtLevel(species, "wild-0", 3, library),
                ExperienceResolver.CreateAtLevel(species, "wild-1", 3, library)
            };

            // Which mons are catchable comes from the fight's Faint events, not from testing the
            // line-up's HP — a battle runs on copies, so these instances are never damaged.
            var log = new StepLog();
            log.Events.Add(new StepEvent
            {
                Step = 1,
                Kind = StepEventKind.Faint,
                SourceSide = Side.B,
                SourceInstanceId = "wild-0"
            });

            var defeated = CatchResolver.GetDefeated(wildLineUp, log, Side.B);
            Assert.AreEqual(1, defeated.Count);
            Assert.AreEqual("wild-0", defeated[0].InstanceId);

            CatchResolver.Catch(state, defeated[0], library);

            Assert.AreEqual(1, state.Box.Count);
            int expectedLevel = 12 - ExperienceResolver.CatchUpLevelGap;
            Assert.AreEqual(expectedLevel, ExperienceResolver.LevelOf(state.Box[0]));
            Assert.AreEqual(StatGrowth.AtLevel(species, expectedLevel).Health, state.Box[0].CurrentHP);
        }

        [Test]
        public void CatchResolver_KeepsAWildMonsOwnLevel_WhenItIsHigher()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "rookie"));
            var wild = ExperienceResolver.CreateAtLevel(species, "wild-0", 6, library);

            CatchResolver.Catch(state, wild, library);

            Assert.AreEqual(6, ExperienceResolver.LevelOf(state.Box[0]));
        }
    }
}
