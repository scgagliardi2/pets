using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Gameplay;
using Pets.Simulation;
using UnityEditor;
using UnityEngine;

// Deliberately no blanket `using System;` — this file uses both System.Random (for shop-policy
// decisions) and UnityEngine.Random (to seed a reproducible run per docs/testing-harness-plan.md
// §2.5), and a bare `Random` would be ambiguous between them if both namespaces were in scope.

namespace Pets.Tests
{
    /// <summary>
    /// Plays complete runs (shop -> battle -> shop -> ... -> win/loss) against the real starter
    /// content, using scripted shop-AI policies instead of a human, across many seeds. Catches
    /// what isolated unit tests can't: crashes, softlocks, or invalid state that only show up from
    /// a specific sequence of shop decisions interacting with the real roster. See
    /// docs/testing-harness-plan.md §2.
    ///
    /// Drives ShopEconomy/TeamStateConverter/BattleSimulator directly rather than through
    /// RunController — the MonoBehaviour/SaveSystem/event wiring on top of that state machine
    /// already has PlayMode coverage in RunControllerPlayModeTests.cs, so this harness only needs
    /// the underlying logic.
    /// </summary>
    public class FullRunHarnessTests
    {
        // PR-gating default: kept small enough to run in well under a minute. Override with the
        // PETS_HARNESS_SEED_COUNT env var for a larger sweep (see the scheduled CI job in
        // .github/workflows/ci.yml, which runs this with a much higher count).
        private static int SeedCount =>
            int.TryParse(System.Environment.GetEnvironmentVariable("PETS_HARNESS_SEED_COUNT"), out var n) && n > 0
                ? n
                : 50;

        private const int RoundSafetyCap = 200;

        private CreatureLibrary library;
        private BotRosterLibrary botRoster;
        private ShopConfig config;

        [SetUp]
        public void SetUp()
        {
            library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Content/CreatureLibrary.asset");
            botRoster = AssetDatabase.LoadAssetAtPath<BotRosterLibrary>("Assets/Content/BotRosterLibrary.asset");
            config = AssetDatabase.LoadAssetAtPath<ShopConfig>("Assets/Content/ShopConfig.asset");

            Assume.That(library, Is.Not.Null, "Run Pets/Generate Starter Content first.");
            Assume.That(botRoster, Is.Not.Null, "Run Pets/Generate Starter Content first.");
            Assume.That(config, Is.Not.Null, "Run Pets/Generate Starter Content first.");
        }

        private static IEnumerable<string> PolicyNames()
        {
            yield return "Greedy";
            yield return "Upgrade";
            yield return "Random";
        }

        [TestCaseSource(nameof(PolicyNames))]
        public void FullRun_ManySeeds_NeverThrowsOrProducesInvalidState(string policyName)
        {
            int seedCount = SeedCount;
            var failures = new List<string>();
            var metrics = new List<RunMetrics>();

            for (int seed = 1; seed <= seedCount; seed++)
            {
                try
                {
                    metrics.Add(PlayOneRun(policyName, seed));
                }
                catch (System.Exception e)
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

        private RunMetrics PlayOneRun(string policyName, int seed)
        {
            // Seeds UnityEngine.Random, which both ShopEconomy's offer/reroll rolls and the
            // battle-seed draw below consume, so an entire run is reproducible from just its
            // seed — see docs/testing-harness-plan.md §2.5 for why this is enough without
            // touching production code.
            UnityEngine.Random.InitState(seed);
            var policyRng = new System.Random(seed);

            var state = new RunState();
            ShopEconomy.StartRun(state, config, library);
            AssertInvariants(state);

            int battlesFought = 0;

            for (int roundsPlayed = 0; roundsPlayed < RoundSafetyCap; roundsPlayed++)
            {
                Assert.AreEqual(GamePhase.Shop, state.Phase, "expected to be in the shop phase at the top of the loop");

                PlayShopTurn(policyName, state, policyRng);
                AssertInvariants(state);

                var botTeam = botRoster.GetByRound(state.Round);
                if (botTeam == null)
                {
                    // Survived the whole scripted roster.
                    return new RunMetrics(policyName, seed, state.Round, won: true, battlesFought);
                }

                var playerSlots = state.Board.ConvertAll(c => (c.Definition, c.Level, c.BonusAttack, c.BonusHealth));
                var teamA = TeamStateConverter.ToTeamState(playerSlots, "player");
                var teamB = TeamStateConverter.ToTeamState(botTeam, "bot");

                int battleSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                var log = BattleSimulator.Run(teamA, teamB, battleSeed);
                battlesFought++;

                bool won = log.Outcome == BattleOutcome.TeamAWins;
                if (!won)
                {
                    state.Lives -= 1;
                }

                if (state.Lives <= 0)
                {
                    return new RunMetrics(policyName, seed, state.Round, won: false, battlesFought);
                }

                state.Round += 1;
                ShopEconomy.StartShopPhase(state, config, library);
                AssertInvariants(state);
            }

            throw new System.InvalidOperationException($"run did not terminate within {RoundSafetyCap} rounds");
        }

        private void AssertInvariants(RunState state)
        {
            Assert.GreaterOrEqual(state.Gold, 0, "gold went negative");
            Assert.GreaterOrEqual(state.Lives, 0, "lives went negative");
            Assert.LessOrEqual(state.Board.Count, config.BoardMaxSize, "board exceeded max size");
            foreach (var creature in state.Board)
            {
                Assert.IsNotNull(creature.Definition, "board creature has a null definition");
            }
        }

        private void PlayShopTurn(string policyName, RunState state, System.Random policyRng)
        {
            switch (policyName)
            {
                case "Greedy":
                    PlayShopTurnGreedy(state);
                    break;
                case "Upgrade":
                    PlayShopTurnUpgrade(state);
                    break;
                case "Random":
                    PlayShopTurnRandom(state, policyRng);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(policyName), policyName, "unknown shop policy");
            }
        }

        /// <summary>Repeatedly buys the cheapest affordable offer into an open board slot until
        /// gold or board space runs out. Never sells or rerolls.</summary>
        private void PlayShopTurnGreedy(RunState state)
        {
            while (true)
            {
                int cheapestIndex = -1;
                int cheapestCost = int.MaxValue;
                for (int i = 0; i < state.ShopSlots.Count; i++)
                {
                    var offer = state.ShopSlots[i].Offer;
                    if (offer == null)
                    {
                        continue;
                    }
                    int cost = config.BuyCost(offer.Tier);
                    if (cost < cheapestCost)
                    {
                        cheapestCost = cost;
                        cheapestIndex = i;
                    }
                }

                if (cheapestIndex < 0 || state.Board.Count >= config.BoardMaxSize || state.Gold < cheapestCost)
                {
                    break;
                }

                ShopEconomy.Buy(state, config, cheapestIndex);
            }
        }

        /// <summary>Prioritizes buying whatever completes a 3-of-a-kind combine, falls back to
        /// Greedy for the rest of its gold, then sells its weakest creature to make room for one
        /// more buy if the board is full and it can still afford something. A second, distinct
        /// decision pattern from Greedy/Random — not meant to be "good" play.</summary>
        private void PlayShopTurnUpgrade(RunState state)
        {
            bool boughtForCombine;
            do
            {
                boughtForCombine = false;
                for (int i = 0; i < state.ShopSlots.Count; i++)
                {
                    var offer = state.ShopSlots[i].Offer;
                    if (offer == null || state.Gold < config.BuyCost(offer.Tier))
                    {
                        continue;
                    }
                    bool completesCombine = state.Board.Count(c => c.Level == 1 && c.Definition.Id == offer.Id) == 2;
                    if (!completesCombine)
                    {
                        continue;
                    }
                    if (ShopEconomy.Buy(state, config, i))
                    {
                        boughtForCombine = true;
                        break;
                    }
                }
            } while (boughtForCombine);

            PlayShopTurnGreedy(state);

            bool boardFull = state.Board.Count >= config.BoardMaxSize;
            bool canAffordSomething = state.ShopSlots.Any(s => s.Offer != null && state.Gold >= config.BuyCost(s.Offer.Tier));
            if (boardFull && canAffordSomething && state.Board.Count > 0)
            {
                int weakestIndex = 0;
                int weakestScore = int.MaxValue;
                for (int i = 0; i < state.Board.Count; i++)
                {
                    var def = state.Board[i].Definition;
                    int score = def.BaseAttack + def.BaseHealth;
                    if (score < weakestScore)
                    {
                        weakestScore = score;
                        weakestIndex = i;
                    }
                }
                ShopEconomy.Sell(state, config, weakestIndex);
                PlayShopTurnGreedy(state);
            }
        }

        /// <summary>Picks a uniformly random legal-ish action (buy/sell/reroll/freeze/stop) up to
        /// a capped number of times per turn. Doesn't play sensibly on purpose — the point is to
        /// exercise sequences a human wouldn't choose.</summary>
        private void PlayShopTurnRandom(RunState state, System.Random rng)
        {
            const int maxActionsPerTurn = 20;
            for (int i = 0; i < maxActionsPerTurn; i++)
            {
                switch (rng.Next(5))
                {
                    case 0:
                        if (state.ShopSlots.Count > 0)
                        {
                            ShopEconomy.Buy(state, config, rng.Next(state.ShopSlots.Count));
                        }
                        break;
                    case 1:
                        if (state.Board.Count > 0)
                        {
                            ShopEconomy.Sell(state, config, rng.Next(state.Board.Count));
                        }
                        break;
                    case 2:
                        ShopEconomy.Reroll(state, config, library);
                        break;
                    case 3:
                        if (state.ShopSlots.Count > 0)
                        {
                            ShopEconomy.ToggleFreeze(state, rng.Next(state.ShopSlots.Count));
                        }
                        break;
                    case 4:
                        return;
                }
            }
        }

        private static void WriteMetrics(string policyName, List<RunMetrics> metrics)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"full-run-metrics-{policyName}.json");
            File.WriteAllText(path, JsonUtility.ToJson(new MetricsFile { runs = metrics }, true));
        }

        [System.Serializable]
        private sealed class RunMetrics
        {
            public string policy;
            public int seed;
            public int roundsReached;
            public bool won;
            public int battlesFought;

            public RunMetrics(string policy, int seed, int roundsReached, bool won, int battlesFought)
            {
                this.policy = policy;
                this.seed = seed;
                this.roundsReached = roundsReached;
                this.won = won;
                this.battlesFought = battlesFought;
            }
        }

        [System.Serializable]
        private sealed class MetricsFile
        {
            public List<RunMetrics> runs = new List<RunMetrics>();
        }
    }
}
