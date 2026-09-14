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
    /// <summary>EditMode coverage for catching in the wild (design doc §12.1): the odds formula,
    /// the ball inventory, taking a caught mon out of a fight, and the whole throw end to end.
    ///
    /// Kept apart from RunMetaTests, which covers the run layer broadly and the older post-fight
    /// "pick 1 from defeated" stub — these are about the live, mid-battle mechanic that replaces
    /// it. Content is built in-memory with CreateInstance, the same approach and for the same
    /// reason as RunMetaTests: no dependency on the curated roster's exact contents.</summary>
    public class CatchTests
    {
        private static PokemonSpeciesDefinitionAsset MakeSpecies(int id, string name,
            int attack = 10, int health = 50, int speed = 10)
        {
            var species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            species.Id = id;
            species.DisplayName = name;
            species.Type1 = PokemonType.Bug;
            species.Tier = SpeciesTier.MinTier;
            species.BaseAttack = attack;
            species.BaseHealth = health;
            species.BaseSpeed = speed;
            species.HealthGrowthPercent = 100;
            return species;
        }

        private static PokemonSpeciesLibrary MakeLibrary(params PokemonSpeciesDefinitionAsset[] species)
        {
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies = species.ToList();
            return library;
        }

        private static BattleCombatant Combatant(string id, int currentHP, int maxHP,
            StatusType? status = null, PokemonInstance source = null)
        {
            return new BattleCombatant
            {
                InstanceId = id,
                Source = source,
                CurrentHP = currentHP,
                CurrentStats = new Stats { Attack = 5, Health = maxHP, Speed = 5 },
                Status = status
            };
        }

        // ---- Odds -------------------------------------------------------------------------

        /// <summary>The "weaken it first" loop, which is the whole shape of the mechanic: odds at
        /// full health are the ball's base chance and nothing more, and rise as the target
        /// weakens.</summary>
        [Test]
        public void Odds_AreTheBallsBaseChanceAtFullHealth_AndRiseAsHpFalls()
        {
            Assert.AreEqual(BallCatalog.BaseChance(BallTier.Poke),
                CatchOdds.ChanceFor(BallTier.Poke, 100, 100, null), 0.0001f);

            float half = CatchOdds.ChanceFor(BallTier.Poke, 50, 100, null);
            float nearlyOut = CatchOdds.ChanceFor(BallTier.Poke, 5, 100, null);

            Assert.Greater(half, BallCatalog.BaseChance(BallTier.Poke));
            Assert.Greater(nearlyOut, half);
        }

        [Test]
        public void Odds_BetterBallsBeatWorseOnes_AtEveryHealth()
        {
            foreach (int hp in new[] { 100, 60, 25, 1 })
            {
                Assert.Less(CatchOdds.ChanceFor(BallTier.Poke, hp, 100, null),
                    CatchOdds.ChanceFor(BallTier.Great, hp, 100, null), $"at {hp} HP");
                Assert.Less(CatchOdds.ChanceFor(BallTier.Great, hp, 100, null),
                    CatchOdds.ChanceFor(BallTier.Ultra, hp, 100, null), $"at {hp} HP");
            }
        }

        /// <summary>Design doc §12.1 wants status to be worth a meaningful bonus, so that
        /// status-inflicting passives are useful for catching and not only for fighting.</summary>
        [Test]
        public void Odds_AnyStatusAddsTheSameFlatBonus()
        {
            float clean = CatchOdds.ChanceFor(BallTier.Great, 40, 100, null);
            foreach (var status in new[] { StatusType.Poisoned, StatusType.Burned, StatusType.Paralyzed, StatusType.Asleep })
            {
                Assert.AreEqual(CatchOdds.StatusBonus,
                    CatchOdds.ChanceFor(BallTier.Great, 40, 100, status) - clean, 0.0001f, $"{status}");
            }
        }

        /// <summary>No throw is ever a certainty — the "will it break free" beat needs the
        /// possibility of breaking free.</summary>
        [Test]
        public void Odds_AreCappedBelowCertainty_EvenInTheBestCase()
        {
            float best = CatchOdds.ChanceFor(BallTier.Ultra, 0, 100, StatusType.Asleep);
            Assert.AreEqual(CatchOdds.MaxChance, best, 0.0001f);
            Assert.Less(best, 1f);
        }

        [Test]
        public void Odds_HandleAZeroMaxHpCombatant_WithoutDividingByZero()
        {
            Assert.AreEqual(BallCatalog.BaseChance(BallTier.Poke) + CatchOdds.WeakenedBonus,
                CatchOdds.ChanceFor(BallTier.Poke, 0, 0, null), 0.0001f);
        }

        /// <summary>The roll has to actually fire at the rate the formula advertises — a
        /// resolution slip or an off-by-one inequality would leave ChanceFor correct and the game
        /// wrong.</summary>
        [Test]
        public void Roll_LandsAtRoughlyTheStatedOdds_OverManyThrows()
        {
            var rng = new DeterministicRandom(12345);
            var target = Combatant("wild", 50, 100);
            float expected = CatchOdds.ChanceFor(BallTier.Great, target);

            const int Trials = 20000;
            int hits = 0;
            for (int i = 0; i < Trials; i++)
            {
                if (CatchOdds.Roll(BallTier.Great, target, rng))
                {
                    hits++;
                }
            }

            Assert.AreEqual(expected, hits / (float)Trials, 0.02f);
        }

        [Test]
        public void Roll_IsReproducibleFromTheSeed()
        {
            var target = Combatant("wild", 50, 100);
            var a = new DeterministicRandom(999);
            var b = new DeterministicRandom(999);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(CatchOdds.Roll(BallTier.Poke, target, a),
                    CatchOdds.Roll(BallTier.Poke, target, b), $"throw {i}");
            }
        }

        // ---- Inventory --------------------------------------------------------------------

        [Test]
        public void Inventory_SpendsDownToEmpty_AndRefusesToGoFurther()
        {
            var balls = new BallInventory();
            balls.Add(BallTier.Poke, 2);

            Assert.IsTrue(balls.TrySpend(BallTier.Poke));
            Assert.IsTrue(balls.TrySpend(BallTier.Poke));
            Assert.IsFalse(balls.TrySpend(BallTier.Poke), "Spending past the last ball must fail rather than go negative");
            Assert.AreEqual(0, balls.CountOf(BallTier.Poke));
            Assert.IsFalse(balls.HasAny());
        }

        [Test]
        public void Inventory_StartingStock_GivesEveryTierAndLeansOnTheWeakest()
        {
            var balls = new BallInventory();
            balls.GrantStartingStock();

            foreach (var tier in BallCatalog.AllTiers)
            {
                Assert.Greater(balls.CountOf(tier), 0, $"{tier} should be stocked");
            }
            Assert.Greater(balls.CountOf(BallTier.Poke), balls.CountOf(BallTier.Ultra),
                "The EXP cap on weak balls is only a real decision if weak balls are what you mostly have");
        }

        /// <summary>Design doc §12.1: only the best ball guarantees the target's true strength.
        /// Expressed as an EXP cap because EXP is this game's level (ADR 0008).</summary>
        [Test]
        public void BallTiers_OnlyTheBestGuaranteesTrueStrength()
        {
            Assert.IsNull(BallCatalog.ExpCap(BallTier.Ultra));
            Assert.IsNotNull(BallCatalog.ExpCap(BallTier.Poke));
            Assert.Less(BallCatalog.ExpCap(BallTier.Poke).Value, BallCatalog.ExpCap(BallTier.Great).Value);
        }

        // ---- Removal from the fight -------------------------------------------------------

        /// <summary>Design doc §12.1: a caught mon leaves "exactly as if it had fainted (their
        /// Support steps up)" — but raises Caught, not Faint, so the post-fight stub that reads
        /// Faint events doesn't offer a mon that's already in the Box.</summary>
        [Test]
        public void RemoveCaught_PromotesBehindIt_AndRaisesCaughtRatherThanFaint()
        {
            var lead = Combatant("wild-lead", 10, 40);
            var support = Combatant("wild-support", 40, 40);
            var bench = Combatant("wild-bench", 40, 40);
            var state = new BattleState
            {
                LineUpA = new List<BattleCombatant> { Combatant("player", 40, 40) },
                LineUpB = new List<BattleCombatant> { lead, support, bench },
                StepNumber = 7
            };

            var events = BattleSimulator.RemoveCaught(state, Side.B, lead);

            Assert.AreEqual(StepEventKind.Caught, events[0].Kind);
            Assert.AreEqual("wild-lead", events[0].SourceInstanceId);
            Assert.AreEqual(7, events[0].Step, "The catch belongs to the Step it resolved on");
            Assert.IsFalse(events.Any(e => e.Kind == StepEventKind.Faint));
            Assert.AreSame(support, state.LeadB);
            Assert.AreSame(bench, state.SupportB);
            Assert.AreEqual(2, events.Count(e => e.Kind == StepEventKind.Promotion));
        }

        [Test]
        public void RemoveCaught_TakingTheLastMon_EndsTheFight()
        {
            var lone = Combatant("wild-lone", 5, 40);
            var state = new BattleState
            {
                LineUpA = new List<BattleCombatant> { Combatant("player", 40, 40) },
                LineUpB = new List<BattleCombatant> { lone }
            };

            BattleSimulator.RemoveCaught(state, Side.B, lone);

            Assert.IsEmpty(state.LineUpB);
            Assert.AreEqual(BattleOutcome.SideAWins, BattleSimulator.DetermineOutcome(state));
        }

        /// <summary>A throw queued at a Step boundary can land after the line-up has already moved
        /// on. Removing someone twice would take an extra mon out of the fight.</summary>
        [Test]
        public void RemoveCaught_IsANoOpForAMonNoLongerInTheLineUp()
        {
            var lead = Combatant("wild-lead", 10, 40);
            var support = Combatant("wild-support", 40, 40);
            var state = new BattleState
            {
                LineUpA = new List<BattleCombatant> { Combatant("player", 40, 40) },
                LineUpB = new List<BattleCombatant> { lead, support }
            };

            BattleSimulator.RemoveCaught(state, Side.B, lead);
            var second = BattleSimulator.RemoveCaught(state, Side.B, lead);

            Assert.IsEmpty(second);
            Assert.AreEqual(1, state.LineUpB.Count);
            Assert.AreSame(support, state.LeadB);
        }

        // ---- The whole throw --------------------------------------------------------------

        /// <summary>One wild encounter, set up for a throw. A class rather than a tuple because
        /// the retry helper below hands a whole one back and every field gets asserted on.</summary>
        private sealed class Encounter
        {
            public RunState State;
            public BattleState Battle;
            public PokemonSpeciesLibrary Library;
            public CatchResolver.ThrowResult Result;
        }

        /// <summary>A run with one ball of the given tier and a wild pair in front of it.
        /// <paramref name="veteranExp"/> adds a party member experienced enough to lift the run's
        /// catch-up floor, for the tests where that floor and a ball's EXP cap are in conflict.</summary>
        private static Encounter MakeEncounter(BallTier stocked, int wildLeadExp = 0, int wildLeadHP = 1,
            StatusType? status = null, int veteranExp = 0, bool loneWildMon = false)
        {
            var species = MakeSpecies(1, "Wildling");
            var library = MakeLibrary(species);

            var state = new RunState();
            state.LineUp.Add(PokemonInstanceFactory.Create(species, "player-lead"));
            if (veteranExp > 0)
            {
                state.LineUp.Add(ExperienceResolver.CreateAtExp(species, "veteran", veteranExp, library));
            }
            state.Balls.Add(stocked, 1);

            var wild = new List<PokemonInstance>
            {
                ExperienceResolver.CreateAtExp(species, "wild-0", wildLeadExp, library)
            };
            if (!loneWildMon)
            {
                wild.Add(ExperienceResolver.CreateAtExp(species, "wild-1", 0, library));
            }

            var battle = new BattleState
            {
                LineUpA = BattleCombatant.FromLineUp(state.LineUp),
                LineUpB = BattleCombatant.FromLineUp(wild)
            };
            battle.LineUpB[0].CurrentHP = wildLeadHP;
            battle.LineUpB[0].Status = status;

            return new Encounter { State = state, Battle = battle, Library = library };
        }

        /// <summary>Throws at fresh encounters until one produces <paramref name="wanted"/>, and
        /// returns that encounter for the test to assert on.
        ///
        /// Two details that are easy to get wrong and were, first time round. A *fresh encounter*
        /// per attempt, because a landed throw empties the line-up and every later throw would
        /// then be NotThrown rather than the outcome being waited for. And a *single shared RNG
        /// stream* rather than `new DeterministicRandom(seed)` per attempt: xorshift32 diffuses
        /// poorly from a small state, so the first draw off seeds 1, 2, 3... runs 369, 738, 1107 —
        /// near-perfectly linear, which makes consecutive low seeds anything but independent
        /// trials. One stream is also how a real battle uses its RNG. (Live seeds are
        /// RunSeed-derived and large, where the first draw is properly spread — this is a
        /// test-authoring trap, not a game bug.)</summary>
        private static Encounter ThrowUntil(CatchResolver.ThrowOutcome wanted, BallTier tier,
            Func<Encounter> makeEncounter, int maxAttempts = 200)
        {
            var rng = new DeterministicRandom(20260913);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var encounter = makeEncounter();
                encounter.Result = CatchResolver.TryCatch(encounter.State, encounter.Battle, Side.B,
                    tier, rng, encounter.Library);
                if (encounter.Result.Outcome == wanted)
                {
                    return encounter;
                }
            }
            Assert.Fail($"No throw produced {wanted} in {maxAttempts} attempts");
            return null;
        }

        /// <summary>A landed throw does four things at once: spends the ball, takes the mon out of
        /// the fight, promotes behind it, and puts it in the Box.</summary>
        [Test]
        public void TryCatch_OnSuccess_SpendsTheBall_RemovesTheMon_AndAddsItToTheBox()
        {
            var caught = ThrowUntil(CatchResolver.ThrowOutcome.Caught, BallTier.Ultra,
                () => MakeEncounter(BallTier.Ultra, status: StatusType.Asleep));

            Assert.AreEqual(0, caught.State.Balls.CountOf(BallTier.Ultra), "The ball was spent");
            Assert.AreEqual(1, caught.State.Box.Count);
            Assert.AreEqual(1, caught.Battle.LineUpB.Count, "The caught mon left the fight");
            Assert.AreEqual("wild-1", caught.Battle.LeadB.InstanceId, "Its Support stepped up");
            Assert.IsTrue(caught.Result.Events.Any(e => e.Kind == StepEventKind.Caught));
            Assert.IsFalse(caught.Result.Events.Any(e => e.Kind == StepEventKind.Faint));
        }

        /// <summary>A miss still costs the ball (design doc §12.1) and changes nothing else.</summary>
        [Test]
        public void TryCatch_OnFailure_SpendsTheBall_AndLeavesTheFightAlone()
        {
            // Full health, no status, weakest ball — the odds a miss is the likely roll at.
            var missed = ThrowUntil(CatchResolver.ThrowOutcome.BrokeFree, BallTier.Poke,
                () => MakeEncounter(BallTier.Poke, wildLeadHP: 50));

            Assert.IsTrue(missed.Result.SpentBall);
            Assert.AreEqual(0, missed.State.Balls.CountOf(BallTier.Poke));
            Assert.IsEmpty(missed.Result.Events);
            Assert.IsEmpty(missed.State.Box, "A miss adds nobody to the Box");
            Assert.AreEqual(2, missed.Battle.LineUpB.Count, "A miss leaves the line-up untouched");
            Assert.AreEqual("wild-0", missed.Battle.LeadB.InstanceId);
        }

        /// <summary>Running out of balls is not a throw: nothing is spent, nothing is rolled, and
        /// the caller can tell it apart from a miss.</summary>
        [Test]
        public void TryCatch_WithNoBallOfThatTier_DoesNotThrowAndCostsNothing()
        {
            var encounter = MakeEncounter(BallTier.Poke);

            var result = CatchResolver.TryCatch(encounter.State, encounter.Battle, Side.B,
                BallTier.Ultra, new DeterministicRandom(20260913), encounter.Library);

            Assert.AreEqual(CatchResolver.ThrowOutcome.NotThrown, result.Outcome);
            Assert.IsFalse(result.SpentBall);
            Assert.AreEqual(1, encounter.State.Balls.CountOf(BallTier.Poke), "The stocked tier is untouched too");
            Assert.IsEmpty(encounter.State.Box);
            Assert.AreEqual(2, encounter.Battle.LineUpB.Count);
        }

        [Test]
        public void TryCatch_WithNothingLeftToCatch_DoesNotSpendABall()
        {
            var encounter = MakeEncounter(BallTier.Ultra);
            encounter.Battle.LineUpB.Clear();

            var result = CatchResolver.TryCatch(encounter.State, encounter.Battle, Side.B,
                BallTier.Ultra, new DeterministicRandom(20260913), encounter.Library);

            Assert.AreEqual(CatchResolver.ThrowOutcome.NotThrown, result.Outcome);
            Assert.AreEqual(1, encounter.State.Balls.CountOf(BallTier.Ultra));
        }

        /// <summary>A default ThrowResult must not read as a successful catch — callers declare one
        /// before a loop fills it in, and NotThrown is the only safe zero value.</summary>
        [Test]
        public void ThrowResult_DefaultsToNotThrown()
        {
            CatchResolver.ThrowResult result = default;

            Assert.AreEqual(CatchResolver.ThrowOutcome.NotThrown, result.Outcome);
            Assert.IsFalse(result.Landed);
            Assert.IsFalse(result.SpentBall);
        }

        /// <summary>Design doc §12.1's under-levelled catch: a weak ball can land a strong mon but
        /// doesn't deliver it at full strength. The cap binds even against the run's catch-up
        /// floor — which is the reason to carry better balls at all.</summary>
        [Test]
        public void TryCatch_WithAWeakBall_CapsTheCaughtMonsExp()
        {
            int cap = BallCatalog.ExpCap(BallTier.Poke).Value;
            var caught = ThrowUntil(CatchResolver.ThrowOutcome.Caught, BallTier.Poke,
                () => MakeEncounter(BallTier.Poke, wildLeadExp: cap + 20, status: StatusType.Asleep,
                    veteranExp: cap + 30));

            Assert.Greater(ExperienceResolver.CatchUpExp(caught.State), cap,
                "The run's catch-up floor has to exceed the cap for this test to mean anything");
            Assert.AreEqual(cap, caught.Result.Caught.Exp,
                "A Poké Ball yields a capped mon however experienced the target or the run");
        }

        /// <summary>The best ball has no cap, so the same target arrives at full strength.</summary>
        [Test]
        public void TryCatch_WithTheBestBall_KeepsTheTargetsFullExp()
        {
            var caught = ThrowUntil(CatchResolver.ThrowOutcome.Caught, BallTier.Ultra,
                () => MakeEncounter(BallTier.Ultra, wildLeadExp: 40, status: StatusType.Asleep));

            Assert.AreEqual(40, caught.Result.Caught.Exp);
        }

        /// <summary>A caught mon joins usable rather than at whatever the wild roll gave it
        /// (ExperienceResolver.CatchUpExp), the same promise the post-fight stub makes.</summary>
        [Test]
        public void TryCatch_RaisesAWeakCatchToTheRunsCatchUpExp()
        {
            var caught = ThrowUntil(CatchResolver.ThrowOutcome.Caught, BallTier.Ultra,
                () => MakeEncounter(BallTier.Ultra, wildLeadExp: 0, status: StatusType.Asleep, veteranExp: 20));

            int floor = ExperienceResolver.CatchUpExp(caught.State);
            Assert.Greater(floor, 0, "The run needs a catch-up floor for this test to mean anything");
            Assert.AreEqual(floor, caught.Result.Caught.Exp);
        }

        /// <summary>Catching the wild side's last mon wins the fight, exactly as knocking it out
        /// would have (design doc §12.1).</summary>
        [Test]
        public void TryCatch_TakingTheLastWildMon_WinsTheFight()
        {
            var caught = ThrowUntil(CatchResolver.ThrowOutcome.Caught, BallTier.Ultra,
                () => MakeEncounter(BallTier.Ultra, status: StatusType.Asleep, loneWildMon: true));

            Assert.IsEmpty(caught.Battle.LineUpB);
            Assert.AreEqual(BattleOutcome.SideAWins, BattleSimulator.DetermineOutcome(caught.Battle));
        }
    }
}
