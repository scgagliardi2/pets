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
        public void RunState_AdvanceToNextNode_MarksClearedAndMovesForwardUntilTheEnd()
        {
            var state = new RunState { Nodes = ForestLocationFactory.BuildNodes() };

            Assert.AreEqual("forest-1", state.CurrentNode.Id);
            Assert.IsFalse(state.CurrentNode.Cleared);

            for (int i = 0; i < state.Nodes.Count - 1; i++)
            {
                Assert.IsTrue(state.HasNextNode);
                state.AdvanceToNextNode();
            }

            Assert.AreEqual("forest-5", state.CurrentNode.Id);
            Assert.IsFalse(state.HasNextNode);

            // Advancing past the last node clears it but doesn't move the index out of range.
            state.AdvanceToNextNode();
            Assert.AreEqual("forest-5", state.CurrentNode.Id);
            Assert.IsTrue(state.CurrentNode.Cleared);
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

        [Test]
        public void ExperienceResolver_LevelsUp_AndGrowsStats_WhenExpThresholdIsCrossed()
        {
            var species = MakeSpecies(1, "Grower", PokemonType.Normal, attack: 10, health: 100, speed: 10);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");
            int startingAttack = mon.CurrentStats.Attack;
            int startingThreshold = mon.ExpToNextLevel;

            ExperienceResolver.GrantExp(mon, startingThreshold);

            Assert.AreEqual(2, mon.Level);
            Assert.Greater(mon.CurrentStats.Attack, startingAttack);
            Assert.AreEqual(0, mon.Exp);
            Assert.Greater(mon.ExpToNextLevel, startingThreshold);
        }

        [Test]
        public void ExperienceResolver_CanCrossMultipleLevelsFromOneGrant()
        {
            var species = MakeSpecies(1, "Grower", PokemonType.Normal);
            var mon = PokemonInstanceFactory.Create(species, "mon-1");

            ExperienceResolver.GrantExp(mon, mon.ExpToNextLevel * 5);

            Assert.Greater(mon.Level, 2);
        }

        [Test]
        public void CampResolver_GrantsExpToTheWholeLineUp_AndSetsANextBattleBuff()
        {
            var species = MakeSpecies(1, "Camper", PokemonType.Normal);
            var state = new RunState
            {
                LineUp = new List<PokemonInstance>
                {
                    PokemonInstanceFactory.Create(species, "lead"),
                    PokemonInstanceFactory.Create(species, "support")
                }
            };

            CampResolver.Resolve(state);

            Assert.IsTrue(state.LineUp.All(m => m.Exp > 0));
            Assert.Greater(state.NextBattleAttackBonusPercent, 0f);
        }

        [Test]
        public void PokemonInstanceFactory_ResetForBattle_RestoresFullHealthAndClearsTransientState()
        {
            var species = MakeSpecies(1, "Fighter", PokemonType.Normal, health: 80);
            var persisted = PokemonInstanceFactory.Create(species, "mon-1");
            persisted.CurrentHP = 1;
            persisted.Charge = 75;
            persisted.Status = StatusType.Poisoned;
            persisted.Level = 3;

            var fresh = PokemonInstanceFactory.ResetForBattle(persisted);

            Assert.AreEqual(80, fresh.CurrentHP);
            Assert.AreEqual(0, fresh.Charge);
            Assert.IsNull(fresh.Status);
            Assert.AreEqual(3, fresh.Level);
            Assert.AreEqual(persisted.InstanceId, fresh.InstanceId);
        }

        [Test]
        public void CatchResolver_OnlyOffersDefeatedMons_AndAddsAFreshCopyToTheBox()
        {
            var species = MakeSpecies(1, "Catchable", PokemonType.Bug);
            var library = MakeLibrary(species);
            var state = new RunState();

            var fainted = PokemonInstanceFactory.Create(species, "wild-0");
            fainted.CurrentHP = 0;
            var survivor = PokemonInstanceFactory.Create(species, "wild-1");
            var snapshot = new List<PokemonInstance> { fainted, survivor };

            var defeated = CatchResolver.GetDefeated(snapshot);
            Assert.AreEqual(1, defeated.Count);
            Assert.AreEqual("wild-0", defeated[0].InstanceId);

            CatchResolver.Catch(state, defeated[0], library);

            Assert.AreEqual(1, state.Box.Count);
            Assert.AreEqual(species.BaseHealth, state.Box[0].CurrentHP);
        }
    }
}
