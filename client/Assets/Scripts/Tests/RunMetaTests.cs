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

        /// <summary>A synthetic species. The stats are deliberately *not* a valid tier line — these
        /// exist to make arithmetic in a test legible (a Health of 50 survives long enough to assert
        /// on), and only the handful of tests that care about tiering pass a real one.</summary>
        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name, PokemonType type1,
            int attack = 10, int health = 50, int speed = 10, int tier = SpeciesTier.MinTier,
            int healthGrowthPercent = 100)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = type1;
            species.Tier = tier;
            species.BaseAttack = attack;
            species.BaseHealth = health;
            species.BaseSpeed = speed;
            // 100 by default so a test that only cares *that* a mon grew can assert on Health without
            // having to know which way the species' own draw fell (ADR 0009). Tests about the draw
            // itself pass their own value.
            species.HealthGrowthPercent = healthGrowthPercent;
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

        /// <summary>Encounters are pitched by the run's progress: EXP from the badge count and the
        /// node's depth into the map, group size from the badge count.</summary>
        [Test]
        public void EncounterGenerator_BuildsAtTheWildExp_AndItsGroupGrowsWithBadges()
        {
            var species = MakeSpecies(1, "Wild", PokemonType.Grass);
            var library = MakeLibrary(species);

            var early = EncounterGenerator.GenerateWildLineUp(library, Grass, badges: 0, layer: 1, seed: 3, instanceIdPrefix: "wild");
            var late = EncounterGenerator.GenerateWildLineUp(library, Grass, badges: 5, layer: 5, seed: 3, instanceIdPrefix: "wild");

            Assert.AreEqual(RunProgression.WildEncounterSize(0), early.Count);
            Assert.AreEqual(RunProgression.WildEncounterSize(5), late.Count);
            Assert.Greater(late.Count, early.Count);
            Assert.IsTrue(early.All(m => m.Exp == RunProgression.WildExp(0, 1)));
            Assert.IsTrue(late.All(m => m.Exp == RunProgression.WildExp(5, 5)));
            Assert.AreEqual(StatGrowth.AtExp(species, late[0].InstanceId, RunProgression.WildExp(5, 5), 0).Attack,
                late[0].CurrentStats.Attack);
        }

        /// <summary>The pool holds base forms only, but an encounter past a threshold is evolved — a
        /// wild mon follows exactly the rules the player's do.</summary>
        [Test]
        public void EncounterGenerator_EvolvesAWildMonPastItsThreshold()
        {
            var (basic, evolved, library) = MakeEvolutionLine();
            basic.Type1 = PokemonType.Grass;
            int badges = 4;
            Assert.GreaterOrEqual(RunProgression.WildExp(badges, 1), ExperienceResolver.ExpPerEvolution, "precondition");

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, Grass, badges, layer: 1, seed: 5, instanceIdPrefix: "wild");

            Assert.IsTrue(lineUp.All(m => m.SpeciesId == evolved.Id));
            Assert.IsTrue(lineUp.All(m => m.TimesEvolved == 1));
        }

        [Test]
        public void EncounterPool_ExcludesEvolvedForms_Legendaries_AndHighTiers_Early()
        {
            var basic = MakeSpecies(1, "Basic", PokemonType.Grass);
            var evolved = MakeSpecies(2, "Evolved", PokemonType.Grass);
            basic.EvolvesInto = evolved;
            var legendary = MakeSpecies(3, "Legend", PokemonType.Grass);
            legendary.IsLegendary = true;
            var bulky = MakeSpecies(4, "Bulky", PokemonType.Grass, tier: SpeciesTier.MaxTier);
            var library = MakeLibrary(basic, evolved, legendary, bulky);
            int finalBadges = RunProgression.BadgesToWin - 1;

            CollectionAssert.AreEquivalent(new[] { basic }, EncounterPool.For(library, Grass, badges: 0, isGym: false));
            CollectionAssert.AreEquivalent(new[] { basic, bulky }, EncounterPool.For(library, Grass, finalBadges, isGym: false),
                "the final Location lifts the tier cap, but a Legendary still never turns up in the wild");
            CollectionAssert.AreEquivalent(new[] { basic, legendary, bulky }, EncounterPool.For(library, Grass, finalBadges, isGym: true),
                "the final Gym Leader may field a Legendary");
        }

        // ---- Gyms -----------------------------------------------------------------------------

        [Test]
        public void GymTeamGenerator_FieldsAtLeastTheLineUp_AtTheGymExp()
        {
            var library = MakeLibrary(MakeSpecies(1, "Leader", PokemonType.Rock));
            var rock = new[] { PokemonType.Rock };

            var team = GymTeamGenerator.Generate(library, rock, badges: 0, lineUpCount: 4, seed: 7);
            var late = GymTeamGenerator.Generate(library, rock, badges: 6, lineUpCount: 1, seed: 7);

            Assert.AreEqual(4, team.Count, "the Leader matches a longer line-up");
            Assert.IsTrue(team.All(m => m.Exp == RunProgression.GymExp(0)));
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

        /// <summary>Lifetime EXP and the EXP the current species has grown on are different numbers
        /// once a mon evolves: the total keeps climbing, and each evolution charges
        /// ExpPerEvolution against it.</summary>
        [Test]
        public void ExperienceResolver_ExpSinceEvolution_ChargesEachEvolutionAgainstTheLifetimeTotal()
        {
            int perEvolution = ExperienceResolver.ExpPerEvolution;
            var mon = new PokemonInstance { Exp = perEvolution + 3, TimesEvolved = 0 };
            Assert.AreEqual(perEvolution + 3, ExperienceResolver.ExpSinceEvolution(mon));
            Assert.AreEqual(perEvolution, ExperienceResolver.ExpAtNextEvolution(mon));

            mon.TimesEvolved = 1;
            Assert.AreEqual(3, ExperienceResolver.ExpSinceEvolution(mon));
            Assert.AreEqual(2 * perEvolution, ExperienceResolver.ExpAtNextEvolution(mon));

            // A mon can't owe EXP: a species handed out below the total its own evolutions cost
            // reads as freshly evolved rather than as a negative.
            mon.TimesEvolved = 3;
            Assert.AreEqual(0, ExperienceResolver.ExpSinceEvolution(mon));
        }

        /// <summary>The whole growth model: a point of EXP is +1 Attack *or* +1 Health, never both,
        /// and Speed never moves at all.</summary>
        [Test]
        public void StatGrowth_AddsOneStatPerExp_AndNeverTouchesSpeed()
        {
            var allHealth = MakeSpecies(1, "Wall", PokemonType.Normal, attack: 3, health: 5, speed: 2,
                tier: 2, healthGrowthPercent: 100);
            var allAttack = MakeSpecies(2, "Cannon", PokemonType.Normal, attack: 3, health: 5, speed: 2,
                tier: 2, healthGrowthPercent: 0);

            var fresh = StatGrowth.AtExp(allHealth, "mon", 0, 0);
            Assert.AreEqual(3, fresh.Attack);
            Assert.AreEqual(5, fresh.Health);
            Assert.AreEqual(2, fresh.Speed);

            var wall = StatGrowth.AtExp(allHealth, "mon", 4, 0);
            Assert.AreEqual(3, wall.Attack, "a 100% Health grower never gains Attack");
            Assert.AreEqual(9, wall.Health);

            var cannon = StatGrowth.AtExp(allAttack, "mon", 4, 0);
            Assert.AreEqual(7, cannon.Attack);
            Assert.AreEqual(5, cannon.Health, "a 0% Health grower never gains Health");

            Assert.AreEqual(2, wall.Speed);
            Assert.AreEqual(2, cannon.Speed, "Speed doesn't move for EXP or for evolution");
        }

        /// <summary>Each point buys exactly one stat, whichever way the draw fell.</summary>
        [Test]
        public void StatGrowth_SpendsEveryPointExactlyOnce_AtAnyGrowthValue()
        {
            foreach (int percent in new[] { 0, 50, 72, 86, 100 })
            {
                var species = MakeSpecies(1, "Mon", PokemonType.Normal, attack: 3, health: 5, speed: 1,
                    healthGrowthPercent: percent);
                for (int exp = 0; exp <= 30; exp++)
                {
                    var stats = StatGrowth.AtExp(species, "mon-1", exp, 0);
                    Assert.AreEqual(3 + 5 + exp, stats.Attack + stats.Health,
                        $"{percent}% at {exp} EXP spent more or less than one point per EXP");
                }
            }
        }

        /// <summary>The draw is a chance, not a ratio: it follows the species' growth value over many
        /// points, and it is keyed to the mon, so two of the same species don't grow the same way.</summary>
        [Test]
        public void StatGrowth_DrawsHealthAboutAsOftenAsTheGrowthValueSays_ButPerMon()
        {
            var species = MakeSpecies(1, "Mon", PokemonType.Normal, healthGrowthPercent: 70);

            int health = StatGrowth.HealthGainsIn(species.HealthGrowthPercent, "sample", 1000);
            Assert.That(health, Is.InRange(630, 770), "a 70% grower drew Health {0} times in 1000", health);

            // Asserted point by point rather than on the totals: two mons can land on the same *count*
            // of Health by coincidence, and a test that failed when they did would be flaky.
            bool diverges = false;
            for (int i = 0; i < 12 && !diverges; i++)
            {
                diverges = StatGrowth.GainsHealth(70, "twin-a", i) != StatGrowth.GainsHealth(70, "twin-b", i);
            }
            Assert.IsTrue(diverges, "two mons of the same species drew identically — the draw isn't per mon");

            // And it isn't only these two: a spread of mons should not all reach the same stat line.
            var lines = Enumerable.Range(0, 10)
                .Select(i => StatGrowth.AtExp(species, $"mon-{i}", 12, 0).Health)
                .Distinct()
                .ToList();
            Assert.Greater(lines.Count, 1, "ten mons of the same species all grew into the same Health");
        }

        /// <summary>Stats have to be rebuildable from scratch on every grant, so the draw must be a
        /// pure function of the mon and which point it is — the same question asked twice, or out of
        /// order, gives the same answer.</summary>
        [Test]
        public void StatGrowth_IsDeterministic_ForTheSameMonAndPoint()
        {
            var species = MakeSpecies(1, "Mon", PokemonType.Normal, healthGrowthPercent: 60);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(StatGrowth.GainsHealth(60, "mon-1", i), StatGrowth.GainsHealth(60, "mon-1", i));
            }
            Assert.AreEqual(StatGrowth.AtExp(species, "mon-1", 9, 1).Health,
                StatGrowth.AtExp(species, "mon-1", 9, 1).Health);
        }

        /// <summary>An evolution is a flat bonus and the species evolved into contributes nothing —
        /// growth keeps running off the base form for the mon's whole life.</summary>
        [Test]
        public void StatGrowth_AddsAFlatBonusPerEvolution()
        {
            var species = MakeSpecies(1, "Base", PokemonType.Normal, attack: 3, health: 5, speed: 1,
                healthGrowthPercent: 100);

            var once = StatGrowth.AtExp(species, "mon", 12, 1);
            Assert.AreEqual(3 + StatGrowth.AttackPerEvolution, once.Attack);
            Assert.AreEqual(5 + 12 + StatGrowth.HealthPerEvolution, once.Health);

            var twice = StatGrowth.AtExp(species, "mon", 24, 2);
            Assert.AreEqual(3 + 2 * StatGrowth.AttackPerEvolution, twice.Attack);
            Assert.AreEqual(5 + 24 + 2 * StatGrowth.HealthPerEvolution, twice.Health);
        }

        [Test]
        public void ExperienceResolver_StatsComeFromStatGrowth_AtTheMonsExp()
        {
            var (species, _, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution - 1, library);

            Assert.AreEqual(ExperienceResolver.ExpPerEvolution - 1, mon.Exp);
            var expected = StatGrowth.AtExp(species, "mon-1", ExperienceResolver.ExpPerEvolution - 1, 0);
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
            // The same instance id on both: which stat a point buys is drawn from the mon's identity
            // (ADR 0009), so two *different* mons are supposed to diverge. What this test is about is
            // that one mon's stats don't depend on how its EXP arrived.
            var atOnce = PokemonInstanceFactory.Create(species, "mon-1");
            var piecemeal = PokemonInstanceFactory.Create(species, "mon-1");

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
        public void ExperienceResolver_EvolvesAtFiveExp_AndReportsIt()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var below = ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution - 1, library);
            CollectionAssert.IsEmpty(below.Evolutions, "nothing should evolve before the fifth point");
            Assert.AreEqual(species.Id, mon.SpeciesId);

            var report = ExperienceResolver.GrantExp(mon, 1, library);

            Assert.AreEqual(1, report.Evolutions.Count);
            Assert.AreEqual(evolved.Id, mon.SpeciesId);
            Assert.AreEqual(1, mon.TimesEvolved);
            Assert.AreEqual(StatGrowth.AtExp(species, "mon-1", ExperienceResolver.ExpPerEvolution, 1).Attack,
                mon.CurrentStats.Attack,
                "an evolution is a flat bonus on top of the base form's growth — the new species' own " +
                "stats contribute nothing");
            Assert.AreEqual(1, report.Gains.Count);
            StringAssert.Contains("Basic evolved into Evolved!", report.Describe());
        }

        /// <summary>Each evolution costs its own five points: crossing the first threshold evolves
        /// once, and the mon has to earn five more to evolve again — unless one grant covers both.</summary>
        [Test]
        public void ExperienceResolver_EvolvesOncePerThresholdCrossed()
        {
            var (first, second, third, library) = MakeThreeStageLine();
            int perEvolution = ExperienceResolver.ExpPerEvolution;

            var stepwise = PokemonInstanceFactory.Create(first, "stepwise");
            ExperienceResolver.GrantExp(stepwise, perEvolution, library);
            Assert.AreEqual(second.Id, stepwise.SpeciesId, "the first threshold is one step, not the whole chain");
            ExperienceResolver.GrantExp(stepwise, perEvolution, library);
            Assert.AreEqual(third.Id, stepwise.SpeciesId);
            Assert.AreEqual(2, stepwise.TimesEvolved);

            var atOnce = PokemonInstanceFactory.Create(first, "at-once");
            var report = ExperienceResolver.GrantExp(atOnce, 2 * perEvolution, library);
            Assert.AreEqual(2, report.Evolutions.Count);
            Assert.AreEqual(third.Id, atOnce.SpeciesId);
        }

        [Test]
        public void ExperienceResolver_LeavesAFinalFormAlone_HoweverMuchExpItEarns()
        {
            var species = MakeSpecies(1, "FinalForm", PokemonType.Normal);
            var library = MakeLibrary(species);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var report = ExperienceResolver.GrantExp(mon, 30, library);

            CollectionAssert.IsEmpty(report.Evolutions);
            Assert.AreEqual(species.Id, mon.SpeciesId);
            Assert.AreEqual(0, mon.TimesEvolved);
            Assert.IsFalse(ExperienceResolver.CanEverEvolve(mon, library));
            Assert.IsNull(ExperienceResolver.ExpToNextEvolution(mon, library));
            Assert.AreEqual(StatGrowth.AtExp(species, "mon-1", 30, 0).Health, mon.CurrentStats.Health,
                "a final form keeps growing; EXP never caps");
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

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution, library);

            Assert.AreEqual(evolved.Id, mon.SpeciesId);
        }

        /// <summary>A mon created at an amount of EXP arrives evolved as far as that EXP allows —
        /// and an evolved species arrives with its earlier evolutions already paid for, so a middle
        /// stage handed out cheap doesn't turn out to be a base form that evolves on the spot.</summary>
        [Test]
        public void ExperienceResolver_CreateAtExp_CountsEvolutionsAlreadyMade()
        {
            var (first, second, third, library) = MakeThreeStageLine();
            int perEvolution = ExperienceResolver.ExpPerEvolution;
            int between = perEvolution + 1;

            var fromBase = ExperienceResolver.CreateAtExp(first, "a", between, library);
            Assert.AreEqual(second.Id, fromBase.SpeciesId);
            Assert.AreEqual(between, fromBase.Exp);
            Assert.AreEqual(1, ExperienceResolver.ExpSinceEvolution(fromBase));

            var middle = ExperienceResolver.CreateAtExp(second, "b", between, library);
            Assert.AreEqual(second.Id, middle.SpeciesId, "a middle stage has already paid for the first threshold");
            Assert.AreEqual(1, middle.TimesEvolved);
            Assert.AreEqual(perEvolution - 1, ExperienceResolver.ExpToNextEvolution(middle, library));

            var cheap = ExperienceResolver.CreateAtExp(second, "c", 0, library);
            Assert.AreEqual(second.Id, cheap.SpeciesId, "asking for a Charmeleon at 0 EXP can't produce a Charmander");
            Assert.AreEqual(perEvolution, cheap.Exp, "it costs what getting there costs");
            Assert.AreEqual(StatGrowth.AtExp(first, "c", perEvolution, 1).Health, cheap.CurrentStats.Health,
                "a mon asked for as a middle stage is still built from the base form it grew out of");

            var late = ExperienceResolver.CreateAtExp(first, "d", 2 * perEvolution, library);
            Assert.AreEqual(third.Id, late.SpeciesId);
        }

        /// <summary>The fairness rule, and its limit: nobody *in the party* sits more than
        /// CatchUpExpGap below the run's most-experienced mon, catching up never takes EXP away from
        /// anyone, and the Box is not part of it — a mon that didn't fight doesn't grow.</summary>
        [Test]
        public void ExperienceResolver_ApplyCatchUp_RaisesPartyStragglers_LeavesTheBoxAlone()
        {
            var species = MakeSpecies(1, "Mon", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var veteran = ExperienceResolver.CreateAtExp(species, "veteran", 12, library);
            var rookie = PokemonInstanceFactory.Create(species, "rookie");
            var boxed = ExperienceResolver.CreateAtExp(species, "boxed", 11, library);
            var boxedStraggler = PokemonInstanceFactory.Create(species, "boxed-straggler");
            state.LineUp.Add(veteran);
            state.LineUp.Add(rookie);
            state.Box.Add(boxed);
            state.Box.Add(boxedStraggler);

            ExperienceResolver.ApplyCatchUp(state, library);

            int floor = 12 - ExperienceResolver.CatchUpExpGap;
            Assert.AreEqual(12, veteran.Exp);
            Assert.AreEqual(floor, rookie.Exp);
            Assert.AreEqual(StatGrowth.AtExp(species, "rookie", floor, 0).Health, rookie.CurrentHP);
            Assert.AreEqual(11, boxed.Exp, "a mon already above the floor is left alone");
            Assert.AreEqual(0, boxedStraggler.Exp,
                "the Box is not caught up — only the party earns, however far behind storage falls");

            // ...and the moment it is fielded, the next grant brings it back within the gap, so
            // leaving a mon in the Box costs nothing permanent.
            state.Box.Remove(boxedStraggler);
            state.LineUp.Add(boxedStraggler);
            ExperienceResolver.ApplyCatchUp(state, library);
            Assert.AreEqual(floor, boxedStraggler.Exp);
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
            int expectedHealth = StatGrowth.BaseOf(species).Health;

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

        /// <summary>Every fight pays the same single point, so "five wins is an evolution" is true
        /// however the five were earned. What rises across a run is the opposition, not the payout.</summary>
        [Test]
        public void BattleRewardResolver_PaysOnePoint_ForEveryKindOfFight()
        {
            var species = MakeSpecies(1, "Foe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var weak = new List<PokemonInstance> { ExperienceResolver.CreateAtExp(species, "f0", 0, library) };
            var strong = new List<PokemonInstance>
            {
                ExperienceResolver.CreateAtExp(species, "f1", 30, library),
                ExperienceResolver.CreateAtExp(species, "f2", 40, library)
            };

            Assert.AreEqual(1, BattleRewardResolver.ExpPerWin);
            Assert.AreEqual(1, BattleRewardResolver.ExpForWin(weak, isGym: false));
            Assert.AreEqual(1, BattleRewardResolver.ExpForWin(strong, isGym: false));
            Assert.AreEqual(1, BattleRewardResolver.ExpForWin(strong, isGym: true), "a Gym pays the same");
        }

        /// <summary>Every mon in the line-up is paid, not just whoever was left standing — a
        /// Reserve behind a Lead that never faints would otherwise never grow — and nothing outside
        /// the line-up is paid at all, catch-up included. The Box wasn't at the fight.</summary>
        [Test]
        public void BattleRewardResolver_PaysEveryMonInTheLineUp_AndNothingInTheBox()
        {
            var species = MakeSpecies(1, "Winner", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var lead = ExperienceResolver.CreateAtExp(species, "lead", 6, library);
            var reserve = ExperienceResolver.CreateAtExp(species, "reserve", 6, library);
            // Far enough behind that the old whole-run catch-up would have dragged it up with the
            // party — which is exactly the free growth this test exists to rule out.
            var boxed = PokemonInstanceFactory.Create(species, "boxed");
            state.LineUp.Add(lead);
            state.LineUp.Add(reserve);
            state.Box.Add(boxed);
            var foes = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "foe") };
            int expected = BattleRewardResolver.ExpForWin(foes, isGym: false);

            var report = BattleRewardResolver.GrantWinRewards(state, foes, isGym: false, library);

            Assert.AreEqual(expected, report.ExpGranted);
            Assert.IsTrue(state.LineUp.All(m => m.Exp == 6 + expected));
            Assert.AreEqual(0, boxed.Exp, "a mon sitting in the Box didn't fight, so it didn't grow");
            Assert.IsFalse(report.Gains.Any(g => g.Mon == boxed),
                "and the result screen has nothing to say about it");
        }

        /// <summary>What the result panel reads off a win: one line per mon naming the stat its
        /// point of EXP actually bought, since that's the half of a win the player doesn't already
        /// know (ADR 0009 — a point buys Attack *or* Health by a draw they don't control).</summary>
        [Test]
        public void GrowthReport_GainLines_NameTheStatEachMonGained()
        {
            var species = MakeSpecies(1, "Winner", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var lead = PokemonInstanceFactory.Create(species, "lead");
            state.LineUp.Add(lead);
            var before = lead.CurrentStats;
            var foes = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "foe") };

            var lines = BattleRewardResolver.GrantWinRewards(state, foes, isGym: false, library).GainLines();

            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains(species.DisplayName, lines[0]);
            StringAssert.Contains(GrowthReport.Line(before), lines[0], "the line it grew from");
            StringAssert.Contains(GrowthReport.Line(lead.CurrentStats), lines[0], "and the line it grew to");
            StringAssert.Contains(GrowthReport.Delta(before, lead.CurrentStats), lines[0]);
            // A point is one stat or the other, never both, and never Speed.
            Assert.That(GrowthReport.Delta(before, lead.CurrentStats),
                Is.EqualTo("+1 Attack").Or.EqualTo("+1 Health"));
        }

        /// <summary>An evolution shows up in the same list, after the stat lines, so a win that set
        /// one off reads in the order it happened.</summary>
        [Test]
        public void GrowthReport_GainLines_EndWithAnyEvolution()
        {
            var (species, _, library) = MakeEvolutionLine();
            var report = new GrowthReport();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");
            mon.Exp = ExperienceResolver.ExpPerEvolution - 1;
            report.Merge(ExperienceResolver.GrantExp(mon, 1, library));

            var lines = report.GainLines();

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains("→", lines[0]);
            StringAssert.Contains("evolved into", lines[1]);
        }

        [Test]
        public void BattleRewardResolver_ReportsAnEvolutionTheWinSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var mon = PokemonInstanceFactory.Create(species, "lead");
            mon.Exp = ExperienceResolver.ExpPerEvolution - 1;
            state.LineUp.Add(mon);
            var foes = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "foe") };

            var report = BattleRewardResolver.GrantWinRewards(state, foes, isGym: false, library);

            Assert.AreEqual(1, report.Evolutions.Count);
            Assert.AreEqual(evolved.Id, report.Evolutions[0].Mon.SpeciesId);
        }

        // ---- combining ------------------------------------------------------------------------

        /// <summary>A combine is worth exactly what a win is — one point — and the duplicate's own
        /// EXP is not carried over: it's a dupe sink, not a way to launder a second mon's growth.</summary>
        [Test]
        public void CombineResolver_ConsumesTheDuplicate_AndPaysTheSurvivorOnePoint()
        {
            var species = MakeSpecies(1, "Dupe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            var keeper = PokemonInstanceFactory.Create(species, "keeper");
            keeper.Exp = 3;
            state.LineUp.Add(keeper);
            var spare = PokemonInstanceFactory.Create(species, "spare");
            spare.Exp = 2;
            state.Box.Add(spare);

            CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.IsEmpty(state.Box, "the duplicate should be consumed");
            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(4, state.LineUp[0].Exp, "one point, not the duplicate's two on top");
            Assert.AreEqual(StatGrowth.AtExp(species, "keeper", 4, 0).Attack, state.LineUp[0].CurrentStats.Attack);
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
        public void CombineResolver_ReportsAnEvolutionThePointSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var keeper = PokemonInstanceFactory.Create(species, "keeper");
            keeper.Exp = ExperienceResolver.ExpPerEvolution - 1;
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
        /// catch-up EXP when the wild mon was below it.</summary>
        [Test]
        public void CatchResolver_OnlyOffersDefeatedMons_AndAddsThemAtTheCatchUpExp()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(ExperienceResolver.CreateAtExp(species, "veteran", 12, library));

            var wildLineUp = new List<PokemonInstance>
            {
                ExperienceResolver.CreateAtExp(species, "wild-0", 3, library),
                ExperienceResolver.CreateAtExp(species, "wild-1", 3, library)
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
            int expectedExp = 12 - ExperienceResolver.CatchUpExpGap;
            Assert.AreEqual(expectedExp, state.Box[0].Exp);
            Assert.AreEqual(StatGrowth.AtExp(species, state.Box[0].InstanceId, expectedExp, 0).Health,
                state.Box[0].CurrentHP);
        }

        [Test]
        public void CatchResolver_KeepsAWildMonsOwnExp_WhenItIsHigher()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "rookie"));
            var wild = ExperienceResolver.CreateAtExp(species, "wild-0", 6, library);

            CatchResolver.Catch(state, wild, library);

            Assert.AreEqual(6, state.Box[0].Exp);
        }
    }
}
