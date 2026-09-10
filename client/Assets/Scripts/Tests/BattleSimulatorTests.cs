using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Simulation;

namespace Pets.Tests
{
    public class BattleSimulatorTests
    {
        private static CreatureState Creature(string id, int attack, int health, params AbilityData[] abilities)
        {
            return new CreatureState
            {
                InstanceId = id,
                TemplateId = id,
                DisplayName = id,
                Attack = attack,
                Health = health,
                MaxHealth = health,
                Level = 1,
                Abilities = new List<AbilityData>(abilities),
            };
        }

        private static AbilityData Ability(TriggerType trigger, params EffectData[] effects)
        {
            return new AbilityData { Trigger = trigger, Effects = new List<EffectData>(effects) };
        }

        private static EffectData DealDamage(TargetSelector target, int amount) =>
            new EffectData { Type = EffectType.DealDamage, Target = target, Amount = amount };

        private static EffectData Heal(TargetSelector target, int amount) =>
            new EffectData { Type = EffectType.Heal, Target = target, Amount = amount };

        private static EffectData BuffAttack(TargetSelector target, int amount) =>
            new EffectData { Type = EffectType.BuffAttack, Target = target, Amount = amount };

        private static EffectData BuffHealth(TargetSelector target, int amount) =>
            new EffectData { Type = EffectType.BuffHealth, Target = target, Amount = amount };

        private static EffectData Summon(CreatureTemplate template) =>
            new EffectData { Type = EffectType.Summon, SummonTemplate = template };

        private static TeamState Team(params CreatureState[] creatures) =>
            new TeamState { Slots = new List<CreatureState>(creatures) };

        [Test]
        public void AttackExchange_NoAbilities_StrongerAttackerWins()
        {
            var teamA = Team(Creature("a1", attack: 3, health: 10));
            var teamB = Team(Creature("b1", attack: 2, health: 10));

            var log = BattleSimulator.Run(teamA, teamB, seed: 1);

            // b1 dies at round 4 (10 - 4*3 <= 0) while a1 still has 2 hp (10 - 4*2).
            Assert.AreEqual(BattleOutcome.TeamAWins, log.Outcome);
        }

        [Test]
        public void EmptyTeam_OpponentWinsImmediately()
        {
            var teamA = Team();
            var teamB = Team(Creature("b1", attack: 1, health: 1));

            var log = BattleSimulator.Run(teamA, teamB, seed: 1);

            Assert.AreEqual(BattleOutcome.TeamBWins, log.Outcome);
        }

        [Test]
        public void BothTeamsEmpty_IsADraw()
        {
            var log = BattleSimulator.Run(Team(), Team(), seed: 1);

            Assert.AreEqual(BattleOutcome.Draw, log.Outcome);
        }

        [Test]
        public void OnFaint_DamagesRemainingEnemyAfterFainting()
        {
            var faintee = Creature("faintee", attack: 1, health: 1,
                Ability(TriggerType.OnFaint, DealDamage(TargetSelector.RandomEnemy, 3)));
            var target = Creature("target", attack: 1, health: 10);

            var teamA = Team(faintee);
            var teamB = Team(target);

            var log = BattleSimulator.Run(teamA, teamB, seed: 1);

            // Round 1: both deal 1 dmg. faintee (1 hp) dies, its OnFaint deals 3 more to target.
            // target ends at 10 - 1 (exchange) - 3 (OnFaint) = 6, and teamA has nothing left.
            Assert.AreEqual(BattleOutcome.TeamBWins, log.Outcome);
            Assert.AreEqual(6, target.Health);
        }

        [Test]
        public void OnBattleStart_BuffAttackAppliesToRandomAlly()
        {
            var buffer = Creature("buffer", attack: 1, health: 5,
                Ability(TriggerType.OnBattleStart, BuffAttack(TargetSelector.RandomAlly, 5)));
            var buddy = Creature("buddy", attack: 1, health: 5);
            var wall = Creature("wall", attack: 0, health: 1000);

            BattleSimulator.Run(Team(buffer, buddy), Team(wall), seed: 1);

            // Only one other ally exists, so RandomAlly is deterministic regardless of seed.
            Assert.AreEqual(6, buddy.Attack);
        }

        [Test]
        public void OnBattleStart_DealDamageCanChainIntoTargetsOnFaint()
        {
            var striker = Creature("striker", attack: 0, health: 5,
                Ability(TriggerType.OnBattleStart, DealDamage(TargetSelector.FrontEnemy, 5)));
            var fragile = Creature("fragile", attack: 0, health: 1,
                Ability(TriggerType.OnFaint, DealDamage(TargetSelector.RandomEnemy, 2)));

            var teamA = Team(striker);
            var teamB = Team(fragile);

            var log = BattleSimulator.Run(teamA, teamB, seed: 1);

            // striker's OnBattleStart kills fragile before round 1; fragile's OnFaint then hits
            // striker for 2 (striker: 5 -> 3). fragile was Team B's only creature, so Team B is
            // now empty before the round loop ever runs — Team A wins immediately.
            Assert.AreEqual(BattleOutcome.TeamAWins, log.Outcome);
            Assert.AreEqual(3, striker.Health);
        }

        [Test]
        public void FrontEnemy_SkipsAFaintedButNotYetRemovedTarget()
        {
            // b1's OnFaint targets FrontEnemy. If team A's simultaneously-fainting front (a1)
            // hasn't been removed/resolved yet, FrontEnemy must treat it as invalid rather than
            // hitting a corpse.
            var a1 = Creature("a1", attack: 5, health: 5);
            var b1 = Creature("b1", attack: 5, health: 5,
                Ability(TriggerType.OnFaint, DealDamage(TargetSelector.FrontEnemy, 99)));
            var a2 = Creature("a2", attack: 1, health: 10);

            var teamA = Team(a1, a2);
            var teamB = Team(b1);

            // Team A's faint (and thus removal) resolves before Team B's per spec §6.3, so by
            // the time b1's OnFaint runs, a2 is already the front and takes the hit.
            BattleSimulator.Run(teamA, teamB, seed: 1);

            Assert.AreEqual(10 - 99, a2.Health);
        }

        [Test]
        public void SimultaneousDoubleFaint_TeamAOnFaintResolvesBeforeTeamB()
        {
            var summonTemplate = new CreatureTemplate { Id = "token", DisplayName = "Token", Attack = 1, Health = 1 };
            var a1 = Creature("a1", attack: 5, health: 5,
                Ability(TriggerType.OnFaint, Summon(summonTemplate)));
            var b1 = Creature("b1", attack: 5, health: 5,
                Ability(TriggerType.OnFaint, DealDamage(TargetSelector.FrontEnemy, 1)));

            var log = BattleSimulator.Run(Team(a1), Team(b1), seed: 1);

            // If A's OnFaint (the summon) resolves before B's OnFaint, the summoned token is
            // already team A's front by the time b1's FrontEnemy effect fires, so the token takes
            // the hit and faints (1 hp - 1 dmg). If the order were reversed, FrontEnemy would see
            // a1 (still at 0 hp, not yet removed) and skip per the invalid-target rule, and no
            // faint event for the token would ever appear.
            var tokenFainted = log.Events.Any(e => e.Kind == BattleEventKind.Faint && e.SourceInstanceId.StartsWith("token#summon"));
            Assert.IsTrue(tokenFainted, "expected the summoned token to take damage and faint, proving Team A's OnFaint resolved first");
        }

        [Test]
        public void SummonOnFaint_RefillsFrontSlotAndKeepsFighting()
        {
            var summonTemplate = new CreatureTemplate { Id = "guard", DisplayName = "Guard", Attack = 1, Health = 100 };
            var warrior = Creature("warrior", attack: 10, health: 1,
                Ability(TriggerType.OnFaint, Summon(summonTemplate)));
            var brick = Creature("brick", attack: 1, health: 1000);

            var teamA = Team(warrior);
            var log = BattleSimulator.Run(teamA, Team(brick), seed: 1);

            Assert.IsTrue(log.Events.Any(e => e.Kind == BattleEventKind.Summon));
            Assert.AreEqual(1, teamA.Slots.Count);
            Assert.AreEqual("guard", teamA.Slots[0].TemplateId);
            Assert.IsTrue(teamA.Slots[0].IsAlive);
        }

        [Test]
        public void OnHurt_HealCannotExceedMaxHealth()
        {
            var healer = Creature("healer", attack: 1, health: 5,
                Ability(TriggerType.OnHurt, Heal(TargetSelector.Self, 10)));
            var poker = Creature("poker", attack: 1, health: 1000);

            var log = BattleSimulator.Run(Team(healer), Team(poker), seed: 1);

            // Each round: poker deals 1 (healer 5->4), healer's OnHurt heals 10 but caps at
            // MaxHealth (5). Healer never dies; poker has too much health to die within the round
            // cap, so this is a draw — and the cap must have held across every round.
            Assert.AreEqual(BattleOutcome.Draw, log.Outcome);
            Assert.AreEqual(5, healer.Health);
        }

        [Test]
        public void BuffHealth_IncreasesCurrentAndMaxHealth()
        {
            var buffed = Creature("buffed", attack: 0, health: 5,
                Ability(TriggerType.OnBattleStart, BuffHealth(TargetSelector.Self, 3)));
            var wall = Creature("wall", attack: 0, health: 1000);

            BattleSimulator.Run(Team(buffed), Team(wall), seed: 1);

            Assert.AreEqual(8, buffed.Health);
            Assert.AreEqual(8, buffed.MaxHealth);
        }

        [Test]
        public void ZeroAttackStalemate_HitsRoundCapAndDraws()
        {
            var teamA = Team(Creature("immovableA", attack: 0, health: 5));
            var teamB = Team(Creature("immovableB", attack: 0, health: 5));

            var log = BattleSimulator.Run(teamA, teamB, seed: 1);

            Assert.AreEqual(BattleOutcome.Draw, log.Outcome);
        }

        [Test]
        public void FullBoard_FiveVsFive_ResolvesWithoutError()
        {
            var teamA = Team(
                Creature("a1", 2, 3), Creature("a2", 1, 4), Creature("a3", 3, 2),
                Creature("a4", 1, 1), Creature("a5", 2, 2));
            var teamB = Team(
                Creature("b1", 2, 2), Creature("b2", 1, 3), Creature("b3", 2, 4),
                Creature("b4", 3, 1), Creature("b5", 1, 2));

            var log = BattleSimulator.Run(teamA, teamB, seed: 42);

            Assert.IsTrue(log.Outcome == BattleOutcome.TeamAWins
                || log.Outcome == BattleOutcome.TeamBWins
                || log.Outcome == BattleOutcome.Draw);
            Assert.IsTrue(log.Events.Count > 0);
        }
    }
}
