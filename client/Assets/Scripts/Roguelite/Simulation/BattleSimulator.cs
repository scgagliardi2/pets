using System;
using System.Collections.Generic;

namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// Pure per-Step battle logic. See docs/battle-sim-spec.md §3 for the exact loop this
    /// implements: attack exchange, charge accrual, passive resolution, status ticks, faint
    /// check/promotion, in that order. No MonoBehaviour/UnityEngine dependency (noEngineReferences
    /// in the asmdef enforces this at compile time).
    /// </summary>
    public static class BattleSimulator
    {
        /// <summary>
        /// Advances the battle by exactly one Step and returns everything that happened. Callers
        /// (the two runners in BattleRunners.cs) are responsible for deciding when to stop
        /// calling this — the simulator itself has no concept of "the battle is over," it just
        /// reflects whatever BattleState it's given.
        /// </summary>
        public static List<StepEvent> AdvanceStep(BattleState state)
        {
            state.StepNumber++;
            var events = new List<StepEvent>();

            var leadA = state.SideA.Lead;
            var leadB = state.SideB.Lead;

            // 1. Attack exchange — simultaneous, Speed plays no part (docs/battle-sim-spec.md §3.1).
            if (leadA != null && leadA.IsAlive && leadB != null && leadB.IsAlive)
            {
                int rawToB = leadA.Attack;
                int rawToA = leadB.Attack;
                ApplyDamage(leadA, SideId.A, leadB, SideId.B, rawToB, events);
                ApplyDamage(leadB, SideId.B, leadA, SideId.A, rawToA, events);
            }

            // 2. Charge accrual for all currently-active mons (up to 4).
            AccrueCharge(leadA);
            AccrueCharge(state.SideA.Support);
            AccrueCharge(leadB);
            AccrueCharge(state.SideB.Support);

            // 3. Passive resolution — ordering per docs/battle-sim-spec.md §6 (open question,
            //    default: attacking Leads before waiting Supports, ties by Speed, then side A
            //    before B).
            ResolveTriggeredPassives(state, events);

            // 4. Status ticks (Poison/Burn) — end of Step, before the faint check.
            ApplyStatusTick(state.SideA.Lead, SideId.A, events);
            ApplyStatusTick(state.SideA.Support, SideId.A, events);
            ApplyStatusTick(state.SideB.Lead, SideId.B, events);
            ApplyStatusTick(state.SideB.Support, SideId.B, events);

            // 5. Faint check & promotion.
            state.SideA.PromoteAndRemoveFainted(events, SideId.A);
            state.SideB.PromoteAndRemoveFainted(events, SideId.B);

            foreach (var evt in events)
            {
                evt.StepNumber = state.StepNumber;
            }
            return events;
        }

        private static void AccrueCharge(BattleMon mon)
        {
            if (mon == null || !mon.IsAlive)
            {
                return;
            }

            float statusMultiplier = 1f;
            if (mon.Status == StatusType.Paralyzed)
            {
                statusMultiplier = BattleConfig.ParalysisChargeMultiplier;
            }
            else if (mon.Status == StatusType.Asleep)
            {
                statusMultiplier = BattleConfig.AsleepChargeMultiplier;
            }

            float rateMultiplier = Math.Max(0f, 1f + mon.ChargeRateModifierPercent / 100f);
            mon.Charge += mon.Speed * BattleConfig.StepDurationSeconds * statusMultiplier * rateMultiplier;
        }

        private struct TriggerCandidate
        {
            public BattleMon Mon;
            public SideId Side;
            public BattleSide OwnSide;
            public BattleSide OpposingSide;
            public bool IsLead;
        }

        private static void ResolveTriggeredPassives(BattleState state, List<StepEvent> events)
        {
            var candidates = new List<TriggerCandidate>();
            AddCandidateIfCharged(candidates, state.SideA.Lead, SideId.A, state.SideA, state.SideB, isLead: true);
            AddCandidateIfCharged(candidates, state.SideB.Lead, SideId.B, state.SideB, state.SideA, isLead: true);
            AddCandidateIfCharged(candidates, state.SideA.Support, SideId.A, state.SideA, state.SideB, isLead: false);
            AddCandidateIfCharged(candidates, state.SideB.Support, SideId.B, state.SideB, state.SideA, isLead: false);

            // Attacking Leads before waiting Supports; ties by Speed (desc); final tiebreak by side.
            candidates.Sort((x, y) =>
            {
                if (x.IsLead != y.IsLead)
                {
                    return x.IsLead ? -1 : 1;
                }
                if (x.Mon.Speed != y.Mon.Speed)
                {
                    return y.Mon.Speed - x.Mon.Speed;
                }
                return x.Side.CompareTo(y.Side);
            });

            foreach (var candidate in candidates)
            {
                // Re-check: an earlier passive this Step may have fainted or already retriggered this mon.
                if (!candidate.Mon.IsAlive || candidate.Mon.Charge < BattleConfig.ChargeThreshold || candidate.Mon.Passive == null)
                {
                    continue;
                }

                candidate.Mon.Charge = 0f;
                events.Add(new StepEvent
                {
                    Kind = StepEventKind.PassiveTriggered,
                    SourceSide = candidate.Side,
                    SourceName = candidate.Mon.DisplayName,
                    PassiveName = candidate.Mon.Passive.DisplayName,
                });

                foreach (var effect in candidate.Mon.Passive.Effects)
                {
                    ApplyEffect(effect, candidate.Mon, candidate.Side, candidate.OwnSide, candidate.OpposingSide, events);
                }
            }
        }

        private static void AddCandidateIfCharged(List<TriggerCandidate> candidates, BattleMon mon, SideId side, BattleSide ownSide, BattleSide opposingSide, bool isLead)
        {
            if (mon != null && mon.IsAlive && mon.Charge >= BattleConfig.ChargeThreshold)
            {
                candidates.Add(new TriggerCandidate { Mon = mon, Side = side, OwnSide = ownSide, OpposingSide = opposingSide, IsLead = isLead });
            }
        }

        private static void ApplyEffect(EffectDefinition effect, BattleMon source, SideId sourceSide, BattleSide ownSide, BattleSide opposingSide, List<StepEvent> events)
        {
            var (target, targetSide) = ResolveTarget(effect.Target, source, sourceSide, ownSide, opposingSide);
            if (target == null)
            {
                return;
            }

            switch (effect.Type)
            {
                case EffectType.DealDamage:
                    ApplyDamage(source, sourceSide, target, targetSide, effect.Amount, events);
                    break;

                case EffectType.Heal:
                    {
                        int healAmount = Math.Min(effect.Amount, target.MaxHP - target.CurrentHP);
                        if (healAmount > 0)
                        {
                            target.CurrentHP += healAmount;
                            events.Add(new StepEvent { Kind = StepEventKind.Heal, TargetSide = targetSide, TargetName = target.DisplayName, Amount = healAmount });
                        }
                        break;
                    }

                case EffectType.Shield:
                    target.ShieldAmount += effect.Amount;
                    events.Add(new StepEvent { Kind = StepEventKind.Shield, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;

                case EffectType.ApplyStatus:
                    if (effect.Status.HasValue)
                    {
                        target.Status = effect.Status.Value;
                        events.Add(new StepEvent { Kind = StepEventKind.StatusApplied, TargetSide = targetSide, TargetName = target.DisplayName, Status = effect.Status });
                    }
                    break;

                case EffectType.ClearStatus:
                    target.Status = null;
                    events.Add(new StepEvent { Kind = StepEventKind.StatusCleared, TargetSide = targetSide, TargetName = target.DisplayName });
                    break;

                case EffectType.BuffAttack:
                    target.Attack += effect.Amount;
                    events.Add(new StepEvent { Kind = StepEventKind.BuffAttack, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;

                case EffectType.BuffSpeed:
                    target.Speed += effect.Amount;
                    events.Add(new StepEvent { Kind = StepEventKind.BuffSpeed, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;

                case EffectType.DamageReduction:
                    target.DamageReductionFlat += effect.Amount;
                    events.Add(new StepEvent { Kind = StepEventKind.DamageReductionApplied, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;

                case EffectType.Lifesteal:
                    target.LifestealPercent = Math.Max(0, Math.Min(100, target.LifestealPercent + effect.Amount));
                    events.Add(new StepEvent { Kind = StepEventKind.LifestealModified, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;

                case EffectType.ModifyChargeRate:
                    target.ChargeRateModifierPercent += effect.Amount;
                    events.Add(new StepEvent { Kind = StepEventKind.ChargeRateModified, TargetSide = targetSide, TargetName = target.DisplayName, Amount = effect.Amount });
                    break;
            }
        }

        private static (BattleMon target, SideId targetSide) ResolveTarget(TargetSelector selector, BattleMon source, SideId sourceSide, BattleSide ownSide, BattleSide opposingSide)
        {
            switch (selector)
            {
                case TargetSelector.Self:
                    return (source, sourceSide);

                case TargetSelector.Ally:
                    {
                        var ally = ownSide.GetAllyOf(source);
                        return (ally != null && ally.IsAlive) ? (ally, sourceSide) : (null, sourceSide);
                    }

                case TargetSelector.EnemyLead:
                    {
                        var lead = opposingSide.Lead;
                        var enemySide = Opposite(sourceSide);
                        return (lead != null && lead.IsAlive) ? (lead, enemySide) : (null, enemySide);
                    }

                case TargetSelector.EnemySupport:
                    {
                        var support = opposingSide.Support;
                        var enemySide = Opposite(sourceSide);
                        return (support != null && support.IsAlive) ? (support, enemySide) : (null, enemySide);
                    }

                default:
                    return (null, sourceSide);
            }
        }

        private static SideId Opposite(SideId side) => side == SideId.A ? SideId.B : SideId.A;

        /// <summary>
        /// Single damage-resolution pipeline used for both the basic attack exchange and any
        /// passive's DealDamage effect: damage reduction, then shield absorption, then HP, then
        /// (if the attacker has lifesteal) a heal back to the attacker. See docs/content-schema.md
        /// §4's note that Lifesteal must apply to attack-exchange damage too, not just passives.
        /// </summary>
        private static void ApplyDamage(BattleMon attacker, SideId attackerSideId, BattleMon target, SideId targetSideId, int rawAmount, List<StepEvent> events)
        {
            if (target == null || !target.IsAlive || rawAmount <= 0)
            {
                return;
            }

            int afterReduction = Math.Max(0, rawAmount - target.DamageReductionFlat);
            int shieldAbsorb = Math.Min(target.ShieldAmount, afterReduction);
            target.ShieldAmount -= shieldAbsorb;
            int hpDamage = afterReduction - shieldAbsorb;
            target.CurrentHP -= hpDamage;

            events.Add(new StepEvent
            {
                Kind = StepEventKind.Damage,
                SourceSide = attackerSideId,
                SourceName = attacker.DisplayName,
                TargetSide = targetSideId,
                TargetName = target.DisplayName,
                Amount = hpDamage,
            });

            if (hpDamage > 0 && attacker.LifestealPercent > 0)
            {
                int healAmount = Math.Min(hpDamage * attacker.LifestealPercent / 100, attacker.MaxHP - attacker.CurrentHP);
                if (healAmount > 0)
                {
                    attacker.CurrentHP += healAmount;
                    events.Add(new StepEvent
                    {
                        Kind = StepEventKind.LifestealHeal,
                        SourceSide = attackerSideId,
                        SourceName = attacker.DisplayName,
                        Amount = healAmount,
                    });
                }
            }
        }

        private static void ApplyStatusTick(BattleMon mon, SideId side, List<StepEvent> events)
        {
            if (mon == null || !mon.IsAlive || mon.Status == null)
            {
                return;
            }
            if (mon.Status != StatusType.Poisoned && mon.Status != StatusType.Burned)
            {
                return;
            }

            int tick = Math.Min(BattleConfig.StatusTickDamage, mon.CurrentHP);
            mon.CurrentHP -= tick;
            events.Add(new StepEvent
            {
                Kind = StepEventKind.StatusTick,
                TargetSide = side,
                TargetName = mon.DisplayName,
                Amount = tick,
                Status = mon.Status,
            });
        }
    }
}
