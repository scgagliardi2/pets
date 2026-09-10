using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>
    /// Runs every JSON fixture under /shared/fixtures through BattleSimulator and checks outcome,
    /// faint order, and final survivor stats — the behavioral contract a future server-side
    /// reimplementation (Phase 4) must also satisfy, per PLAN.md §3 and shared/README.md.
    ///
    /// Fixture JSON shape:
    /// {
    ///   "seed": 1,
    ///   "teamA": [ { "instanceId": "a1", "attack": 3, "health": 10, "abilities": [
    ///       { "trigger": "OnFaint", "effects": [
    ///           { "type": "DealDamage", "target": "RandomEnemy", "amount": 1 } ] } ] } ],
    ///   "teamB": [ ... ],
    ///   "expected": {
    ///     "outcome": "TeamAWins",
    ///     "faintOrder": ["b1"],
    ///     "survivors": [ { "instanceId": "a1", "health": 2, "attack": 3 } ]
    ///   }
    /// }
    /// </summary>
    public class GoldenFixtureTests
    {
        private static string FixturesDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "shared", "fixtures"));

        private static IEnumerable<string> FixturePaths()
        {
            return Directory.Exists(FixturesDirectory)
                ? Directory.GetFiles(FixturesDirectory, "*.json").OrderBy(p => p)
                : Enumerable.Empty<string>();
        }

        [Test]
        public void AtLeastOneFixtureExists()
        {
            Assert.IsTrue(FixturePaths().Any(), $"No fixtures found under {FixturesDirectory}");
        }

        [TestCaseSource(nameof(FixturePaths))]
        public void Fixture_MatchesExpectedOutcome(string path)
        {
            var fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            var name = Path.GetFileName(path);

            var teamA = ToTeamState(fixture.teamA);
            var teamB = ToTeamState(fixture.teamB);

            var log = BattleSimulator.Run(teamA, teamB, fixture.seed);

            Assert.AreEqual(fixture.expected.outcome, log.Outcome.ToString(), $"outcome mismatch in {name}");

            var actualFaintOrder = log.Events
                .Where(e => e.Kind == BattleEventKind.Faint)
                .Select(e => e.SourceInstanceId)
                .ToList();
            CollectionAssert.AreEqual(fixture.expected.faintOrder, actualFaintOrder, $"faint order mismatch in {name}");

            var allCreatures = teamA.Slots.Concat(teamB.Slots).ToList();
            foreach (var survivor in fixture.expected.survivors)
            {
                var actual = allCreatures.FirstOrDefault(c => c.InstanceId == survivor.instanceId);
                Assert.IsNotNull(actual, $"expected survivor {survivor.instanceId} not found in {name}");
                Assert.AreEqual(survivor.health, actual.Health, $"survivor {survivor.instanceId} health mismatch in {name}");
                Assert.AreEqual(survivor.attack, actual.Attack, $"survivor {survivor.instanceId} attack mismatch in {name}");
            }
        }

        private static TeamState ToTeamState(List<FixtureCreature> creatures)
        {
            var team = new TeamState();
            foreach (var c in creatures)
            {
                team.Slots.Add(new CreatureState
                {
                    InstanceId = c.instanceId,
                    TemplateId = c.instanceId,
                    DisplayName = c.instanceId,
                    Attack = c.attack,
                    Health = c.health,
                    MaxHealth = c.health,
                    Level = 1,
                    Abilities = c.abilities.Select(ToAbilityData).ToList(),
                });
            }
            return team;
        }

        private static AbilityData ToAbilityData(FixtureAbility a)
        {
            return new AbilityData
            {
                Trigger = (TriggerType)Enum.Parse(typeof(TriggerType), a.trigger),
                Effects = a.effects.Select(ToEffectData).ToList(),
            };
        }

        private static EffectData ToEffectData(FixtureEffect e)
        {
            return new EffectData
            {
                Type = (EffectType)Enum.Parse(typeof(EffectType), e.type),
                Target = string.IsNullOrEmpty(e.target) ? default : (TargetSelector)Enum.Parse(typeof(TargetSelector), e.target),
                Amount = e.amount,
                SummonTemplate = e.summonTemplate == null || string.IsNullOrEmpty(e.summonTemplate.id)
                    ? null
                    : new CreatureTemplate
                    {
                        Id = e.summonTemplate.id,
                        DisplayName = e.summonTemplate.displayName,
                        Attack = e.summonTemplate.attack,
                        Health = e.summonTemplate.health,
                    },
            };
        }

        [Serializable]
        private class FixtureFile
        {
            public int seed;
            public List<FixtureCreature> teamA = new List<FixtureCreature>();
            public List<FixtureCreature> teamB = new List<FixtureCreature>();
            public FixtureExpected expected = new FixtureExpected();
        }

        [Serializable]
        private class FixtureCreature
        {
            public string instanceId;
            public int attack;
            public int health;
            public List<FixtureAbility> abilities = new List<FixtureAbility>();
        }

        [Serializable]
        private class FixtureAbility
        {
            public string trigger;
            public List<FixtureEffect> effects = new List<FixtureEffect>();
        }

        [Serializable]
        private class FixtureEffect
        {
            public string type;
            public string target;
            public int amount;
            public FixtureSummonTemplate summonTemplate;
        }

        [Serializable]
        private class FixtureSummonTemplate
        {
            public string id;
            public string displayName;
            public int attack;
            public int health;
        }

        [Serializable]
        private class FixtureExpected
        {
            public string outcome;
            public List<string> faintOrder = new List<string>();
            public List<FixtureSurvivor> survivors = new List<FixtureSurvivor>();
        }

        [Serializable]
        private class FixtureSurvivor
        {
            public string instanceId;
            public int health;
            public int attack;
        }
    }
}
