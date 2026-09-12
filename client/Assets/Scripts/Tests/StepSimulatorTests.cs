using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Simulation;

namespace Pets.Tests
{
    /// <summary>
    /// Direct unit tests against hand-built BattleCombatant POCOs, covering every mechanic in
    /// battle-sim-spec.md: the Step loop's ordering, charge-meter timing, simultaneous Lead
    /// exchange, passive triggering (incl. same-Step tie-breaking), Lead/Support promotion on
    /// faint, statuses, and battle-end/safety-cap conditions. See GoldenFixtureTests.cs for
    /// scenarios run against /shared/fixtures instead of hand-built state.
    /// </summary>
    public class StepSimulatorTests
    {
        private static BattleCombatant Mon(string id, int attack, int health, int speed, PassiveDefinition passive = null)
        {
            return new BattleCombatant
            {
                InstanceId = id,
                CurrentStats = new Stats { Attack = attack, Health = health, Speed = speed },
                CurrentHP = health,
                ResolvedPassive = passive
            };
        }

        private static BattleState State(List<BattleCombatant> a, List<BattleCombatant> b)
        {
            return new BattleState { LineUpA = a, LineUpB = b };
        }

        // --- 1. Attack exchange -------------------------------------------------------------

        [Test]
        public void AttackExchange_IsSimultaneous_BothLeadsTakeDamage()
        {
            var a = Mon("a-lead", attack: 5, health: 20, speed: 0);
            var b = Mon("b-lead", attack: 3, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(17, a.CurrentHP, "a-lead should take b-lead's attack (3)");
            Assert.AreEqual(15, b.CurrentHP, "b-lead should take a-lead's attack (5)");
        }

        [Test]
        public void AttackExchange_DoesNotHappen_WhenEitherSideHasNoLead()
        {
            var a = Mon("a-lead", attack: 5, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant>());

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(20, a.CurrentHP);
            Assert.IsFalse(events.Any(e => e.Kind == StepEventKind.Damage));
        }

        // --- 2. Charge accumulation & passive triggering ------------------------------------

        [Test]
        public void Charge_AccruesBySpeed_AndTriggersPassiveOnceThresholdReached()
        {
            var passive = new PassiveDefinition
            {
                Id = "test-damage",
                Effects = { new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemyLead, Amount = 4 } }
            };
            var a = Mon("a-lead", attack: 0, health: 20, speed: BattleConfig.ChargeThreshold, passive: passive);
            var b = Mon("b-lead", attack: 0, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.PassiveTriggered && e.SourceInstanceId == "a-lead"));
            Assert.AreEqual(16, b.CurrentHP, "a-lead's passive should have dealt 4 damage to b-lead");
            Assert.AreEqual(0, a.Charge, "charge resets to 0 on trigger, no overshoot carry-over");
        }

        [Test]
        public void Charge_BelowThreshold_DoesNotTriggerPassive()
        {
            var passive = new PassiveDefinition
            {
                Id = "test-damage",
                Effects = { new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemyLead, Amount = 4 } }
            };
            var a = Mon("a-lead", attack: 0, health: 20, speed: BattleConfig.ChargeThreshold - 1, passive: passive);
            var b = Mon("b-lead", attack: 0, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.IsFalse(events.Any(e => e.Kind == StepEventKind.PassiveTriggered));
            Assert.AreEqual(20, b.CurrentHP);
        }

        [Test]
        public void Passive_CanTriggerMoreThanOnce_OverMultipleSteps()
        {
            var passive = new PassiveDefinition
            {
                Id = "test-damage",
                Effects = { new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemyLead, Amount = 1 } }
            };
            var a = Mon("a-lead", attack: 0, health: 999, speed: BattleConfig.ChargeThreshold, passive: passive);
            var b = Mon("b-lead", attack: 0, health: 999, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });
            var rng = new DeterministicRandom(1);

            int triggerCount = 0;
            for (int i = 0; i < 5; i++)
            {
                triggerCount += BattleSimulator.AdvanceStep(state, rng).Count(e => e.Kind == StepEventKind.PassiveTriggered);
            }

            Assert.AreEqual(5, triggerCount, "a mon at exactly threshold speed should trigger every Step");
        }

        // --- 3. Same-Step multi-trigger tie-break (battle-sim-spec.md §6) -------------------

        [Test]
        public void SameStepTrigger_LeadsResolveBeforeSupports()
        {
            var leadEffect = new PassiveDefinition { Id = "lead-fx", Effects = { new EffectDefinition { Type = EffectType.BuffAttack, Target = TargetSelector.Self, Amount = 1 } } };
            var supportEffect = new PassiveDefinition { Id = "support-fx", Effects = { new EffectDefinition { Type = EffectType.BuffAttack, Target = TargetSelector.Self, Amount = 1 } } };
            var lead = Mon("a-lead", 0, 20, BattleConfig.ChargeThreshold, leadEffect);
            var support = Mon("a-support", 0, 20, BattleConfig.ChargeThreshold, supportEffect);
            var enemy = Mon("b-lead", 0, 20, 0);
            var state = State(new List<BattleCombatant> { lead, support }, new List<BattleCombatant> { enemy });

            var triggers = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1))
                .Where(e => e.Kind == StepEventKind.PassiveTriggered).Select(e => e.SourceInstanceId).ToList();

            CollectionAssert.AreEqual(new[] { "a-lead", "a-support" }, triggers);
        }

        [Test]
        public void SameStepTrigger_WithinRole_HigherSpeedResolvesFirst()
        {
            var fx = new PassiveDefinition { Id = "fx", Effects = { new EffectDefinition { Type = EffectType.BuffAttack, Target = TargetSelector.Self, Amount = 1 } } };
            var slowLead = Mon("a-lead", 0, 20, BattleConfig.ChargeThreshold, fx);
            var fastLead = Mon("b-lead", 0, 20, BattleConfig.ChargeThreshold * 2, fx);
            var state = State(new List<BattleCombatant> { slowLead }, new List<BattleCombatant> { fastLead });

            var triggers = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1))
                .Where(e => e.Kind == StepEventKind.PassiveTriggered).Select(e => e.SourceInstanceId).ToList();

            CollectionAssert.AreEqual(new[] { "b-lead", "a-lead" }, triggers, "b-lead has higher Speed and should resolve first");
        }

        [Test]
        public void SameStepTrigger_FullTie_SideAResolvesBeforeSideB()
        {
            var fx = new PassiveDefinition { Id = "fx", Effects = { new EffectDefinition { Type = EffectType.BuffAttack, Target = TargetSelector.Self, Amount = 1 } } };
            var a = Mon("a-lead", 0, 20, BattleConfig.ChargeThreshold, fx);
            var b = Mon("b-lead", 0, 20, BattleConfig.ChargeThreshold, fx);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            var triggers = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1))
                .Where(e => e.Kind == StepEventKind.PassiveTriggered).Select(e => e.SourceInstanceId).ToList();

            CollectionAssert.AreEqual(new[] { "a-lead", "b-lead" }, triggers);
        }

        // --- 4. Faint & promotion -------------------------------------------------------------

        [Test]
        public void Faint_PromotesSupportToLead_AndNextDormantToSupport()
        {
            var a = Mon("a-lead", attack: 0, health: 20, speed: 0);
            var b = Mon("b-lead", attack: 100, health: 20, speed: 0);
            var bSupport = Mon("b-support", attack: 0, health: 20, speed: 0);
            var bDormant = Mon("b-dormant", attack: 0, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b, bSupport, bDormant });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(0, state.LineUpA.Count, "a-lead should have fainted and been removed");
            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.Faint && e.SourceInstanceId == "a-lead"));
        }

        [Test]
        public void Faint_OfLead_PromotesSupportAndBacksFillSupportSlot()
        {
            var aLead = Mon("a-lead", attack: 0, health: 1, speed: 0);
            var aSupport = Mon("a-support", attack: 0, health: 20, speed: 0);
            var aDormant = Mon("a-dormant", attack: 0, health: 20, speed: 0);
            var bLead = Mon("b-lead", attack: 5, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { aLead, aSupport, aDormant }, new List<BattleCombatant> { bLead });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreSame(aSupport, state.LeadA, "support should be promoted to lead");
            Assert.AreSame(aDormant, state.SupportA, "next dormant mon should be promoted to support");
            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.Promotion && e.SourceInstanceId == "a-support"));
            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.Promotion && e.SourceInstanceId == "a-dormant"));
        }

        [Test]
        public void DormantMonPromoted_DoesNotAccrueChargeOnTheStepItWasPromoted()
        {
            // battle-sim-spec.md §3 step 2: a mon promoted mid-Step doesn't get a partial share of
            // that Step's charge accrual — it only starts charging on the Step after promotion.
            // aDormant is 3rd in line, so it's still dormant during steps 1-3 of this Step even
            // though aLead's faint (from this Step's exchange) will promote it up to Support in
            // step 4.
            var aLead = Mon("a-lead", attack: 0, health: 1, speed: 0);
            var aSupport = Mon("a-support", attack: 0, health: 20, speed: 0);
            var aDormant = Mon("a-dormant", attack: 0, health: 20, speed: 50);
            var bLead = Mon("b-lead", attack: 5, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { aLead, aSupport, aDormant }, new List<BattleCombatant> { bLead });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreSame(aDormant, state.SupportA, "aDormant should have been promoted to Support this Step");
            Assert.AreEqual(0, aDormant.Charge, "aDormant was still dormant during charge accrual this Step, so it shouldn't have accrued yet");
        }

        // --- 5. Statuses (battle-sim-spec.md §5) ----------------------------------------------

        [Test]
        public void Paralyzed_HalvesChargeAccrual()
        {
            var a = Mon("a-lead", 0, 999, 100);
            a.Status = StatusType.Paralyzed;
            var b = Mon("b-lead", 0, 999, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(50, a.Charge);
        }

        [Test]
        public void Asleep_ZeroesChargeAccrual()
        {
            var a = Mon("a-lead", 0, 999, 100);
            a.Status = StatusType.Asleep;
            var b = Mon("b-lead", 0, 999, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(0, a.Charge);
        }

        [Test]
        public void Poisoned_TickDamage_StacksInSeverityEachStep()
        {
            var a = Mon("a-lead", 0, 999, 0);
            a.Status = StatusType.Poisoned;
            a.StatusTickDamage = 2;
            a.PoisonStacks = 1;
            var b = Mon("b-lead", 0, 999, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });
            var rng = new DeterministicRandom(1);

            BattleSimulator.AdvanceStep(state, rng); // tick 1: 2 * 1 = 2
            BattleSimulator.AdvanceStep(state, rng); // tick 2: 2 * 2 = 4
            BattleSimulator.AdvanceStep(state, rng); // tick 3: 2 * 3 = 6

            Assert.AreEqual(999 - 2 - 4 - 6, a.CurrentHP);
        }

        [Test]
        public void Burned_TickDamage_DoesNotStack()
        {
            var a = Mon("a-lead", 0, 999, 0);
            a.Status = StatusType.Burned;
            a.StatusTickDamage = 3;
            var b = Mon("b-lead", 0, 999, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });
            var rng = new DeterministicRandom(1);

            BattleSimulator.AdvanceStep(state, rng);
            BattleSimulator.AdvanceStep(state, rng);
            BattleSimulator.AdvanceStep(state, rng);

            Assert.AreEqual(999 - 9, a.CurrentHP);
        }

        [Test]
        public void ClearStatus_RemovesStatus_AndAccrualResumesNextStep()
        {
            // An Asleep mon can never trigger its own cleanse (its own charge is permanently
            // zeroed), so this exercises the realistic path: an ally's passive clears it instead.
            var aLead = Mon("a-lead", 0, 999, 50);
            aLead.Status = StatusType.Asleep;
            var cleanseFx = new PassiveDefinition { Id = "cleanse", Effects = { new EffectDefinition { Type = EffectType.ClearStatus, Target = TargetSelector.Ally } } };
            var aSupport = Mon("a-support", 0, 999, BattleConfig.ChargeThreshold, cleanseFx);
            var bLead = Mon("b-lead", 0, 999, 0);
            var state = State(new List<BattleCombatant> { aLead, aSupport }, new List<BattleCombatant> { bLead });
            var rng = new DeterministicRandom(1);

            BattleSimulator.AdvanceStep(state, rng);
            Assert.IsNull(aLead.Status, "the support's cleanse should have cleared aLead's status");
            Assert.AreEqual(0, aLead.Charge, "clearing happens after this Step's charge accrual, so it's too late to affect this Step");

            BattleSimulator.AdvanceStep(state, rng);
            Assert.AreEqual(50, aLead.Charge, "with status cleared, aLead should accrue normally on the next Step");
        }

        // --- 6. Shield, damage reduction, lifesteal ------------------------------------------

        [Test]
        public void Shield_AbsorbsDamageBeforeHP()
        {
            var a = Mon("a-lead", attack: 0, health: 20, speed: 0);
            a.Shield = 3;
            var b = Mon("b-lead", attack: 5, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(0, a.Shield, "3 of the 5 incoming damage should have been absorbed");
            Assert.AreEqual(18, a.CurrentHP, "remaining 2 damage should have hit HP");
        }

        [Test]
        public void DamageReduction_FlatlyReducesIncomingDamage()
        {
            var a = Mon("a-lead", attack: 0, health: 20, speed: 0);
            a.DamageReductionFlat = 3;
            var b = Mon("b-lead", attack: 5, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(18, a.CurrentHP, "5 attack - 3 flat reduction = 2 damage");
        }

        [Test]
        public void Lifesteal_HealsAttackerByPercentOfDamageDealt()
        {
            var a = Mon("a-lead", attack: 10, health: 20, speed: 0);
            a.CurrentHP = 10;
            a.LifestealPercent = 0.5f;
            var b = Mon("b-lead", attack: 0, health: 20, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(10, b.CurrentHP, "sanity: b should have taken 10 damage");
            Assert.AreEqual(15, a.CurrentHP, "a dealt 10 damage and should heal back 50% = 5, capped at max health 20");
        }

        // --- 7. Target selectors --------------------------------------------------------------

        [Test]
        public void Ally_Selector_IsNoOp_WhenNoSupportPresent()
        {
            var fx = new PassiveDefinition { Id = "fx", Effects = { new EffectDefinition { Type = EffectType.Heal, Target = TargetSelector.Ally, Amount = 5 } } };
            var a = Mon("a-lead", 0, 20, BattleConfig.ChargeThreshold, fx);
            var b = Mon("b-lead", 0, 20, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.IsFalse(events.Any(e => e.Kind == StepEventKind.Heal), "no Support exists, so Ally-targeted Heal should be skipped");
        }

        [Test]
        public void FaintedMon_IsNotAValidTarget_EffectIsSkipped()
        {
            var fx = new PassiveDefinition { Id = "fx", Effects = { new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemySupport, Amount = 5 } } };
            var a = Mon("a-lead", 0, 20, BattleConfig.ChargeThreshold, fx);
            var b = Mon("b-lead", 0, 20, 0);
            var faintedSupport = Mon("b-support", 0, 20, 0);
            faintedSupport.CurrentHP = 0;
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b, faintedSupport });

            var events = BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.IsFalse(events.Any(e => e.Kind == StepEventKind.Damage && e.TargetInstanceId == "b-support"));
        }

        // --- 8. Battle end & safety caps -------------------------------------------------------

        [Test]
        public void Battle_SideWithZeroMonsLoses()
        {
            var a = Mon("a-lead", 0, 20, 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant>());

            Assert.AreEqual(BattleOutcome.SideAWins, BattleSimulator.DetermineOutcome(state));
        }

        [Test]
        public void Battle_BothSidesEmpty_IsADraw()
        {
            var state = State(new List<BattleCombatant>(), new List<BattleCombatant>());

            Assert.AreEqual(BattleOutcome.Draw, BattleSimulator.DetermineOutcome(state));
        }

        [Test]
        public void SimultaneousDoubleKO_IsADraw()
        {
            var a = Mon("a-lead", attack: 100, health: 1, speed: 0);
            var b = Mon("b-lead", attack: 100, health: 1, speed: 0);
            var state = State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

            BattleSimulator.AdvanceStep(state, new DeterministicRandom(1));

            Assert.AreEqual(BattleOutcome.Draw, BattleSimulator.DetermineOutcome(state));
        }

        [Test]
        public void PrecomputedRunner_StalemateHitsStepCap_ForcesADraw()
        {
            // Neither side can ever kill the other (0 attack, heals every trigger) - the runner
            // must force a draw at BattleConfig.StepCap rather than looping forever.
            var healFx = new PassiveDefinition { Id = "self-heal", Effects = { new EffectDefinition { Type = EffectType.Heal, Target = TargetSelector.Self, Amount = 1000 } } };
            var a = Mon("a-lead", attack: 0, health: 1000, speed: BattleConfig.ChargeThreshold, healFx);
            var b = Mon("b-lead", attack: 0, health: 1000, speed: BattleConfig.ChargeThreshold, healFx);

            var log = PrecomputedStepLogRunner.Run(new List<BattleCombatant> { a }, new List<BattleCombatant> { b }, seed: 1);

            Assert.AreEqual(BattleOutcome.Draw, log.Outcome);
            Assert.IsTrue(log.Events.Last().Kind == StepEventKind.BattleEnd);
        }

        // --- 9. Determinism ---------------------------------------------------------------------

        [Test]
        public void PrecomputedRunner_SameSeedAndLineUps_ProduceIdenticalEventLogs()
        {
            List<BattleCombatant> BuildA() => new List<BattleCombatant>
            {
                Mon("a-lead", 4, 15, 60, new PassiveDefinition { Id = "fx", Effects = { new EffectDefinition { Type = EffectType.DealDamage, Target = TargetSelector.EnemyLead, Amount = 2 } } }),
                Mon("a-support", 2, 10, 30)
            };
            List<BattleCombatant> BuildB() => new List<BattleCombatant>
            {
                Mon("b-lead", 3, 15, 45),
                Mon("b-support", 2, 10, 20)
            };

            var log1 = PrecomputedStepLogRunner.Run(BuildA(), BuildB(), seed: 42);
            var log2 = PrecomputedStepLogRunner.Run(BuildA(), BuildB(), seed: 42);

            Assert.AreEqual(log1.Events.Count, log2.Events.Count);
            for (int i = 0; i < log1.Events.Count; i++)
            {
                Assert.AreEqual(log1.Events[i].ToString(), log2.Events[i].ToString(), $"event {i} diverged");
            }
        }
    }
}
