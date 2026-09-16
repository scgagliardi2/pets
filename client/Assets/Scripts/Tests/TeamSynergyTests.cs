using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Tests
{
    /// <summary>Team type synergies (Simulation/TeamSynergy, ADR 0015): one test per type's rule,
    /// plus how the pass sits in the runners — once, before Step 1, and a no-op for combatants that
    /// carry no types.</summary>
    public class TeamSynergyTests
    {
        private static BattleCombatant Mon(string id, int attack, int health, int speed, params PokemonType[] types)
        {
            return new BattleCombatant
            {
                InstanceId = id,
                CurrentStats = new Stats { Attack = attack, Health = health, Speed = speed },
                CurrentHP = health,
                Types = types.Length > 0 ? types.ToList() : null
            };
        }

        private static BattleState State(List<BattleCombatant> a, List<BattleCombatant> b) =>
            new BattleState { LineUpA = a, LineUpB = b };

        private static BattleState OneVsOne(BattleCombatant a, BattleCombatant b) =>
            State(new List<BattleCombatant> { a }, new List<BattleCombatant> { b });

        private static BattleCombatant Dummy(string id = "dummy") => Mon(id, 1, 10, 1);

        // --- Counting ------------------------------------------------------------------------

        [Test]
        public void NoTypes_AppliesNothing()
        {
            var a = Mon("a", 3, 10, 1);
            var b = Mon("b", 3, 10, 1);

            var events = TeamSynergy.Apply(OneVsOne(a, b));

            Assert.IsEmpty(events);
            Assert.AreEqual(10, a.CurrentHP);
            Assert.AreEqual(0, a.Charge);
            Assert.AreEqual(0, b.Shield);
        }

        [Test]
        public void EachMonCountsOncePerType_AndDualTypesCountForBoth()
        {
            var lineUp = new List<BattleCombatant>
            {
                Mon("1", 1, 10, 1, PokemonType.Water, PokemonType.Ground),
                Mon("2", 1, 10, 1, PokemonType.Water),
                Mon("3", 1, 10, 1, PokemonType.Water, PokemonType.Water),
            };

            Assert.AreEqual(3, TeamSynergy.CountOf(lineUp, PokemonType.Water));
            Assert.AreEqual(1, TeamSynergy.CountOf(lineUp, PokemonType.Ground));
            Assert.AreEqual(0, TeamSynergy.CountOf(lineUp, PokemonType.Fire));
        }

        [Test]
        public void AnnouncesOneEventPerTypePresent_WithTheCount()
        {
            var a = new List<BattleCombatant>
            {
                Mon("a1", 1, 10, 1, PokemonType.Normal),
                Mon("a2", 1, 10, 1, PokemonType.Normal, PokemonType.Flying),
            };

            var events = TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            var synergies = events.Where(e => e.Kind == StepEventKind.TypeSynergy).ToList();
            Assert.AreEqual(2, synergies.Count);
            Assert.AreEqual(2, synergies.Single(e => e.SynergyType == PokemonType.Normal).Amount);
            Assert.AreEqual(1, synergies.Single(e => e.SynergyType == PokemonType.Flying).Amount);
            Assert.IsTrue(synergies.All(e => e.SourceSide == Side.A && e.Step == 0));
        }

        // --- Own-team stats ------------------------------------------------------------------

        [Test]
        public void Normal_AddsHealthToEveryMon_PerNormalType()
        {
            var a = new List<BattleCombatant>
            {
                Mon("a1", 1, 10, 1, PokemonType.Normal),
                Mon("a2", 1, 10, 1, PokemonType.Normal),
                Mon("a3", 1, 10, 1, PokemonType.Fire),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            foreach (var mon in a)
            {
                Assert.AreEqual(12, mon.CurrentStats.Health, mon.InstanceId);
                Assert.AreEqual(12, mon.CurrentHP, mon.InstanceId);
            }
        }

        [Test]
        public void Fighting_AddsAttackToEveryMon_PerFightingType()
        {
            var a = new List<BattleCombatant>
            {
                Mon("a1", 3, 10, 1, PokemonType.Fighting),
                Mon("a2", 3, 10, 1, PokemonType.Fighting),
                Mon("a3", 3, 10, 1),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.IsTrue(a.All(m => m.CurrentStats.Attack == 5));
        }

        [Test]
        public void Flying_AddsOneSpeedPerTwoFlyingTypes_UpToTheCap()
        {
            var one = new List<BattleCombatant> { Mon("f1", 1, 10, 1, PokemonType.Flying) };
            TeamSynergy.Apply(State(one, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(1, one[0].CurrentStats.Speed, "one Flying-type isn't enough for a point of Speed");

            var two = new List<BattleCombatant>
            {
                Mon("f1", 1, 10, 1, PokemonType.Flying),
                Mon("f2", 1, 10, 3, PokemonType.Flying),
            };
            TeamSynergy.Apply(State(two, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(2, two[0].CurrentStats.Speed);
            Assert.AreEqual(TeamSynergy.FlyingSpeedCap, two[1].CurrentStats.Speed, "Tailwind never lifts a mon past the cap");
        }

        [Test]
        public void Bug_AddsOneSpeedPerThreeBugTypes_WithNoCap()
        {
            var a = new List<BattleCombatant>
            {
                Mon("b1", 1, 10, 3, PokemonType.Bug),
                Mon("b2", 1, 10, 1, PokemonType.Bug),
                Mon("b3", 1, 10, 1, PokemonType.Bug),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(4, a[0].CurrentStats.Speed);
            Assert.AreEqual(2, a[1].CurrentStats.Speed);
        }

        [Test]
        public void Rock_AddsAPercentOfHealthToTheLead_AtLeastOnePerRockType()
        {
            var big = new List<BattleCombatant> { Mon("lead", 1, 30, 1, PokemonType.Rock), Mon("support", 1, 30, 1) };
            TeamSynergy.Apply(State(big, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(33, big[0].CurrentStats.Health);
            Assert.AreEqual(33, big[0].CurrentHP);
            Assert.AreEqual(30, big[1].CurrentStats.Health, "Stone Guard is the Lead's alone");

            var small = new List<BattleCombatant> { Mon("lead", 1, 4, 1, PokemonType.Rock) };
            TeamSynergy.Apply(State(small, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(5, small[0].CurrentStats.Health, "10% of 4 rounds to nothing, so the floor applies");
        }

        [Test]
        public void Ghost_LeadPaysHp_AndEveryoneBehindItGainsAttackAndHealth()
        {
            var a = new List<BattleCombatant>
            {
                Mon("lead", 2, 10, 1, PokemonType.Ghost),
                Mon("support", 2, 10, 1),
                Mon("back", 2, 10, 1),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(8, a[0].CurrentHP);
            Assert.AreEqual(10, a[0].CurrentStats.Health, "the cost is HP, not max Health");
            Assert.AreEqual(2, a[0].CurrentStats.Attack);
            foreach (var mon in a.Skip(1))
            {
                Assert.AreEqual(3, mon.CurrentStats.Attack, mon.InstanceId);
                Assert.AreEqual(11, mon.CurrentHP, mon.InstanceId);
            }
        }

        [Test]
        public void Ghost_NeverTakesTheLeadBelowOne_AndDoesNothingForALoneLead()
        {
            var fragile = new List<BattleCombatant> { Mon("lead", 1, 2, 1, PokemonType.Ghost), Mon("support", 1, 5, 1, PokemonType.Ghost) };
            TeamSynergy.Apply(State(fragile, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(1, fragile[0].CurrentHP);

            var alone = new List<BattleCombatant> { Mon("lead", 1, 10, 1, PokemonType.Ghost) };
            TeamSynergy.Apply(State(alone, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(10, alone[0].CurrentHP, "nobody to give the HP to, so nothing is given");
        }

        [Test]
        public void Dragon_LowersEveryEnemysAttack_NeverBelowOne()
        {
            var a = new List<BattleCombatant> { Mon("d1", 1, 10, 1, PokemonType.Dragon), Mon("d2", 1, 10, 1, PokemonType.Dragon) };
            var b = new List<BattleCombatant> { Mon("strong", 5, 10, 1), Mon("weak", 2, 10, 1), Mon("none", 0, 10, 1) };

            TeamSynergy.Apply(State(a, b));

            Assert.AreEqual(3, b[0].CurrentStats.Attack);
            Assert.AreEqual(1, b[1].CurrentStats.Attack);
            Assert.AreEqual(0, b[2].CurrentStats.Attack, "a 0-Attack mon isn't lifted to 1");
            Assert.AreEqual(1, a[0].CurrentStats.Attack, "Intimidate doesn't touch its own side");
        }

        // --- Defenses ------------------------------------------------------------------------

        [Test]
        public void Water_ShieldsOneMonPerWaterType_FromTheFront_ScaledByCount()
        {
            var a = new List<BattleCombatant>
            {
                Mon("1", 1, 10, 1),
                Mon("2", 1, 10, 1, PokemonType.Water),
                Mon("3", 1, 10, 1, PokemonType.Water),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(2, a[0].Shield);
            Assert.AreEqual(2, a[1].Shield);
            Assert.AreEqual(0, a[2].Shield);
        }

        [Test]
        public void Steel_GivesTheLeadDamageReduction_PerSteelType()
        {
            var a = new List<BattleCombatant> { Mon("lead", 1, 10, 1, PokemonType.Steel), Mon("support", 1, 10, 1, PokemonType.Steel) };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(2, a[0].DamageReductionFlat);
            Assert.AreEqual(0, a[1].DamageReductionFlat);
        }

        [Test]
        public void Grass_GivesEveryMonLifesteal_PerGrassType()
        {
            var a = new List<BattleCombatant> { Mon("1", 1, 10, 1, PokemonType.Grass), Mon("2", 1, 10, 1, PokemonType.Grass) };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(0.2f, a[0].LifestealPercent, 0.0001f);
            Assert.AreEqual(0.2f, a[1].LifestealPercent, 0.0001f);
        }

        [Test]
        public void Fairy_WardsOneMonPerFairyType_FromTheBack()
        {
            var a = new List<BattleCombatant>
            {
                Mon("1", 1, 10, 1, PokemonType.Fairy),
                Mon("2", 1, 10, 1),
                Mon("3", 1, 10, 1),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(0, a[0].StatusWards);
            Assert.AreEqual(0, a[1].StatusWards);
            Assert.AreEqual(1, a[2].StatusWards);
        }

        [Test]
        public void StatusWard_BlocksOneStatusApplication_ThenIsSpent()
        {
            var burn = new PassiveDefinition
            {
                Id = "test-burn",
                Effects = { new EffectDefinition { Type = EffectType.ApplyStatus, Target = TargetSelector.EnemyLead, Amount = 1, Status = StatusType.Burned } }
            };
            var warded = Mon("warded", 0, 50, 0);
            warded.StatusWards = 1;
            var burner = Mon("burner", 0, 50, BattleConfig.ChargeThreshold);
            burner.ResolvedPassive = burn;
            var state = OneVsOne(warded, burner);
            var rng = new DeterministicRandom(1);

            var first = BattleSimulator.AdvanceStep(state, rng);
            Assert.IsTrue(first.Any(e => e.Kind == StepEventKind.StatusBlocked && e.TargetInstanceId == "warded"));
            Assert.IsNull(warded.Status);
            Assert.AreEqual(0, warded.StatusWards);

            BattleSimulator.AdvanceStep(state, rng);
            Assert.AreEqual(StatusType.Burned, warded.Status, "the ward only stops one application");
        }

        // --- Charge --------------------------------------------------------------------------

        [Test]
        public void Electric_ChargesTheLead_NeverPastTheThreshold()
        {
            var one = new List<BattleCombatant> { Mon("lead", 1, 10, 1, PokemonType.Electric), Mon("support", 1, 10, 1) };
            TeamSynergy.Apply(State(one, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(2, one[0].Charge);
            Assert.AreEqual(0, one[1].Charge);

            var two = new List<BattleCombatant> { Mon("lead", 1, 10, 1, PokemonType.Electric), Mon("support", 1, 10, 1, PokemonType.Electric) };
            TeamSynergy.Apply(State(two, new List<BattleCombatant> { Dummy() }));
            Assert.AreEqual(BattleConfig.ChargeThreshold, two[0].Charge);
        }

        [Test]
        public void Psychic_ChargesTheLeadAndSupport_PerPsychicType()
        {
            var a = new List<BattleCombatant>
            {
                Mon("lead", 1, 10, 1, PokemonType.Psychic),
                Mon("support", 1, 10, 1),
                Mon("back", 1, 10, 1),
            };

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Dummy() }));

            Assert.AreEqual(1, a[0].Charge);
            Assert.AreEqual(1, a[1].Charge);
            Assert.AreEqual(0, a[2].Charge, "a dormant mon gets no charge");
        }

        [Test]
        public void IceAndGround_PutTheEnemyInChargeDebt()
        {
            var a = new List<BattleCombatant> { Mon("ice", 1, 10, 1, PokemonType.Ice), Mon("ground", 1, 10, 1, PokemonType.Ground) };
            var b = new List<BattleCombatant> { Mon("lead", 1, 10, 1), Mon("support", 1, 10, 1), Mon("back", 1, 10, 1) };

            TeamSynergy.Apply(State(a, b));

            Assert.AreEqual(-3, b[0].Charge, "Ice's 1 plus Ground's 2");
            Assert.AreEqual(-1, b[1].Charge, "Ice only");
            Assert.AreEqual(0, b[2].Charge);
        }

        [Test]
        public void ChargeDebt_DelaysTheFirstTrigger()
        {
            var passive = new PassiveDefinition
            {
                Id = "test-heal",
                Effects = { new EffectDefinition { Type = EffectType.Heal, Target = TargetSelector.Self, Amount = 1 } }
            };
            var ice = Mon("ice", 0, 50, 1, PokemonType.Ice);
            var slowed = Mon("slowed", 0, 50, 1);
            slowed.ResolvedPassive = passive;
            var runner = new OnDemandStepRunner(new List<BattleCombatant> { ice }, new List<BattleCombatant> { slowed }, seed: 1);

            int firstTrigger = 0;
            for (int step = 1; step <= 5 && firstTrigger == 0; step++)
            {
                if (runner.NextStep().Any(e => e.Kind == StepEventKind.PassiveTriggered && e.SourceInstanceId == "slowed"))
                {
                    firstTrigger = step;
                }
            }
            Assert.AreEqual(4, firstTrigger, "a Speed-1 mon fires on Step 3 normally; one point of debt makes it 4");
        }

        // --- Openings ------------------------------------------------------------------------

        [Test]
        public void Fire_HitsTheEnemyLead_ThroughReductionAndShield()
        {
            var a = new List<BattleCombatant> { Mon("f1", 1, 10, 1, PokemonType.Fire), Mon("f2", 1, 10, 1, PokemonType.Fire), Mon("f3", 1, 10, 1, PokemonType.Fire) };
            var target = Mon("target", 1, 10, 1);
            target.Shield = 1;
            target.DamageReductionFlat = 1;

            var events = TeamSynergy.Apply(State(a, new List<BattleCombatant> { target }));

            Assert.AreEqual(9, target.CurrentHP, "3 Fire: 1 reduced, 1 absorbed, 1 through");
            Assert.AreEqual(0, target.Shield);
            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.Damage && e.TargetInstanceId == "target" && e.Amount == 1));
        }

        [Test]
        public void Fire_DoesNotLifesteal()
        {
            var a = new List<BattleCombatant> { Mon("lead", 1, 10, 1, PokemonType.Fire, PokemonType.Grass) };
            a[0].CurrentHP = 5;

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { Mon("target", 1, 10, 1) }));

            Assert.AreEqual(5, a[0].CurrentHP);
        }

        [Test]
        public void Dark_HitsTheEnemyLead_IgnoringReductionAndShield()
        {
            var a = new List<BattleCombatant> { Mon("d1", 1, 10, 1, PokemonType.Dark), Mon("d2", 1, 10, 1, PokemonType.Dark) };
            var target = Mon("target", 1, 10, 1);
            target.Shield = 5;
            target.DamageReductionFlat = 5;

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { target }));

            Assert.AreEqual(8, target.CurrentHP);
            Assert.AreEqual(5, target.Shield);
        }

        [Test]
        public void Poison_PoisonsTheEnemyLead_WithTickDamagePerPoisonType()
        {
            var a = new List<BattleCombatant> { Mon("p1", 1, 10, 1, PokemonType.Poison), Mon("p2", 1, 10, 1, PokemonType.Poison) };
            var target = Mon("target", 1, 10, 1);

            TeamSynergy.Apply(State(a, new List<BattleCombatant> { target }));

            Assert.AreEqual(StatusType.Poisoned, target.Status);
            Assert.AreEqual(2, target.StatusTickDamage);
        }

        [Test]
        public void Poison_IsBlockedByAFairyWard()
        {
            var a = new List<BattleCombatant> { Mon("poison", 1, 10, 1, PokemonType.Poison) };
            var b = new List<BattleCombatant> { Mon("fairy", 1, 10, 1, PokemonType.Fairy) };

            var events = TeamSynergy.Apply(State(a, b));

            Assert.IsNull(b[0].Status);
            Assert.IsTrue(events.Any(e => e.Kind == StepEventKind.StatusBlocked));
        }

        // --- In the runners ------------------------------------------------------------------

        [Test]
        public void AnOpeningKo_PromotesBeforeStepOne()
        {
            var a = new List<BattleCombatant> { Mon("dark", 1, 50, 1, PokemonType.Dark) };
            var b = new List<BattleCombatant> { Mon("frail", 5, 1, 1), Mon("next", 1, 50, 1) };
            var runner = new OnDemandStepRunner(a, b, seed: 1);

            var events = runner.NextStep();

            Assert.IsTrue(events.Any(e => e.Step == 0 && e.Kind == StepEventKind.Faint && e.SourceInstanceId == "frail"));
            Assert.AreEqual("next", runner.State.LeadB.InstanceId);
            Assert.AreEqual(49, a[0].CurrentHP, "the frail Lead fainted before it could swing; its replacement hit for 1");
        }

        [Test]
        public void AnOpeningThatEmptiesASide_EndsTheBattleWithoutAStep()
        {
            var a = new List<BattleCombatant> { Mon("dark", 1, 50, 1, PokemonType.Dark) };
            var b = new List<BattleCombatant> { Mon("frail", 5, 1, 1) };
            var runner = new OnDemandStepRunner(a, b, seed: 1);

            runner.NextStep();

            Assert.IsTrue(runner.IsBattleOver);
            Assert.AreEqual(BattleOutcome.SideAWins, runner.Outcome);
            Assert.AreEqual(0, runner.State.StepNumber);
            Assert.AreEqual(50, a[0].CurrentHP);
        }

        [Test]
        public void SynergiesApplyOnce_AndBothRunnersProduceTheSameLog()
        {
            List<BattleCombatant> BuildA() => new List<BattleCombatant>
            {
                Mon("a1", 3, 12, 1, PokemonType.Normal, PokemonType.Fire),
                Mon("a2", 2, 14, 2, PokemonType.Normal),
            };
            List<BattleCombatant> BuildB() => new List<BattleCombatant>
            {
                Mon("b1", 3, 12, 1, PokemonType.Water, PokemonType.Poison),
                Mon("b2", 2, 14, 1, PokemonType.Poison),
            };

            var log = PrecomputedStepLogRunner.Run(BuildA(), BuildB(), seed: 3);
            var onDemand = new OnDemandStepRunner(BuildA(), BuildB(), seed: 3);
            var stepped = new List<StepEvent>();
            while (!onDemand.IsBattleOver)
            {
                stepped.AddRange(onDemand.NextStep());
            }

            var precomputed = log.Events.Where(e => e.Kind != StepEventKind.BattleEnd).Select(e => e.ToString()).ToList();
            CollectionAssert.AreEqual(precomputed, stepped.Select(e => e.ToString()).ToList());
            Assert.AreEqual(2, stepped.Count(e => e.Kind == StepEventKind.TypeSynergy && e.SourceSide == Side.A), "Normal and Fire, announced once");
        }

        /// <summary>The opening can be taken by itself, which is the only board state a small shield
        /// ever exists in: one Water mon raises a point of Shield and the very exchange that follows
        /// spends it, so a screen that draws only after NextStep returns never sees one
        /// (BattleScreenController.PlayOpening).</summary>
        [Test]
        public void TheOpeningCanBeTakenOnItsOwn_AndTheStepAfterDoesNotRepeatIt()
        {
            var a = new List<BattleCombatant> { Mon("water", 1, 20, 1, PokemonType.Water) };
            var b = new List<BattleCombatant> { Mon("foe", 3, 20, 1) };
            var runner = new OnDemandStepRunner(a, b, seed: 1);

            var opening = runner.ApplyOpening();

            Assert.IsTrue(runner.OpeningApplied);
            Assert.AreEqual(1, a[0].Shield, "one Water mon shields the front of its own line-up");
            Assert.AreEqual(0, runner.State.StepNumber, "the opening is not a Step");
            Assert.IsTrue(opening.Any(e => e.Kind == StepEventKind.TypeSynergy), "and it says what it did");

            CollectionAssert.IsEmpty(runner.ApplyOpening(), "applying it again does nothing");

            runner.NextStep();

            Assert.AreEqual(1, runner.State.StepNumber);
            Assert.AreEqual(0, a[0].Shield, "the exchange spent the shield");
            Assert.AreEqual(18, a[0].CurrentHP, "which is what a shield is for: 3 damage, 1 absorbed");
        }

        /// <summary>Taking the opening separately changes nothing about the fight — same events in
        /// the same order, and the same result — so a screen can ask for it without the fight it
        /// draws being a different fight from the one a test or the server would run.</summary>
        [Test]
        public void TakingTheOpeningSeparately_PlaysOutTheSameFight()
        {
            List<BattleCombatant> BuildA() => new List<BattleCombatant>
            {
                Mon("a1", 3, 12, 1, PokemonType.Water, PokemonType.Fire),
                Mon("a2", 2, 14, 2, PokemonType.Water),
            };
            List<BattleCombatant> BuildB() => new List<BattleCombatant>
            {
                Mon("b1", 3, 12, 1, PokemonType.Steel),
                Mon("b2", 2, 14, 1, PokemonType.Dark),
            };

            var straight = new OnDemandStepRunner(BuildA(), BuildB(), seed: 5);
            var straightEvents = new List<StepEvent>();
            while (!straight.IsBattleOver)
            {
                straightEvents.AddRange(straight.NextStep());
            }

            var split = new OnDemandStepRunner(BuildA(), BuildB(), seed: 5);
            var splitEvents = new List<StepEvent>(split.ApplyOpening());
            while (!split.IsBattleOver)
            {
                splitEvents.AddRange(split.NextStep());
            }

            CollectionAssert.AreEqual(
                straightEvents.Select(e => e.ToString()).ToList(),
                splitEvents.Select(e => e.ToString()).ToList());
            Assert.AreEqual(straight.Outcome, split.Outcome);
        }

        // --- How they read on screen ---------------------------------------------------------

        [Test]
        public void EveryType_HasANameASummaryAndAnEffectAtEveryCount()
        {
            foreach (PokemonType type in System.Enum.GetValues(typeof(PokemonType)))
            {
                Assert.IsNotEmpty(TeamSynergy.DisplayName(type), $"{type} has no name");
                Assert.IsNotEmpty(TeamSynergy.Summary(type), $"{type} has no card summary");
                for (int count = 1; count <= 6; count++)
                {
                    Assert.IsNotEmpty(TeamSynergy.EffectAtCount(type, count), $"{type} x{count} has no effect text");
                }
            }
        }

        [Test]
        public void EffectAtCount_SpellsOutTheResolvedNumbers_NotThePerMonRule()
        {
            Assert.AreEqual("+2 Health to every mon", TeamSynergy.EffectAtCount(PokemonType.Normal, 2));
            Assert.AreEqual("+3 Attack to every mon", TeamSynergy.EffectAtCount(PokemonType.Fighting, 3));
            Assert.AreEqual("+20% lifesteal to every mon", TeamSynergy.EffectAtCount(PokemonType.Grass, 2));
            Assert.AreEqual("-4 starting charge on the foe's Lead", TeamSynergy.EffectAtCount(PokemonType.Ground, 2));
            StringAssert.Contains("2 true damage", TeamSynergy.EffectAtCount(PokemonType.Dark, 2));
        }

        /// <summary>The two that don't pay out at every count say what it would take, rather than
        /// claiming a bonus of zero.</summary>
        [Test]
        public void EffectAtCount_BelowASpeedThreshold_SaysWhatItWouldTake()
        {
            StringAssert.Contains($"at {TeamSynergy.FlyingTypesPerSpeed} Flying", TeamSynergy.EffectAtCount(PokemonType.Flying, 1));
            Assert.AreEqual("+1 Speed to every mon (max 3)", TeamSynergy.EffectAtCount(PokemonType.Flying, 2));
            StringAssert.Contains($"at {TeamSynergy.BugTypesPerSpeed} Bug", TeamSynergy.EffectAtCount(PokemonType.Bug, 2));
            Assert.AreEqual("+1 Speed to every mon", TeamSynergy.EffectAtCount(PokemonType.Bug, 3));
        }

        [Test]
        public void ActiveFor_ListsEverySynergyWithItsCount_AndNothingForATypelessLineUp()
        {
            var lineUp = new List<BattleCombatant>
            {
                Mon("1", 1, 10, 1, PokemonType.Water, PokemonType.Ground),
                Mon("2", 1, 10, 1, PokemonType.Water),
            };

            var active = TeamSynergy.ActiveFor(lineUp);

            Assert.AreEqual(2, active.Count);
            var water = active.Single(a => a.Type == PokemonType.Water);
            Assert.AreEqual(2, water.Count);
            Assert.AreEqual("Shell Guard x2", water.Title);
            Assert.AreEqual("+2 shield to the front 2", water.Effect);
            Assert.AreEqual(1, active.Single(a => a.Type == PokemonType.Ground).Count);

            Assert.IsEmpty(TeamSynergy.ActiveFor(new List<BattleCombatant> { Mon("plain", 1, 10, 1) }));
        }

        // --- Assembly (Meta) -----------------------------------------------------------------

        [Test]
        public void BattleLineUp_CopiesEachSpeciesTypesOntoItsCombatant()
        {
            var bulbasaur = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            bulbasaur.Id = 1;
            bulbasaur.Type1 = PokemonType.Grass;
            bulbasaur.HasSecondType = true;
            bulbasaur.Type2 = PokemonType.Poison;
            var charmander = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
            charmander.Id = 4;
            charmander.Type1 = PokemonType.Fire;
            charmander.Type2 = PokemonType.Water; // ignored: HasSecondType is false
            var library = ScriptableObject.CreateInstance<PokemonSpeciesLibrary>();
            library.AllSpecies.Add(bulbasaur);
            library.AllSpecies.Add(charmander);

            try
            {
                var lineUp = new List<PokemonInstance>
                {
                    new PokemonInstance { InstanceId = "b", SpeciesId = 1, CurrentStats = new Stats { Attack = 1, Health = 5, Speed = 1 }, CurrentHP = 5 },
                    new PokemonInstance { InstanceId = "c", SpeciesId = 4, CurrentStats = new Stats { Attack = 1, Health = 5, Speed = 1 }, CurrentHP = 5 },
                };

                var combatants = BattleLineUp.Assemble(lineUp, library);

                CollectionAssert.AreEqual(new[] { PokemonType.Grass, PokemonType.Poison }, combatants[0].Types);
                CollectionAssert.AreEqual(new[] { PokemonType.Fire }, combatants[1].Types);
                Assert.AreSame(lineUp[0], combatants[0].Source);
            }
            finally
            {
                Object.DestroyImmediate(library);
                Object.DestroyImmediate(bulbasaur);
                Object.DestroyImmediate(charmander);
            }
        }
    }
}
