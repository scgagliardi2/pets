using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>The line-ups the Battle screen fights with (Meta/RandomBattle): a random enemy
    /// team sized to the party, both sides with abilities switched off, and the run's own roster
    /// left alone.</summary>
    public class RandomBattleTests
    {
        private PokemonSpeciesLibrary library;
        private PassiveDefinitionAsset passive;

        [SetUp]
        public void SetUp()
        {
            passive = ScriptableObject.CreateInstance<PassiveDefinitionAsset>();
            passive.Id = "test-burst";
            passive.Effects.Add(new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemyLead, Amount = 5 });

            library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            for (int i = 1; i <= 6; i++)
            {
                library.AllSpecies.Add(Species(i, $"Mon{i}"));
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var species in library.AllSpecies)
            {
                UnityEngine.Object.DestroyImmediate(species);
            }
            UnityEngine.Object.DestroyImmediate(library);
            UnityEngine.Object.DestroyImmediate(passive);
        }

        private PokemonSpeciesDefinitionAsset Species(int id, string name)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.BaseAttack = 10;
            species.BaseHealth = 40;
            species.BaseSpeed = 100;
            species.Passive = passive;
            return species;
        }

        private List<PokemonInstance> Party(int count) =>
            Enumerable.Range(0, count)
                .Select(i => PokemonInstanceFactory.Create(library.AllSpecies[i], $"player-{i}"))
                .ToList();

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void EnemyTeam_IsTheSameSizeAsTheParty(int partySize)
        {
            var battle = RandomBattle.Create(Party(partySize), library, seed: 42);

            Assert.AreEqual(partySize, battle.PlayerLineUp.Count);
            Assert.AreEqual(partySize, battle.EnemyLineUp.Count);
        }

        [Test]
        public void EnemyTeam_IsDrawnFromTheLibrary_WithDistinctEnemyIds()
        {
            var battle = RandomBattle.Create(Party(6), library, seed: 7);

            var librarySpeciesIds = library.AllSpecies.Select(s => s.Id).ToList();
            foreach (var enemy in battle.EnemyLineUp)
            {
                CollectionAssert.Contains(librarySpeciesIds, enemy.Source.SpeciesId);
                StringAssert.StartsWith(RandomBattle.EnemyInstanceIdPrefix, enemy.InstanceId);
            }
            CollectionAssert.AllItemsAreUnique(battle.EnemyLineUp.Select(e => e.InstanceId));
        }

        [Test]
        public void SameSeed_RollsTheSameEnemyTeam()
        {
            var first = RandomBattle.GenerateEnemyLineUp(library, 6, seed: 1234).Select(m => m.SpeciesId);
            var second = RandomBattle.GenerateEnemyLineUp(library, 6, seed: 1234).Select(m => m.SpeciesId);

            CollectionAssert.AreEqual(first.ToList(), second.ToList());
        }

        /// <summary>"Random every time" in practice: across a spread of seeds, not every roll comes
        /// out the same.</summary>
        [Test]
        public void DifferentSeeds_RollDifferentEnemyTeams()
        {
            var rolls = Enumerable.Range(1, 20)
                .Select(seed => string.Join(",", RandomBattle.GenerateEnemyLineUp(library, 3, seed).Select(m => m.SpeciesId)))
                .Distinct()
                .Count();

            Assert.Greater(rolls, 1);
        }

        /// <summary>Abilities are off for now: every species here has a passive and a Speed that
        /// fills the meter every Step, and neither side's combatants carry it.</summary>
        [Test]
        public void BothSides_FightWithoutPassives()
        {
            var party = Party(2);
            Assert.IsNotNull(party[0].ResolvedPassive, "precondition: the run's mons do have a passive");

            var battle = RandomBattle.Create(party, library, seed: 3);

            Assert.IsTrue(battle.PlayerLineUp.All(c => c.ResolvedPassive == null));
            Assert.IsTrue(battle.EnemyLineUp.All(c => c.ResolvedPassive == null));
            Assert.IsNotNull(party[0].ResolvedPassive, "stripping is done on the battle's copies, not the roster");

            // A full fight never deals passive damage: every hit is exactly one Lead's attack.
            var log = PrecomputedStepLogRunner.Run(battle.PlayerLineUp, battle.EnemyLineUp, battle.Seed);
            var damage = log.Events.Where(e => e.Kind == StepEventKind.Damage).ToList();
            Assert.IsNotEmpty(damage);
            Assert.IsTrue(damage.All(e => e.Amount == 10), "only the 10-attack exchange should deal damage");
        }

        [Test]
        public void FightingTheBattle_LeavesTheRunsRosterUntouched()
        {
            var party = Party(3);
            var battle = RandomBattle.Create(party, library, seed: 99);
            // Read before fighting: the runner removes fainted combatants from these lists.
            CollectionAssert.AreEqual(party, battle.PlayerLineUp.Select(c => c.Source).ToList(),
                "each player combatant should point back at its own run mon, in line-up order");

            PrecomputedStepLogRunner.Run(battle.PlayerLineUp, battle.EnemyLineUp, battle.Seed);

            Assert.IsTrue(party.All(m => m.CurrentHP == 40), "damage belongs to the combatants, not the run");
            Assert.AreEqual(3, party.Count, "fainting in battle must not remove mons from the run");
        }

        [Test]
        public void AnEmptyParty_CannotStartABattle()
        {
            Assert.Throws<ArgumentException>(() => RandomBattle.Create(new List<PokemonInstance>(), library, seed: 1));
        }

        [Test]
        public void AnEmptyLibrary_CannotRollAnEnemyTeam()
        {
            var empty = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            try
            {
                Assert.Throws<ArgumentException>(() => RandomBattle.Create(Party(1), empty, seed: 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }
        }
    }
}
