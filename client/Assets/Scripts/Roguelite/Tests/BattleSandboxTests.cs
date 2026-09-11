using System.Linq;
using NUnit.Framework;
using Pets.Roguelite.Sandbox;
using Pets.Roguelite.Simulation;
using UnityEngine;

namespace Pets.Roguelite.Tests
{
    /// <summary>
    /// The interactive testing area: run a matchup, read the printed transcript (Console /
    /// Test Runner output), add your own test methods as you add species/passives. See
    /// Sandbox/README.md for how to extend this. A handful of these are real regression checks
    /// (the vocabulary-coverage and bare-exchange tests); the sample-species ones are meant to be
    /// edited/duplicated freely as you experiment — they're not sacred.
    /// </summary>
    public class BattleSandboxTests
    {
        [Test]
        public void BareAttackExchange_NoAbilities_HigherEffectiveAttackWins()
        {
            // No passives, speed 0 (irrelevant with no passive) — isolates the core Step-loop
            // damage math, same spirit as the old spec's equivalent test.
            var a = SandboxContent.NewMon("A", PokemonType.Normal, null, attack: 5, health: 20, speed: 0, passive: null);
            var b = SandboxContent.NewMon("B", PokemonType.Normal, null, attack: 4, health: 20, speed: 0, passive: null);

            var state = SandboxContent.NewBattle(new[] { a }, new[] { b }, seed: 1);
            var (log, outcome) = PrecomputedStepLogRunner.Run(state);

            Debug.Log(BattleTranscript.Render(log));

            // B faints at Step 4 (20 - 4*5 <= 0) while A still has 4 HP (20 - 4*4).
            Assert.AreEqual(BattleOutcome.SideAWins, outcome);
        }

        [Test]
        public void Ember_BurnsEnemyLeadAndTicksInTheSameStepItTriggers()
        {
            // Real xlsx base stats put Attack and HP on similar scales — there's no Defense stat
            // in this combat model (design doc §10.2: Attack goes straight to HP), so a real
            // Charmander-vs-Squirtle pairing trades a mutual KO on Step 1, before any passive can
            // charge naturally. That's a genuine balance signal worth knowing (see PLAN.md §10),
            // not a test bug — but it means *this* test isolates Ember's mechanics against a
            // durable dummy rather than a real matchup. Use a real matchup + PrecomputedStepLogRunner
            // when you want to see whether two specific Pokémon actually survive to trade abilities.
            var charmander = SandboxContent.Charmander();
            var dummy = SandboxContent.NewMon("Dummy", PokemonType.Normal, null, attack: 5, health: 100, speed: 0, passive: null);
            var state = SandboxContent.NewBattle(new[] { charmander }, new[] { dummy }, seed: 2);

            charmander.Charge = BattleConfig.ChargeThreshold; // force Ember Burst this Step
            var events = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(events));

            // Status is applied and ticked in the same Step here because ApplyStatusTick (§3
            // step 4) runs right after passive resolution (§3 step 3), within the same call.
            Assert.That(events.Any(e => e.Kind == StepEventKind.StatusApplied && e.Status == StatusType.Burned));
            Assert.That(events.Any(e => e.Kind == StepEventKind.StatusTick && e.Status == StatusType.Burned));
        }

        [Test]
        public void AquaShield_AbsorbsIncomingDamageOnTheFollowingStep()
        {
            // Step order matters here (docs/battle-sim-spec.md §3): the attack exchange happens
            // *before* passive resolution within a Step, so a shield granted this Step can't
            // absorb this Step's own incoming hit — only the next one. Worth a test precisely
            // because it's the kind of ordering detail that's easy to get backwards by hand.
            var squirtle = SandboxContent.Squirtle();
            var attacker = SandboxContent.NewMon("Bruiser", PokemonType.Normal, null, attack: 20, health: 60, speed: 0, passive: null);
            var state = SandboxContent.NewBattle(new[] { squirtle }, new[] { attacker }, seed: 3);

            squirtle.Charge = BattleConfig.ChargeThreshold; // force Aqua Shield to trigger this Step
            var stepOneEvents = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(stepOneEvents));

            Assert.That(stepOneEvents.Any(e => e.Kind == StepEventKind.Damage && e.TargetName == "Squirtle" && e.Amount == 20),
                "This Step's hit lands before the shield exists — full 20 damage, no absorption yet.");
            Assert.AreEqual(10, squirtle.ShieldAmount, "Aqua Shield should have granted a 10-point shield for the next Step.");

            var stepTwoEvents = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(stepTwoEvents));

            Assert.That(stepTwoEvents.Any(e => e.Kind == StepEventKind.Damage && e.TargetName == "Squirtle" && e.Amount == 10),
                "The shield should have absorbed 10 of this Step's 20 raw damage, leaving 10 to HP.");
            Assert.AreEqual(0, squirtle.ShieldAmount, "The 10-point shield should be fully consumed by the 20-damage hit.");
        }

        [Test]
        public void LeechBite_HealsBulbasaurWhenTriggered()
        {
            // Same note as the Ember test above: a durable dummy stand-in, not a real Pikachu,
            // so Bulbasaur reliably survives long enough to see its own passive resolve.
            var bulbasaur = SandboxContent.Bulbasaur();
            var dummy = SandboxContent.NewMon("Dummy", PokemonType.Normal, null, attack: 5, health: 100, speed: 0, passive: null);
            bulbasaur.CurrentHP -= 15; // leave room to observe the heal
            var state = SandboxContent.NewBattle(new[] { bulbasaur }, new[] { dummy }, seed: 4);

            bulbasaur.Charge = BattleConfig.ChargeThreshold; // force Leech Bite this Step
            var events = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(events));

            Assert.That(events.Any(e => e.Kind == StepEventKind.Heal && e.TargetName == "Bulbasaur"),
                "Expected Leech Bite to heal Bulbasaur.");
            Assert.That(events.Any(e => e.Kind == StepEventKind.Damage && e.TargetName == "Dummy" && e.Amount == 8),
                "Expected Leech Bite's own damage tick against the enemy Lead too.");
        }

        [Test]
        public void StaticShock_ParalyzesEnemyLead()
        {
            // Same durable-dummy pattern as Ember/LeechBite above — a real Pikachu-vs-Geodude
            // trade is mutually lethal on Step 1 with these placeholder stats.
            var pikachu = SandboxContent.Pikachu();
            var dummy = SandboxContent.NewMon("Dummy", PokemonType.Normal, null, attack: 5, health: 100, speed: 0, passive: null);
            var state = SandboxContent.NewBattle(new[] { pikachu }, new[] { dummy }, seed: 5);

            pikachu.Charge = BattleConfig.ChargeThreshold; // force Static Shock this Step
            var events = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(events));

            Assert.That(events.Any(e => e.Kind == StepEventKind.StatusApplied && e.Status == StatusType.Paralyzed));
        }

        [Test]
        public void ThreeVersusThree_WithBench_CompletesWithoutHittingSafetyCap()
        {
            // Exercises bench promotion (index 2+ only becomes active once something in front faints)
            // across a spread of the sample roster — this is the "try different teams" scenario.
            var teamA = new[] { SandboxContent.Pikachu(), SandboxContent.Geodude(), SandboxContent.Charmander() };
            var teamB = new[] { SandboxContent.Gastly(), SandboxContent.Squirtle(), SandboxContent.Bulbasaur() };
            var state = SandboxContent.NewBattle(teamA, teamB, seed: 6);

            var (log, outcome) = PrecomputedStepLogRunner.Run(state);
            Debug.Log(BattleTranscript.Render(log));

            Assert.LessOrEqual(state.StepNumber, BattleConfig.StepCap);
            Assert.AreEqual(StepEventKind.BattleEnd, log.Last().Kind);
            Assert.IsTrue(outcome == BattleOutcome.SideAWins || outcome == BattleOutcome.SideBWins || outcome == BattleOutcome.Draw);
        }

        [Test]
        public void EffectVocabulary_AllTypesProduceExpectedEvents()
        {
            // A synthetic passive exercising every EffectType at once — a reference for "does my
            // new effect type actually work" plus a regression check against silent breakage.
            var vocabTest = new PassiveDefinition("vocab-test", "Vocab Test", PokemonType.Normal,
                EffectDefinition.DealDamage(TargetSelector.EnemyLead, 5),
                EffectDefinition.Heal(TargetSelector.Self, 10),
                EffectDefinition.Shield(TargetSelector.Self, 10),
                EffectDefinition.ApplyStatus(TargetSelector.EnemyLead, StatusType.Poisoned),
                EffectDefinition.ClearStatus(TargetSelector.Self),
                EffectDefinition.BuffAttack(TargetSelector.Self, 4),
                EffectDefinition.BuffSpeed(TargetSelector.Self, 6),
                EffectDefinition.DamageReduction(TargetSelector.EnemyLead, 2),
                EffectDefinition.Lifesteal(TargetSelector.Self, 25),
                EffectDefinition.ModifyChargeRate(TargetSelector.EnemyLead, -20));

            var tester = SandboxContent.NewMon("Tester", PokemonType.Normal, null, attack: 0, health: 50, speed: 1, passive: vocabTest);
            tester.CurrentHP = 40; // leave room for the Heal effect to actually do something
            var dummy = SandboxContent.NewMon("Dummy", PokemonType.Normal, null, attack: 0, health: 50, speed: 1, passive: null);

            var state = SandboxContent.NewBattle(new[] { tester }, new[] { dummy }, seed: 7);
            tester.Charge = BattleConfig.ChargeThreshold; // force the trigger this Step, ignoring Speed

            var events = BattleSimulator.AdvanceStep(state);
            Debug.Log(BattleTranscript.Render(events));

            Assert.That(events.Any(e => e.Kind == StepEventKind.PassiveTriggered));
            Assert.That(events.Any(e => e.Kind == StepEventKind.Damage && e.TargetName == "Dummy"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.Heal && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.Shield && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.StatusApplied && e.Status == StatusType.Poisoned));
            Assert.That(events.Any(e => e.Kind == StepEventKind.StatusCleared && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.BuffAttack && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.BuffSpeed && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.DamageReductionApplied && e.TargetName == "Dummy"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.LifestealModified && e.TargetName == "Tester"));
            Assert.That(events.Any(e => e.Kind == StepEventKind.ChargeRateModified && e.TargetName == "Dummy"));
        }
    }
}
