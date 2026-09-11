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

        [Test]
        public void EverySpecies_ConvertsToAPokemonInstance_WithBaseStatsIntact()
        {
            var library = LoadSpeciesLibrary();

            foreach (var species in library.AllSpecies)
            {
                var instance = PokemonInstanceFactory.Create(species, $"{species.DisplayName}#1");

                Assert.AreEqual(species.BaseAttack, instance.CurrentStats.Attack);
                Assert.AreEqual(species.BaseHealth, instance.CurrentStats.Health);
                Assert.AreEqual(species.BaseSpeed, instance.CurrentStats.Speed);
                Assert.AreEqual(species.BaseHealth, instance.CurrentHP);
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

            // Caterpie/Weedle specifically (rather than e.g. the starters) because their
            // Attack is low relative to their HP — most of this curated slice's placeholder
            // stats are high enough relative to HP that Leads trade lethal blows in the very
            // first Step or two, before charge has time to build toward a passive trigger. That
            // one-or-two-Step lethality is itself expected/documented content-balance behavior
            // for Phase 0's placeholder numbers (PLAN.md §10) — this matchup is chosen so the
            // fight actually lasts long enough to exercise a passive trigger, not because the
            // simulator requires slow fights.
            var playerLineUp = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Caterpie"], "player-caterpie"),
                PokemonInstanceFactory.Create(bySpecies["Squirtle"], "player-squirtle")
            };
            var wildLineUp = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Weedle"], "wild-weedle"),
                PokemonInstanceFactory.Create(bySpecies["Zubat"], "wild-zubat")
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
                PokemonInstanceFactory.Create(bySpecies["Caterpie"], "player-caterpie"),
                PokemonInstanceFactory.Create(bySpecies["Squirtle"], "player-squirtle")
            };
            var rematchWild = new List<PokemonInstance>
            {
                PokemonInstanceFactory.Create(bySpecies["Weedle"], "wild-weedle"),
                PokemonInstanceFactory.Create(bySpecies["Zubat"], "wild-zubat")
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
