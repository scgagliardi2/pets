using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>
    /// Pure, deterministic (teamA, teamB, seed) -> BattleLog resolver. See battle-sim-spec.md for
    /// the exact rules this implements.
    /// </summary>
    public static class BattleSimulator
    {
        private const int RoundCap = 50;
        private const int EventCap = 10000;

        private sealed class EventCapExceededException : System.Exception
        {
        }

        private sealed class BattleContext
        {
            public BattleLog Log;
            public DeterministicRandom Rng;
            public int SummonCounter;
        }

        public static BattleLog Run(TeamState teamA, TeamState teamB, int seed)
        {
            var ctx = new BattleContext { Log = new BattleLog(), Rng = new DeterministicRandom(seed) };

            try
            {
                FireBattleStartForTeam(teamA, Team.A, teamB, ctx);
                FireBattleStartForTeam(teamB, Team.B, teamA, ctx);

                int round = 0;
                while (!teamA.IsEmpty && !teamB.IsEmpty)
                {
                    round++;
                    if (round > RoundCap)
                    {
                        return EndBattle(ctx, BattleOutcome.Draw);
                    }

                    RunAttackExchange(teamA, teamB, ctx);
                }

                var outcome = teamA.IsEmpty && teamB.IsEmpty ? BattleOutcome.Draw
                    : teamA.IsEmpty ? BattleOutcome.TeamBWins
                    : BattleOutcome.TeamAWins;

                return EndBattle(ctx, outcome);
            }
            catch (EventCapExceededException)
            {
                return EndBattle(ctx, BattleOutcome.Draw);
            }
        }

        private static BattleLog EndBattle(BattleContext ctx, BattleOutcome outcome)
        {
            ctx.Log.Outcome = outcome;
            ctx.Log.Events.Add(new BattleEvent { Kind = BattleEventKind.BattleEnd, Outcome = outcome });
            return ctx.Log;
        }

        private static void RunAttackExchange(TeamState teamA, TeamState teamB, BattleContext ctx)
        {
            var frontA = teamA.Front;
            var frontB = teamB.Front;
            int dmgToB = frontA.Attack;
            int dmgToA = frontB.Attack;

            // Both damage applications happen before either side's OnHurt/faint — see spec §6.3.
            frontA.Health -= dmgToA;
            frontB.Health -= dmgToB;
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Damage, SourceTeam = Team.A, SourceInstanceId = frontA.InstanceId, TargetTeam = Team.B, TargetInstanceId = frontB.InstanceId, Amount = dmgToB });
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Damage, SourceTeam = Team.B, SourceInstanceId = frontB.InstanceId, TargetTeam = Team.A, TargetInstanceId = frontA.InstanceId, Amount = dmgToA });

            if (dmgToA > 0)
            {
                FireTrigger(frontA, TriggerType.OnHurt, Team.A, teamA, teamB, ctx);
            }
            if (dmgToB > 0)
            {
                FireTrigger(frontB, TriggerType.OnHurt, Team.B, teamB, teamA, ctx);
            }

            if (frontA.Health <= 0 && teamA.Slots.Contains(frontA))
            {
                ResolveFaint(frontA, Team.A, teamA, teamB, ctx);
            }
            if (frontB.Health <= 0 && teamB.Slots.Contains(frontB))
            {
                ResolveFaint(frontB, Team.B, teamB, teamA, ctx);
            }
        }

        private static void FireBattleStartForTeam(TeamState team, Team teamId, TeamState enemyTeam, BattleContext ctx)
        {
            foreach (var creature in new List<CreatureState>(team.Slots))
            {
                if (!creature.IsAlive)
                {
                    continue;
                }
                FireTrigger(creature, TriggerType.OnBattleStart, teamId, team, enemyTeam, ctx);
            }
        }

        private static void FireTrigger(CreatureState creature, TriggerType trigger, Team teamId, TeamState team, TeamState enemyTeam, BattleContext ctx)
        {
            foreach (var ability in creature.AbilitiesWithTrigger(trigger))
            {
                Log(ctx, new BattleEvent { Kind = BattleEventKind.AbilityTriggered, SourceTeam = teamId, SourceInstanceId = creature.InstanceId, Trigger = trigger });
                ApplyEffects(ability.Effects, creature, teamId, team, enemyTeam, ctx);
            }
        }

        private static void ApplyEffects(List<EffectData> effects, CreatureState source, Team sourceTeamId, TeamState sourceTeam, TeamState enemyTeam, BattleContext ctx)
        {
            foreach (var effect in effects)
            {
                if (effect.Type == EffectType.Summon)
                {
                    ApplySummon(effect, sourceTeamId, sourceTeam, ctx);
                    continue;
                }

                var (target, targetTeamId, targetTeam, opposingTeam) = ResolveTarget(effect.Target, source, sourceTeamId, sourceTeam, enemyTeam, ctx.Rng);
                if (target == null)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case EffectType.DealDamage:
                        ApplyDamage(target, targetTeamId, targetTeam, opposingTeam, effect.Amount, sourceTeamId, source.InstanceId, ctx);
                        break;
                    case EffectType.Heal:
                        ApplyHeal(target, targetTeamId, effect.Amount, sourceTeamId, source.InstanceId, ctx);
                        break;
                    case EffectType.BuffAttack:
                        target.Attack += effect.Amount;
                        Log(ctx, new BattleEvent { Kind = BattleEventKind.BuffAttack, SourceTeam = sourceTeamId, SourceInstanceId = source.InstanceId, TargetTeam = targetTeamId, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                        break;
                    case EffectType.BuffHealth:
                        target.Health += effect.Amount;
                        target.MaxHealth += effect.Amount;
                        Log(ctx, new BattleEvent { Kind = BattleEventKind.BuffHealth, SourceTeam = sourceTeamId, SourceInstanceId = source.InstanceId, TargetTeam = targetTeamId, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                        break;
                }
            }
        }

        private static void ApplyDamage(CreatureState target, Team targetTeamId, TeamState targetTeam, TeamState opposingTeam, int amount, Team sourceTeamId, string sourceInstanceId, BattleContext ctx)
        {
            target.Health -= amount;
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Damage, SourceTeam = sourceTeamId, SourceInstanceId = sourceInstanceId, TargetTeam = targetTeamId, TargetInstanceId = target.InstanceId, Amount = amount });

            if (amount > 0)
            {
                FireTrigger(target, TriggerType.OnHurt, targetTeamId, targetTeam, opposingTeam, ctx);
            }

            if (target.Health <= 0 && targetTeam.Slots.Contains(target))
            {
                ResolveFaint(target, targetTeamId, targetTeam, opposingTeam, ctx);
            }
        }

        private static void ApplyHeal(CreatureState target, Team targetTeamId, int amount, Team sourceTeamId, string sourceInstanceId, BattleContext ctx)
        {
            int healAmount = System.Math.Min(amount, target.MaxHealth - target.Health);
            if (healAmount <= 0)
            {
                return;
            }
            target.Health += healAmount;
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Heal, SourceTeam = sourceTeamId, SourceInstanceId = sourceInstanceId, TargetTeam = targetTeamId, TargetInstanceId = target.InstanceId, Amount = healAmount });
        }

        private static void ResolveFaint(CreatureState creature, Team teamId, TeamState team, TeamState enemyTeam, BattleContext ctx)
        {
            team.Slots.Remove(creature);
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Faint, SourceTeam = teamId, SourceInstanceId = creature.InstanceId });
            FireTrigger(creature, TriggerType.OnFaint, teamId, team, enemyTeam, ctx);
        }

        private static void ApplySummon(EffectData effect, Team teamId, TeamState team, BattleContext ctx)
        {
            if (effect.SummonTemplate == null)
            {
                return;
            }

            var summoned = new CreatureState
            {
                InstanceId = $"{effect.SummonTemplate.Id}#summon{ctx.SummonCounter++}",
                TemplateId = effect.SummonTemplate.Id,
                DisplayName = effect.SummonTemplate.DisplayName,
                Attack = effect.SummonTemplate.Attack,
                Health = effect.SummonTemplate.Health,
                MaxHealth = effect.SummonTemplate.Health,
                Level = 1,
            };
            team.InsertFront(summoned);
            Log(ctx, new BattleEvent { Kind = BattleEventKind.Summon, SourceTeam = teamId, SourceInstanceId = summoned.InstanceId, TargetTeam = teamId, TargetInstanceId = summoned.InstanceId });
        }

        private static (CreatureState creature, Team teamId, TeamState team, TeamState opposing) ResolveTarget(
            TargetSelector selector, CreatureState source, Team sourceTeamId, TeamState sourceTeam, TeamState enemyTeam, DeterministicRandom rng)
        {
            switch (selector)
            {
                case TargetSelector.Self:
                    return (source, sourceTeamId, sourceTeam, enemyTeam);
                case TargetSelector.FrontEnemy:
                    var front = enemyTeam.Front;
                    return (front != null && front.IsAlive ? front : null, Opposite(sourceTeamId), enemyTeam, sourceTeam);
                case TargetSelector.RandomAlly:
                    return (PickRandomOther(sourceTeam, source, rng), sourceTeamId, sourceTeam, enemyTeam);
                case TargetSelector.RandomEnemy:
                    return (PickRandomOther(enemyTeam, null, rng), Opposite(sourceTeamId), enemyTeam, sourceTeam);
                default:
                    return (null, sourceTeamId, sourceTeam, enemyTeam);
            }
        }

        private static Team Opposite(Team team)
        {
            return team == Team.A ? Team.B : Team.A;
        }

        private static CreatureState PickRandomOther(TeamState team, CreatureState exclude, DeterministicRandom rng)
        {
            var candidates = new List<CreatureState>();
            foreach (var creature in team.Slots)
            {
                if (creature != exclude && creature.IsAlive)
                {
                    candidates.Add(creature);
                }
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            return candidates[rng.NextInt(candidates.Count)];
        }

        private static void Log(BattleContext ctx, BattleEvent evt)
        {
            if (ctx.Log.Events.Count >= EventCap)
            {
                throw new EventCapExceededException();
            }
            ctx.Log.Events.Add(evt);
        }
    }
}
