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
    /// Runs every JSON fixture under /shared/fixtures through the battle simulator — the
    /// behavioral contract a future server-side reimplementation (PLAN.md §6 Phase 3) must also
    /// satisfy. See shared/README.md for the fixture shape.
    ///
    /// Two fixture modes, selected by whether "steps" is present and non-zero:
    ///   - Omitted/0: run PrecomputedStepLogRunner to completion and assert "expected"
    ///     (outcome/faintOrder/survivors) — for scenarios about how a fight concludes.
    ///   - N > 0: run exactly N raw AdvanceStep calls and assert "expectedState" (exact
    ///     currentHP/shield/charge/status per named mon) — for scenarios about precise
    ///     mid-battle mechanics (same-Step tie-breaking, status stacking, shield/reduction math)
    ///     that a terminal outcome alone wouldn't pin down.
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
        public void Fixture_MatchesExpectedBehavior(string path)
        {
            var fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            var name = Path.GetFileName(path);

            var lineUpA = ToLineUp(fixture.lineUpA);
            var lineUpB = ToLineUp(fixture.lineUpB);

            if (fixture.steps > 0)
            {
                RunStepsAndAssertState(lineUpA, lineUpB, fixture, name);
            }
            else
            {
                RunToCompletionAndAssertOutcome(lineUpA, lineUpB, fixture, name);
            }
        }

        private static void RunToCompletionAndAssertOutcome(List<PokemonInstance> lineUpA, List<PokemonInstance> lineUpB, FixtureFile fixture, string name)
        {
            var log = PrecomputedStepLogRunner.Run(lineUpA, lineUpB, fixture.seed);

            Assert.AreEqual(fixture.expected.outcome, log.Outcome.ToString(), $"outcome mismatch in {name}");

            var actualFaintOrder = log.Events.Where(e => e.Kind == StepEventKind.Faint).Select(e => e.SourceInstanceId).ToList();
            CollectionAssert.AreEqual(fixture.expected.faintOrder, actualFaintOrder, $"faint order mismatch in {name}");

            // Read off the log's FinalState, not the line-ups passed in: the runner simulates
            // copies, so the PokemonInstances handed to it come back untouched — which is the
            // point (see BattleCombatant), and which is why the result has to be reported.
            var survivors = log.FinalState.LineUpA.Concat(log.FinalState.LineUpB).ToList();
            foreach (var survivor in fixture.expected.survivors)
            {
                var actual = survivors.FirstOrDefault(m => m.InstanceId == survivor.instanceId);
                Assert.IsNotNull(actual, $"expected survivor {survivor.instanceId} not found in {name}");
                Assert.AreEqual(survivor.currentHP, actual.CurrentHP, $"survivor {survivor.instanceId} HP mismatch in {name}");
            }
        }

        private static void RunStepsAndAssertState(List<PokemonInstance> lineUpA, List<PokemonInstance> lineUpB, FixtureFile fixture, string name)
        {
            // Through the on-demand runner rather than driving AdvanceStep against a hand-built
            // BattleState: advancing a Step at a time is exactly what that runner is for, and going
            // through it means these fixtures exercise the same line-up-to-combatant copy a real
            // PvE fight does.
            var runner = new OnDemandStepRunner(lineUpA, lineUpB, fixture.seed);
            for (int i = 0; i < fixture.steps; i++)
            {
                runner.NextStep();
            }

            var allMons = runner.State.LineUpA.Concat(runner.State.LineUpB).ToList();
            foreach (var expected in fixture.expectedState)
            {
                var actual = allMons.FirstOrDefault(m => m.InstanceId == expected.instanceId);
                Assert.IsNotNull(actual, $"expected mon {expected.instanceId} not found in {name}");
                Assert.AreEqual(expected.currentHP, actual.CurrentHP, $"{expected.instanceId} currentHP mismatch in {name}");
                Assert.AreEqual(expected.shield, actual.Shield, $"{expected.instanceId} shield mismatch in {name}");
                Assert.AreEqual(expected.charge, actual.Charge, $"{expected.instanceId} charge mismatch in {name}");
                var expectedStatus = string.IsNullOrEmpty(expected.status) ? (StatusType?)null : (StatusType)Enum.Parse(typeof(StatusType), expected.status);
                Assert.AreEqual(expectedStatus, actual.Status, $"{expected.instanceId} status mismatch in {name}");
            }
        }

        private static List<PokemonInstance> ToLineUp(List<FixtureMon> mons)
        {
            return mons.Select(ToPokemonInstance).ToList();
        }

        private static PokemonInstance ToPokemonInstance(FixtureMon m)
        {
            return new PokemonInstance
            {
                InstanceId = m.instanceId,
                CurrentStats = new Stats { Attack = m.attack, Health = m.health, Speed = m.speed },
                CurrentHP = m.health,
                ResolvedPassive = m.passive == null || string.IsNullOrEmpty(m.passive.id) ? null : ToPassive(m.passive)
            };
        }

        private static PassiveDefinition ToPassive(FixturePassive p)
        {
            return new PassiveDefinition
            {
                Id = p.id,
                Effects = p.effects.Select(ToEffect).ToList()
            };
        }

        private static EffectDefinition ToEffect(FixtureEffect e)
        {
            return new EffectDefinition
            {
                Type = (EffectType)Enum.Parse(typeof(EffectType), e.type),
                Target = (TargetSelector)Enum.Parse(typeof(TargetSelector), e.target),
                Amount = e.amount,
                Status = string.IsNullOrEmpty(e.status) ? default : (StatusType)Enum.Parse(typeof(StatusType), e.status)
            };
        }

        [Serializable]
        private class FixtureFile
        {
            public int seed;
            public List<FixtureMon> lineUpA = new List<FixtureMon>();
            public List<FixtureMon> lineUpB = new List<FixtureMon>();
            public int steps;
            public FixtureExpectedOutcome expected = new FixtureExpectedOutcome();
            public List<FixtureExpectedState> expectedState = new List<FixtureExpectedState>();
        }

        [Serializable]
        private class FixtureMon
        {
            public string instanceId;
            public int attack;
            public int health;
            public int speed;
            public FixturePassive passive;
        }

        [Serializable]
        private class FixturePassive
        {
            public string id;
            public List<FixtureEffect> effects = new List<FixtureEffect>();
        }

        [Serializable]
        private class FixtureEffect
        {
            public string type;
            public string target;
            public int amount;
            public string status;
        }

        [Serializable]
        private class FixtureExpectedOutcome
        {
            public string outcome;
            public List<string> faintOrder = new List<string>();
            public List<FixtureSurvivor> survivors = new List<FixtureSurvivor>();
        }

        [Serializable]
        private class FixtureSurvivor
        {
            public string instanceId;
            public int currentHP;
        }

        [Serializable]
        private class FixtureExpectedState
        {
            public string instanceId;
            public int currentHP;
            public int shield;
            public int charge;
            public string status;
        }
    }
}
