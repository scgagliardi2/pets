using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using UnityEditor;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>
    /// Plays complete runs (Map -> PvE/Camp node -> ... -> forest cleared or out of Morale)
    /// against the real curated content, across many seeds. Catches what isolated unit tests
    /// can't: crashes or invalid state that only show up from a specific sequence of node
    /// outcomes interacting with the real roster. See docs/testing-harness-plan.md §2.
    ///
    /// This is a direct, UI-free port of the real per-node resolution logic in
    /// Gameplay/PvEClashController.cs (PvE) and Gameplay/LocationFlowController.cs (node
    /// advancement/Morale/terminal-state handling) — the scene wiring itself already has PlayMode
    /// coverage in ForestScenePlayModeTests.cs, so this harness only needs the underlying logic.
    ///
    /// Phase 0's Forest is a fixed, linear 5-node sequence with no shop/reordering yet (PLAN.md
    /// §6), so the only real per-run player decision right now is whether to catch a defeated wild
    /// mon after a PvE win — hence two catch policies rather than the richer buy/sell/reroll
    /// policy set an economy-driven harness would need once Phase 1 lands.
    /// </summary>
    public class FullRunHarnessTests
    {
        // PR-gating default: kept small enough to run in well under a minute. Override with the
        // PETS_HARNESS_SEED_COUNT env var for a larger sweep (see the scheduled CI job in
        // .github/workflows/ci.yml, which runs this with a much higher count).
        private static int SeedCount =>
            int.TryParse(Environment.GetEnvironmentVariable("PETS_HARNESS_SEED_COUNT"), out var n) && n > 0
                ? n
                : 50;

        // Generous relative to what Phase 0's Forest actually needs: at most 4 PvE nodes to win
        // plus at most 3 losses total (Morale starts at 3 and never recovers in Phase 0) plus 1
        // Camp node. This cap exists as a defensive backstop against a hypothetical bug that stops
        // Morale or node advancement from ever terminating, not because real play gets close to it.
        private const int NodeAttemptSafetyCap = 50;

        private PokemonSpeciesLibrary library;

        [SetUp]
        public void SetUp()
        {
            var guids = AssetDatabase.FindAssets("t:PokemonSpeciesLibrary", new[] { "Assets/Content" });
            Assume.That(guids.Length, Is.GreaterThan(0), "PokemonSpeciesLibrary.asset not found under Assets/Content.");
            library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Assume.That(library.AllSpecies.Count, Is.GreaterThanOrEqualTo(2),
                "need at least 2 curated species for a starting Lead/Support pair.");
        }

        [Test]
        public void FullRun_ManySeeds_CatchingEveryDefeatedMon_NeverThrowsOrProducesInvalidState()
        {
            RunSweep("CatchAll", catchAll: true);
        }

        [Test]
        public void FullRun_ManySeeds_NeverCatching_NeverThrowsOrProducesInvalidState()
        {
            RunSweep("CatchNone", catchAll: false);
        }

        private void RunSweep(string policyName, bool catchAll)
        {
            int seedCount = SeedCount;
            var failures = new List<string>();
            var metrics = new List<RunMetrics>();

            for (int seed = 1; seed <= seedCount; seed++)
            {
                try
                {
                    metrics.Add(PlayOneRun(policyName, seed, catchAll));
                }
                catch (Exception e)
                {
                    failures.Add($"seed {seed}: {e.GetType().Name}: {e.Message}");
                }
            }

            WriteMetrics(policyName, metrics);

            if (failures.Count > 0)
            {
                Assert.Fail($"{policyName} policy failed on {failures.Count}/{seedCount} seeds:\n{string.Join("\n", failures)}");
            }
        }

        private RunMetrics PlayOneRun(string policyName, int seed, bool catchAll)
        {
            // Every RNG draw in this flow (EncounterGenerator, PrecomputedStepLogRunner) takes an
            // explicit seed rather than touching UnityEngine.Random global state, so — unlike the
            // pre-pivot harness — no global seeding is needed here for a run to be reproducible
            // from just its seed.
            var lead = library.AllSpecies[0];
            var support = library.AllSpecies[1];

            var state = new RunState
            {
                RunSeed = seed,
                Nodes = ForestLocationFactory.BuildNodes(),
                LineUp =
                {
                    PokemonInstanceFactory.Create(lead, "player-lead"),
                    PokemonInstanceFactory.Create(support, "player-support")
                }
            };
            AssertInvariants(state);

            int pveWins = 0;
            int pveLosses = 0;

            for (int attempt = 0; attempt < NodeAttemptSafetyCap; attempt++)
            {
                if (state.IsRunOver || (state.CurrentNode.Cleared && !state.HasNextNode))
                {
                    break;
                }

                var node = state.CurrentNode;
                if (node.Type == NodeType.PvE)
                {
                    if (ResolvePvENode(state, catchAll))
                    {
                        pveWins++;
                    }
                    else
                    {
                        pveLosses++;
                    }
                }
                else
                {
                    CampResolver.Resolve(state);
                    state.AdvanceToNextNode();
                }

                AssertInvariants(state);
            }

            bool clearedForest = !state.IsRunOver && state.CurrentNode.Cleared && !state.HasNextNode;
            if (!clearedForest && !state.IsRunOver)
            {
                throw new InvalidOperationException($"run did not reach a terminal state within {NodeAttemptSafetyCap} node attempts");
            }

            return new RunMetrics(
                policyName, seed, clearedForest ? "ClearedForest" : "OutOfMorale",
                state.Nodes.Count(n => n.Cleared), pveWins, pveLosses, state.Box.Count, state.Morale);
        }

        /// <summary>Direct port of PvEClashController.Begin/Resolve + the node-advancement half of
        /// LocationFlowController.OnPvEResolved, without the UI/coroutine playback. Returns
        /// whether the player won.</summary>
        private bool ResolvePvENode(RunState state, bool catchAll)
        {
            var playerLineUp = state.LineUp.Select(PokemonInstanceFactory.ResetForBattle).ToList();
            if (state.NextBattleAttackBonusPercent > 0f)
            {
                foreach (var mon in playerLineUp)
                {
                    mon.CurrentStats.Attack = Mathf.RoundToInt(mon.CurrentStats.Attack * (1f + state.NextBattleAttackBonusPercent));
                }
                state.NextBattleAttackBonusPercent = 0f;
            }

            int seed = state.RunSeed + state.CurrentNodeIndex;
            var wildLineUp = EncounterGenerator.GenerateWildLineUp(library, ForestLocationFactory.TypeBias, seed, $"wild-{state.CurrentNodeIndex}");
            var wildSnapshot = new List<PokemonInstance>(wildLineUp);

            var log = PrecomputedStepLogRunner.Run(playerLineUp, wildLineUp, seed);
            bool won = log.Outcome == BattleOutcome.SideAWins;

            if (won)
            {
                if (catchAll)
                {
                    foreach (var defeated in CatchResolver.GetDefeated(wildSnapshot))
                    {
                        CatchResolver.Catch(state, defeated, library);
                    }
                }
                state.AdvanceToNextNode();
            }
            else
            {
                // A lost PvE fight doesn't clear the node (LocationFlowController.OnPvEResolved) —
                // the player retries the same encounter. Since nothing about the player's line-up
                // or the wild encounter changes between retries in Phase 0 (no reordering, no
                // shop, and the attack buff above is already consumed), a retry after a loss
                // resolves identically and loses again, every time, until Morale runs out. That's
                // a real property of the current design worth knowing, not a harness bug — see the
                // PR notes.
                state.Morale--;
            }

            return won;
        }

        private void AssertInvariants(RunState state)
        {
            Assert.GreaterOrEqual(state.Morale, 0, "morale went negative");
            Assert.AreEqual(2, state.LineUp.Count, "line-up should always have exactly a Lead and Support in Phase 0");
            Assert.IsTrue(state.CurrentNodeIndex >= 0 && state.CurrentNodeIndex < state.Nodes.Count, "current node index out of range");
            foreach (var mon in state.LineUp.Concat(state.Box))
            {
                Assert.IsNotNull(library.GetById(mon.SpeciesId), $"{mon.InstanceId} references an unresolvable species id {mon.SpeciesId}");
            }
        }

        private static void WriteMetrics(string policyName, List<RunMetrics> metrics)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"full-run-metrics-{policyName}.json");
            File.WriteAllText(path, JsonUtility.ToJson(new MetricsFile { runs = metrics }, true));
        }

        [Serializable]
        private sealed class RunMetrics
        {
            public string policy;
            public int seed;
            public string outcome;
            public int nodesCleared;
            public int pveWins;
            public int pveLosses;
            public int catches;
            public int finalMorale;

            public RunMetrics(string policy, int seed, string outcome, int nodesCleared, int pveWins, int pveLosses, int catches, int finalMorale)
            {
                this.policy = policy;
                this.seed = seed;
                this.outcome = outcome;
                this.nodesCleared = nodesCleared;
                this.pveWins = pveWins;
                this.pveLosses = pveLosses;
                this.catches = catches;
                this.finalMorale = finalMorale;
            }
        }

        [Serializable]
        private sealed class MetricsFile
        {
            public List<RunMetrics> runs = new List<RunMetrics>();
        }
    }
}
