using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>EditMode coverage for the Phase 0 Location/meta layer (PLAN.md §6, Phase 0):
    /// node-map progression, encounter generation, Camp EXP/buff resolution, and the stubbed
    /// catch flow. Builds ScriptableObject content in-memory via CreateInstance rather than
    /// loading Assets/Content, so these tests don't depend on the curated roster's exact contents.</summary>
    public class RunMetaTests
    {
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

        [Test]
        public void EncounterGenerator_OnlyPicksFromTheBiasedTypes_WhenAnyExist()
        {
            var grass = MakeSpecies(1, "Grassy", PokemonType.Grass);
            var fire = MakeSpecies(2, "Firey", PokemonType.Fire);
            var library = MakeLibrary(grass, fire);

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, new[] { PokemonType.Grass }, seed: 42, instanceIdPrefix: "wild");

            Assert.AreEqual(2, lineUp.Count);
            Assert.IsTrue(lineUp.All(m => m.SpeciesId == grass.Id));
        }

        [Test]
        public void EncounterGenerator_IsDeterministic_ForTheSameSeed()
        {
            var library = MakeLibrary(
                MakeSpecies(1, "A", PokemonType.Grass),
                MakeSpecies(2, "B", PokemonType.Bug),
                MakeSpecies(3, "C", PokemonType.Flying));

            var a = EncounterGenerator.GenerateWildLineUp(library, ForestLocationFactory.TypeBias, seed: 99, instanceIdPrefix: "wild");
            var b = EncounterGenerator.GenerateWildLineUp(library, ForestLocationFactory.TypeBias, seed: 99, instanceIdPrefix: "wild");

            Assert.AreEqual(a.Select(m => m.SpeciesId), b.Select(m => m.SpeciesId));
        }

        [Test]
        public void EncounterGenerator_FallsBackToFullRoster_WhenNoSpeciesMatchTheBias()
        {
            var library = MakeLibrary(MakeSpecies(1, "Rocky", PokemonType.Rock));

            var lineUp = EncounterGenerator.GenerateWildLineUp(library, new[] { PokemonType.Water }, seed: 1, instanceIdPrefix: "wild");

            Assert.AreEqual(2, lineUp.Count);
            Assert.IsTrue(lineUp.All(m => m.SpeciesId == 1));
        }

        /// <summary>A two-stage line built for the EXP tests: base evolves into evolved, and both
        /// are registered so ExperienceResolver can look either up by id.</summary>
        private static (PokemonSpeciesDefinitionAsset first, PokemonSpeciesDefinitionAsset second, PokemonSpeciesLibrary library)
            MakeEvolutionLine(int baseAttack = 10, int evolvedAttack = 40)
        {
            var second = MakeSpecies(2, "Evolved", PokemonType.Normal, attack: evolvedAttack, health: evolvedAttack, speed: evolvedAttack);
            var first = MakeSpecies(1, "Basic", PokemonType.Normal, attack: baseAttack, health: baseAttack, speed: baseAttack);
            first.EvolvesInto = second;
            second.EvolutionStage = 1;
            return (first, second, MakeLibrary(first, second));
        }

        [Test]
        public void ExperienceResolver_AddsAFlatStatGainPerExp_ToAllThreeStats()
        {
            var (species, _, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, 2, library);

            int gain = 2 * ExperienceResolver.StatGainPerExp;
            Assert.AreEqual(2, mon.Exp);
            Assert.AreEqual(species.BaseAttack + gain, mon.CurrentStats.Attack);
            Assert.AreEqual(species.BaseHealth + gain, mon.CurrentStats.Health);
            Assert.AreEqual(species.BaseSpeed + gain, mon.CurrentStats.Speed);
            Assert.AreEqual(mon.CurrentStats.Health, mon.CurrentHP, "growing should leave the mon at full HP");
        }

        /// <summary>Stats are derived from species + EXP rather than accumulated, so the same total
        /// EXP has to produce the same stats however it was granted. The previous percentage-based
        /// model failed exactly this.</summary>
        [Test]
        public void ExperienceResolver_StatsDependOnTotalExpOnly_NotOnHowItArrived()
        {
            var (species, _, library) = MakeEvolutionLine();
            var atOnce = PokemonInstanceFactory.Create(species, "at-once");
            var piecemeal = PokemonInstanceFactory.Create(species, "piecemeal");

            ExperienceResolver.GrantExp(atOnce, 2, library);
            ExperienceResolver.GrantExp(piecemeal, 1, library);
            ExperienceResolver.GrantExp(piecemeal, 1, library);

            Assert.AreEqual(atOnce.CurrentStats.Attack, piecemeal.CurrentStats.Attack);
            Assert.AreEqual(atOnce.CurrentStats.Health, piecemeal.CurrentStats.Health);
            Assert.AreEqual(atOnce.CurrentStats.Speed, piecemeal.CurrentStats.Speed);
        }

        [Test]
        public void ExperienceResolver_EvolvesAtTheThreshold_AndRebasesStatsOnTheNewSpecies()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var below = ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution - 1, library);
            CollectionAssert.IsEmpty(below, "nothing should evolve before the threshold");
            Assert.AreEqual(species.Id, mon.SpeciesId);

            var evolutions = ExperienceResolver.GrantExp(mon, 1, library);

            Assert.AreEqual(1, evolutions.Count);
            Assert.AreEqual("Basic", evolutions[0].FromName);
            Assert.AreEqual("Evolved", evolutions[0].ToName);
            Assert.AreEqual(evolved.Id, mon.SpeciesId);
            Assert.AreEqual(1, mon.TimesEvolved);
            Assert.AreEqual(evolved.BaseAttack + ExperienceResolver.ExpPerEvolution * ExperienceResolver.StatGainPerExp,
                mon.CurrentStats.Attack, "stats should rebase on the new species, not keep the old base");
        }

        /// <summary>The threshold is per evolution, not a flat total — otherwise reaching 3 EXP
        /// would run a mon up its whole chain in one go and the middle stage would never exist.</summary>
        [Test]
        public void ExperienceResolver_DoesNotChainStraightThroughTheNextEvolution()
        {
            var second = MakeSpecies(2, "Middle", PokemonType.Normal);
            var third = MakeSpecies(3, "Final", PokemonType.Normal);
            var first = MakeSpecies(1, "Basic", PokemonType.Normal);
            first.EvolvesInto = second;
            second.EvolvesInto = third;
            var library = MakeLibrary(first, second, third);
            var mon = PokemonInstanceFactory.Create(first, "mon-1");

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution, library);
            Assert.AreEqual(second.Id, mon.SpeciesId, "the first threshold is one step, not the whole chain");

            ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution, library);
            Assert.AreEqual(third.Id, mon.SpeciesId, "the second threshold is another step further");
            Assert.AreEqual(2, mon.TimesEvolved);
        }

        [Test]
        public void ExperienceResolver_LeavesAFinalFormAlone_HoweverMuchExpItEarns()
        {
            var species = MakeSpecies(1, "FinalForm", PokemonType.Normal);
            var library = MakeLibrary(species);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            var evolutions = ExperienceResolver.GrantExp(mon, ExperienceResolver.ExpPerEvolution * 4, library);

            CollectionAssert.IsEmpty(evolutions);
            Assert.AreEqual(species.Id, mon.SpeciesId);
            Assert.AreEqual(0, mon.TimesEvolved);
            Assert.IsFalse(ExperienceResolver.CanEverEvolve(mon, library));
        }

        /// <summary>A curated base form whose real pre-evolution isn't in the roster (Pikachu, whose
        /// chain starts at Pichu) is stage 1 but has still evolved zero times — so it must reach its
        /// first evolution on the first threshold like anything else.</summary>
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

            CampResolver.Resolve(state, library);

            Assert.IsTrue(state.LineUp.All(m => m.Exp == CampResolver.ExpGranted));
            Assert.Greater(state.NextBattleAttackBonusPercent, 0f);
        }

        /// <summary>The roster-to-battle boundary: a combatant starts from the run's stats and HP
        /// with every battle-only field at its default, and keeps a way back to the mon it came
        /// from. This replaces a test of PokemonInstanceFactory.ResetForBattle, which existed to
        /// work around the simulator mutating roster objects and is unnecessary now that a battle
        /// runs on copies.</summary>
        [Test]
        public void BattleCombatant_FromInstance_StartsCleanAndRemembersItsSource()
        {
            var species = MakeSpecies(1, "Fighter", PokemonType.Normal, health: 80);
            var persisted = PokemonInstanceFactory.Create(species, "mon-1");

            var combatant = BattleCombatant.FromInstance(persisted);

            Assert.AreEqual(80, combatant.CurrentHP);
            Assert.AreEqual(80, combatant.CurrentStats.Health);
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

        /// <summary>The reason the split exists: simulating must not write back onto the roster.
        /// Before it, a fight left shields, buffs and damage on the player's own mons.</summary>
        [Test]
        public void RunningABattle_LeavesTheRosterInstancesUntouched()
        {
            var species = MakeSpecies(1, "Brawler", PokemonType.Normal, attack: 20, health: 40, speed: 10);
            var a = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "a-1") };
            var b = new List<PokemonInstance> { PokemonInstanceFactory.Create(species, "b-1") };

            var log = PrecomputedStepLogRunner.Run(a, b, seed: 99);

            Assert.IsTrue(log.Events.Any(e => e.Kind == StepEventKind.Damage),
                "the fight should actually have done something");
            Assert.AreEqual(species.BaseHealth, a[0].CurrentHP, "the roster mon took damage");
            Assert.AreEqual(species.BaseHealth, b[0].CurrentHP, "the roster mon took damage");
            Assert.AreEqual(species.BaseAttack, a[0].CurrentStats.Attack, "the roster mon's stats changed");
        }

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

        /// <summary>A Gym Leader's team is drawn from the whole roster rather than the Location's
        /// type bias, and hits harder to take down than the wildlife on the way to it.</summary>
        [Test]
        public void GymTeamGenerator_DrawsFromTheWholeRoster_AndBuffsEveryMembersHealth()
        {
            var grass = MakeSpecies(1, "Leafy", PokemonType.Grass, health: 100);
            var rock = MakeSpecies(2, "Rocky", PokemonType.Rock, health: 100);
            var library = MakeLibrary(grass, rock);

            var team = GymTeamGenerator.Generate(library, count: 2, seed: 7);

            Assert.AreEqual(2, team.Count);
            foreach (var mon in team)
            {
                int authored = mon.SpeciesId == grass.Id ? grass.BaseHealth : rock.BaseHealth;
                int expected = authored + (int)(authored * GymTeamGenerator.HealthBonusPercent);
                Assert.AreEqual(expected, mon.CurrentStats.Health);
                Assert.AreEqual(expected, mon.CurrentHP, "a Gym member starts its fight at full health");
                StringAssert.StartsWith(GymTeamGenerator.InstanceIdPrefix, mon.InstanceId);
            }
        }

        [Test]
        public void GymTeamGenerator_IsDeterministic_ForTheSameSeed()
        {
            var library = MakeLibrary(
                MakeSpecies(1, "A", PokemonType.Grass),
                MakeSpecies(2, "B", PokemonType.Rock),
                MakeSpecies(3, "C", PokemonType.Water));

            var a = GymTeamGenerator.Generate(library, count: 3, seed: 123);
            var b = GymTeamGenerator.Generate(library, count: 3, seed: 123);

            Assert.AreEqual(a.Select(m => m.SpeciesId), b.Select(m => m.SpeciesId));
        }

        /// <summary>Every mon in the line-up is paid, not just whoever was left standing — a
        /// Reserve behind a Lead that never faints would otherwise never grow. The Box is not:
        /// EXP is for the team that fought.</summary>
        [Test]
        public void BattleRewardResolver_PaysEveryMonInTheLineUp_ButNotTheBox()
        {
            var species = MakeSpecies(1, "Winner", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "lead"));
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "reserve"));
            state.Box.Add(PokemonInstanceFactory.Create(species, "boxed"));

            BattleRewardResolver.GrantWinRewards(state, library);

            Assert.IsTrue(state.LineUp.All(m => m.Exp == BattleRewardResolver.ExpPerWin));
            Assert.AreEqual(0, state.Box[0].Exp, "a mon sitting in the Box didn't fight");
        }

        [Test]
        public void BattleRewardResolver_ReportsAnEvolutionTheWinSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var mon = PokemonInstanceFactory.Create(species, "lead");
            mon.Exp = ExperienceResolver.ExpPerEvolution - BattleRewardResolver.ExpPerWin;
            state.LineUp.Add(mon);

            var evolutions = BattleRewardResolver.GrantWinRewards(state, library);

            Assert.AreEqual(1, evolutions.Count);
            Assert.AreEqual(evolved.Id, evolutions[0].Mon.SpeciesId);
        }

        [Test]
        public void CombineResolver_ConsumesTheDuplicate_AndPaysTheSurvivor()
        {
            var species = MakeSpecies(1, "Dupe", PokemonType.Normal);
            var library = MakeLibrary(species);
            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "keeper"));
            state.Box.Add(PokemonInstanceFactory.Create(species, "spare"));

            CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.IsEmpty(state.Box, "the duplicate should be consumed");
            Assert.AreEqual(1, state.LineUp.Count);
            Assert.AreEqual(CombineResolver.ExpGranted, state.LineUp[0].Exp);
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

        /// <summary>Two combines is six EXP, which is two evolutions' worth — combining is the
        /// fastest route to one, so it has to report them the same way a win does.</summary>
        [Test]
        public void CombineResolver_ReportsAnEvolutionTheGrantSetOff()
        {
            var (species, evolved, library) = MakeEvolutionLine();
            var state = new RunState();
            var keeper = PokemonInstanceFactory.Create(species, "keeper");
            keeper.Exp = ExperienceResolver.ExpPerEvolution - CombineResolver.ExpGranted;
            state.LineUp.Add(keeper);
            state.Box.Add(PokemonInstanceFactory.Create(species, "spare"));

            var evolutions = CombineResolver.Combine(state, RosterGroup.Box, 0, RosterGroup.Party, 0, library);

            Assert.AreEqual(1, evolutions.Count);
            Assert.AreEqual(evolved.Id, state.LineUp[0].SpeciesId);
        }

        [Test]
        public void CatchResolver_OnlyOffersDefeatedMons_AndAddsAFreshCopyToTheBox()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();

            var wildLineUp = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(species, "wild-0"),
                PokemonInstanceFactory.Create(species, "wild-1")
            };

            // Which mons are catchable comes from the fight's Faint events now, not from testing
            // the line-up's HP — a battle runs on copies, so these instances are never damaged.
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
            Assert.AreEqual(species.BaseHealth, state.Box[0].CurrentHP);
        }
    }
}
