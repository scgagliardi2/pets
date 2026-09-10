using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Simulation;
using UnityEditor;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>
    /// Content-linting checks that don't require running a battle — catches authoring mistakes
    /// (duplicate ids, dangling summon references, a creature slipped into a bot round before its
    /// tier unlocks, JSON export drifting from the real fields) directly against the real
    /// ScriptableObject assets under Assets/Content. See docs/testing-harness-plan.md §3.
    /// </summary>
    public class ContentValidationTests
    {
        private static List<CreatureDefinition> AllCreatures()
        {
            var guids = AssetDatabase.FindAssets("t:CreatureDefinition", new[] { "Assets/Content/Creatures" });
            return guids.Select(g => AssetDatabase.LoadAssetAtPath<CreatureDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        }

        private static List<BotTeamDefinition> AllBotTeams()
        {
            var guids = AssetDatabase.FindAssets("t:BotTeamDefinition", new[] { "Assets/Content/BotRoster" });
            return guids.Select(g => AssetDatabase.LoadAssetAtPath<BotTeamDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        }

        private static ShopConfig LoadConfig()
        {
            return AssetDatabase.LoadAssetAtPath<ShopConfig>("Assets/Content/ShopConfig.asset");
        }

        [Test]
        public void EveryCreature_HasANonEmptyId()
        {
            var creatures = AllCreatures();
            Assume.That(creatures.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            foreach (var creature in creatures)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(creature.Id), $"{creature.name} has an empty Id");
            }
        }

        [Test]
        public void CreatureIds_AreUnique()
        {
            var creatures = AllCreatures();
            Assume.That(creatures.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            var duplicates = creatures.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate creature ids: {string.Join(", ", duplicates)}");
        }

        [Test]
        public void SummonEffects_AlwaysReferenceARealTemplate()
        {
            var creatures = AllCreatures();
            Assume.That(creatures.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            foreach (var creature in creatures)
            {
                foreach (var ability in creature.Abilities)
                {
                    foreach (var effect in ability.Effects)
                    {
                        if (effect.Type != EffectType.Summon)
                        {
                            continue;
                        }
                        Assert.IsNotNull(effect.SummonTemplate,
                            $"{creature.Id}'s {ability.Trigger} ability has a Summon effect with no SummonTemplate assigned");
                    }
                }
            }
        }

        [Test]
        public void BotRoster_EveryRoundReferencesTierEligibleCreatures()
        {
            var config = LoadConfig();
            var botTeams = AllBotTeams();
            Assume.That(config, Is.Not.Null, "Run Pets/Generate Starter Content first.");
            Assume.That(botTeams.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            foreach (var botTeam in botTeams)
            {
                int maxTier = config.TierForRound(botTeam.Round);
                foreach (var slot in botTeam.Slots)
                {
                    Assert.IsNotNull(slot.Creature, $"round {botTeam.Round} has a slot with no creature assigned");
                    Assert.LessOrEqual(slot.Creature.Tier, maxTier,
                        $"round {botTeam.Round} fields {slot.Creature.Id} (tier {slot.Creature.Tier}), but only tier {maxTier} is unlocked by that round");
                }
            }
        }

        [Test]
        public void BotRoster_RoundsAreUniqueAndPositive()
        {
            var botTeams = AllBotTeams();
            Assume.That(botTeams.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            Assert.IsTrue(botTeams.All(t => t.Round >= 1), "every bot round should be >= 1");

            var duplicates = botTeams.GroupBy(t => t.Round).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate bot roster rounds: {string.Join(", ", duplicates)}");
        }

        [Test]
        public void JsonExport_RoundTripsCreatureFields()
        {
            var creatures = AllCreatures();
            Assume.That(creatures.Count, Is.GreaterThan(0), "Run Pets/Generate Starter Content first.");

            foreach (var creature in creatures)
            {
                string json = ContentJsonExporter.ToJson(creature);
                var parsed = JsonUtility.FromJson<CreatureExportShape>(json);

                Assert.AreEqual(creature.Id, parsed.id, $"{creature.Id}: id mismatch after export/re-parse");
                Assert.AreEqual(creature.DisplayName, parsed.displayName, $"{creature.Id}: displayName mismatch after export/re-parse");
                Assert.AreEqual(creature.Tier, parsed.tier, $"{creature.Id}: tier mismatch after export/re-parse");
                Assert.AreEqual(creature.BaseAttack, parsed.baseAttack, $"{creature.Id}: baseAttack mismatch after export/re-parse");
                Assert.AreEqual(creature.BaseHealth, parsed.baseHealth, $"{creature.Id}: baseHealth mismatch after export/re-parse");
                Assert.AreEqual(creature.LevelAttackBonus, parsed.levelAttackBonus, $"{creature.Id}: levelAttackBonus mismatch after export/re-parse");
                Assert.AreEqual(creature.LevelHealthBonus, parsed.levelHealthBonus, $"{creature.Id}: levelHealthBonus mismatch after export/re-parse");
            }
        }

        [System.Serializable]
        private sealed class CreatureExportShape
        {
            public string id;
            public string displayName;
            public int tier;
            public int baseAttack;
            public int baseHealth;
            public int levelAttackBonus;
            public int levelHealthBonus;
        }
    }
}
