using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Simulation;
using UnityEditor;

namespace Pets.Tests
{
    /// <summary>
    /// Smoke-tests the actual Phase 0 curated-species content (client/Assets/Content) through
    /// PokemonInstanceFactory and the battle simulator — the only tests that exercise real
    /// ScriptableObject assets rather than hand-built Simulation POCOs. The
    /// EveryCuratedSpecies... test is what satisfies PLAN.md Phase 0's exit criteria: "a full
    /// PvE-node fight resolves deterministically via the new Step model."
    /// </summary>
    public class PokemonContentTests
    {
        private static PokemonSpeciesLibrary LoadSpeciesLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:PokemonSpeciesLibrary", new[] { "Assets/Content" });
            Assume.That(guids.Length, Is.GreaterThan(0), "PokemonSpeciesLibrary.asset not found under Assets/Content.");
            return AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void CuratedRoster_HasAtLeastThirteenSpecies_EachWithAResolvablePassive()
        {
            var library = LoadSpeciesLibrary();

            Assert.GreaterOrEqual(library.AllSpecies.Count, 13);
            foreach (var species in library.AllSpecies)
            {
                Assert.IsNotNull(species.Passive, $"{species.DisplayName} has no passive assigned");
                Assert.IsNotEmpty(species.Passive.Effects, $"{species.DisplayName}'s passive ({species.Passive.Id}) has no effects");
            }
        }

        /// <summary>A fresh instance is its species' sheet stats at level 1, with Health scaled by
        /// Data/StatGrowth — the one place the roster sheet's numbers are transformed on the way
        /// into a fight.</summary>
        [Test]
        public void EverySpecies_ConvertsToAPokemonInstance_WithItsTierLineStats()
        {
            var library = LoadSpeciesLibrary();

            foreach (var species in library.AllSpecies)
            {
                var instance = PokemonInstanceFactory.Create(species, $"{species.DisplayName}#1");
                var expected = StatGrowth.AtExp(species, instance.InstanceId, 0, 0);

                // A fresh mon wears its species' tier line exactly — growth is what EXP adds on top.
                Assert.AreEqual(species.BaseAttack, instance.CurrentStats.Attack);
                Assert.AreEqual(species.BaseHealth, instance.CurrentStats.Health);
                Assert.AreEqual(species.BaseSpeed, instance.CurrentStats.Speed);
                Assert.AreEqual(expected.Health, instance.CurrentHP);
                Assert.AreEqual(species.Passive.Id, instance.PassiveId);
                Assert.IsNotNull(instance.ResolvedPassive);
                Assert.AreEqual(species.Passive.Effects.Count, instance.ResolvedPassive.Effects.Count);
            }
        }

        /// <summary>
        /// Builds a real Forest-style PvE line-up (two curated species vs. two more) and runs it
        /// through PrecomputedStepLogRunner start to finish — this is the literal Phase 0 exit
        /// criterion: a full fight resolving deterministically through the new Step model,
        /// covered by content that actually ships (not just hand-built POCOs).
        /// </summary>
        [Test]
        public void FullPvEFight_ResolvesDeterministically_AgainstRealCuratedContent()
        {
            var library = LoadSpeciesLibrary();
            var bySpecies = library.AllSpecies.ToDictionary(s => s.DisplayName);

            // The four bulkiest species in the low tiers, chosen because a passive needs three
            // Steps to charge at Speed 1 and most matchups don't last that long: every species
            // spends its tier's points across Attack and Health, and EXP adds +1 to both, so a
            // mon's Attack and Health sit close together and Leads trade lethal blows almost at
            // once (ADR 0008, Data/StatGrowth). Metapod and Jigglypuff are the exceptions that
            // buy the fight enough Steps to exercise a trigger — that short-fight lethality is
            // expected content-balance behaviour (PLAN.md §10), not a simulator requirement.
            var playerLineUp = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Metapod"], "player-metapod"),
                PokemonInstanceFactory.Create(bySpecies["Jigglypuff"], "player-jigglypuff")
            };
            var wildLineUp = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Kakuna"], "wild-kakuna"),
                PokemonInstanceFactory.Create(bySpecies["Togepi"], "wild-togepi")
            };

            var log = PrecomputedStepLogRunner.Run(playerLineUp, wildLineUp, seed: 12345);

            Assert.IsTrue(
                log.Outcome == BattleOutcome.SideAWins || log.Outcome == BattleOutcome.SideBWins || log.Outcome == BattleOutcome.Draw);
            Assert.IsTrue(log.Events.Count > 0, "a real fight between real content should generate events");
            Assert.AreEqual(StepEventKind.BattleEnd, log.Events.Last().Kind);
            Assert.IsTrue(log.Events.Any(e => e.Kind == StepEventKind.PassiveTriggered), "at least one hand-authored passive should have fired over the course of the fight");

            // Re-run the identical match-up and assert the whole log matches exactly, proving the
            // fight is reproducible from real content, not just from hand-built test POCOs.
            var rematchPlayer = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Metapod"], "player-metapod"),
                PokemonInstanceFactory.Create(bySpecies["Jigglypuff"], "player-jigglypuff")
            };
            var rematchWild = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Kakuna"], "wild-kakuna"),
                PokemonInstanceFactory.Create(bySpecies["Togepi"], "wild-togepi")
            };
            var replayLog = PrecomputedStepLogRunner.Run(rematchPlayer, rematchWild, seed: 12345);

            Assert.AreEqual(log.Events.Count, replayLog.Events.Count);
            for (int i = 0; i < log.Events.Count; i++)
            {
                Assert.AreEqual(log.Events[i].ToString(), replayLog.Events[i].ToString(), $"event {i} diverged on replay");
            }
        }

        [Test]
        public void OnDemandRunner_ProducesTheSameFirstSteps_AsThePrecomputedRunner()
        {
            // battle-sim-spec.md §7: both runners must call the same AdvanceStep, so the first
            // few Steps of a PvE (on-demand) run and a Gym/PvP (precomputed) run of the identical
            // match-up must agree exactly.
            var library = LoadSpeciesLibrary();
            var bySpecies = library.AllSpecies.ToDictionary(s => s.DisplayName);

            List<PokemonInstance> BuildA() => new List<PokemonInstance> { PokemonInstanceFactory.Create(bySpecies["Pikachu"], "a-pikachu") };
            List<PokemonInstance> BuildB() => new List<PokemonInstance> { PokemonInstanceFactory.Create(bySpecies["Geodude"], "b-geodude") };

            var precomputed = PrecomputedStepLogRunner.Run(BuildA(), BuildB(), seed: 7);

            var onDemand = new OnDemandStepRunner(BuildA(), BuildB(), seed: 7);
            var onDemandEvents = new List<StepEvent>();
            for (int i = 0; i < 3 && !onDemand.IsBattleOver; i++)
            {
                onDemandEvents.AddRange(onDemand.NextStep());
            }

            var precomputedFirstN = precomputed.Events.Where(e => e.Kind != StepEventKind.BattleEnd).Take(onDemandEvents.Count).ToList();
            Assert.AreEqual(precomputedFirstN.Count, onDemandEvents.Count);
            for (int i = 0; i < onDemandEvents.Count; i++)
            {
                Assert.AreEqual(precomputedFirstN[i].ToString(), onDemandEvents[i].ToString());
            }
        }
    }
}
