using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Pure per-Step battle logic: AdvanceStep(BattleState) -> StepEvent[], mutating the
    /// given state in place. See battle-sim-spec.md §3 for the exact ordering this implements.
    /// Two thin runners (BattleRunner.cs) call this in a loop — neither duplicates Step logic.</summary>
    public static class BattleSimulator
    {
        public static List<StepEvent> AdvanceStep(BattleState state, DeterministicRandom rng)
        {
            state.StepNumber++;
            int step = state.StepNumber;
            var events = new List<StepEvent>();

            // Captured once at the top of the Step: promotion only happens in step 4 below, so
            // these references are stable for the whole Step even if a mon's HP drops to 0 in
            // step 1 or step 3 — battle-sim-spec.md §3 explicitly defers all faint handling to
            // one place ("since a passive can deal damage that causes a faint").
            var leadA = state.LeadA;
            var supportA = state.SupportA;
            var leadB = state.LeadB;
            var supportB = state.SupportB;

            // 1. Attack exchange — simultaneous, flat attack-vs-attack, gated only by both Leads
            //    existing. Speed plays no part here.
            if (leadA != null && leadB != null)
            {
                int dmgToB = leadA.CurrentStats.Attack;
                int dmgToA = leadB.CurrentStats.Attack;
                ApplyDamage(leadB, Side.B, dmgToB, leadA, Side.A, events, step);
                ApplyDamage(leadA, Side.A, dmgToA, leadB, Side.B, events, step);
            }

            // 2. Charge accumulation — all currently-active mons, regardless of HP dropped to 0
            //    in step 1 above (see note on the captured references).
            AccrueCharge(leadA);
            AccrueCharge(supportA);
            AccrueCharge(leadB);
            AccrueCharge(supportB);

            // 3. Passive resolution. The trigger set is fixed from this Step's charge values and
            //    doesn't grow mid-resolution even if an earlier passive speeds up a later mon's
            //    charge rate. Same-Step tie-break order (battle-sim-spec.md §6, provisional):
            //    attacking Leads before waiting Supports; ties within a role broken by Speed
            //    (higher first); further ties broken by Side A before Side B.
            var triggering = new List<Triggerer>();
            AddIfTriggering(leadA, Side.A, 0, triggering);
            AddIfTriggering(supportA, Side.A, 1, triggering);
            AddIfTriggering(leadB, Side.B, 0, triggering);
            AddIfTriggering(supportB, Side.B, 1, triggering);
            triggering.Sort(CompareTriggerOrder);

            foreach (var t in triggering)
            {
                events.Add(new StepEvent
                {
                    Step = step,
                    Kind = StepEventKind.PassiveTriggered,
                    SourceSide = t.Side,
                    SourceInstanceId = t.Instance.InstanceId
                });
                t.Instance.Charge = 0;
                if (t.Instance.ResolvedPassive != null)
                {
                    foreach (var effect in t.Instance.ResolvedPassive.Effects)
                    {
                        ApplyEffect(effect, t.Instance, t.Side, state, events, step);
                    }
                }
            }

            // 3.5. Status ticks — Poisoned/Burned deal flat damage at the end of every Step this
            //      mon is active (battle-sim-spec.md §5). This bypasses Shield/DamageReduction
            //      (it's self-inflicted DOT, not an attack) and never procs Lifesteal.
            ApplyStatusTick(leadA, Side.A, events, step);
            ApplyStatusTick(supportA, Side.A, events, step);
            ApplyStatusTick(leadB, Side.B, events, step);
            ApplyStatusTick(supportB, Side.B, events, step);

            // 4. Faint check & promotion — the only point removal/promotion happens, batching
            //    every faint this Step caused (attack exchange, passives, and status ticks alike).
            ResolveFaintsAndPromotions(state, Side.A, events, step);
            ResolveFaintsAndPromotions(state, Side.B, events, step);

            return events;
        }

        /// <summary>Battle-end condition per battle-sim-spec.md §9. Callers (BattleRunner) invoke
        /// this once a Step leaves a line-up empty, or a safety cap is hit.</summary>
        public static BattleOutcome DetermineOutcome(BattleState state)
        {
            bool aEmpty = state.LineUpA.Count == 0;
            bool bEmpty = state.LineUpB.Count == 0;
            if (aEmpty && bEmpty)
            {
                return BattleOutcome.Draw;
            }
            if (aEmpty)
            {
                return BattleOutcome.SideBWins;
            }
            if (bEmpty)
            {
                return BattleOutcome.SideAWins;
            }
            return BattleOutcome.Draw;
        }

        private readonly struct Triggerer
        {
            public readonly PokemonInstance Instance;
            public readonly Side Side;
            public readonly int Role; // 0 = Lead, 1 = Support

            public Triggerer(PokemonInstance instance, Side side, int role)
            {
                Instance = instance;
                Side = side;
                Role = role;
            }
        }

        private static void AddIfTriggering(PokemonInstance instance, Side side, int role, List<Triggerer> list)
        {
            if (instance != null && instance.Charge >= BattleConfig.ChargeThreshold)
            {
                list.Add(new Triggerer(instance, side, role));
            }
        }

        private static int CompareTriggerOrder(Triggerer x, Triggerer y)
        {
            int roleCompare = x.Role.CompareTo(y.Role);
            if (roleCompare != 0)
            {
                return roleCompare;
            }
            int speedCompare = y.Instance.CurrentStats.Speed.CompareTo(x.Instance.CurrentStats.Speed);
            if (speedCompare != 0)
            {
                return speedCompare;
            }
            return x.Side.CompareTo(y.Side);
        }

        private static void AccrueCharge(PokemonInstance instance)
        {
            if (instance == null)
            {
                return;
            }
            if (instance.Status == StatusType.Asleep)
            {
                return;
            }
            float multiplier = instance.ChargeRateMultiplier;
            if (instance.Status == StatusType.Paralyzed)
            {
                multiplier *= BattleConfig.ParalyzedChargeMultiplier;
            }
            instance.Charge += (int)(instance.CurrentStats.Speed * BattleConfig.DefaultStepDurationMs * multiplier);
        }

        private static void ApplyStatusTick(PokemonInstance instance, Side side, List<StepEvent> events, int step)
        {
            if (instance == null || instance.Status == null)
            {
                return;
            }

            if (instance.Status == StatusType.Poisoned)
            {
                int dmg = instance.StatusTickDamage * System.Math.Max(1, instance.PoisonStacks);
                instance.CurrentHP -= dmg;
                events.Add(new StepEvent
                {
                    Step = step,
                    Kind = StepEventKind.StatusTick,
                    SourceSide = side,
                    SourceInstanceId = instance.InstanceId,
                    TargetSide = side,
                    TargetInstanceId = instance.InstanceId,
                    Amount = dmg,
                    Status = StatusType.Poisoned
                });
                instance.PoisonStacks++;
            }
            else if (instance.Status == StatusType.Burned)
            {
                int dmg = instance.StatusTickDamage;
                instance.CurrentHP -= dmg;
                events.Add(new StepEvent
                {
                    Step = step,
                    Kind = StepEventKind.StatusTick,
                    SourceSide = side,
                    SourceInstanceId = instance.InstanceId,
                    TargetSide = side,
                    TargetInstanceId = instance.InstanceId,
                    Amount = dmg,
                    Status = StatusType.Burned
                });
            }
            // Paralyzed/Asleep have no per-Step tick damage.
        }

        private static void ResolveFaintsAndPromotions(BattleState state, Side side, List<StepEvent> events, int step)
        {
            var lineUp = state.LineUp(side);
            var oldLead = lineUp.Count > 0 ? lineUp[0] : null;
            var oldSupport = lineUp.Count > 1 ? lineUp[1] : null;

            if (oldSupport != null && oldSupport.CurrentHP <= 0)
            {
                lineUp.Remove(oldSupport);
                events.Add(new StepEvent { Step = step, Kind = StepEventKind.Faint, SourceSide = side, SourceInstanceId = oldSupport.InstanceId });
            }
            if (oldLead != null && oldLead.CurrentHP <= 0)
            {
                lineUp.Remove(oldLead);
                events.Add(new StepEvent { Step = step, Kind = StepEventKind.Faint, SourceSide = side, SourceInstanceId = oldLead.InstanceId });
            }

            var newLead = lineUp.Count > 0 ? lineUp[0] : null;
            var newSupport = lineUp.Count > 1 ? lineUp[1] : null;

            if (newLead != null && newLead != oldLead)
            {
                events.Add(new StepEvent { Step = step, Kind = StepEventKind.Promotion, SourceSide = side, SourceInstanceId = newLead.InstanceId });
            }
            if (newSupport != null && newSupport != oldSupport)
            {
                events.Add(new StepEvent { Step = step, Kind = StepEventKind.Promotion, SourceSide = side, SourceInstanceId = newSupport.InstanceId });
            }
        }

        private static void ApplyEffect(EffectDefinition effect, PokemonInstance self, Side selfSide, BattleState state, List<StepEvent> events, int step)
        {
            var (target, targetSide) = ResolveTarget(effect.Target, self, selfSide, state);
            if (target == null)
            {
                return;
            }

            switch (effect.Type)
            {
                case EffectType.DealDamage:
                    ApplyDamage(target, targetSide, effect.Amount, self, selfSide, events, step);
                    break;

                case EffectType.Heal:
                    ApplyHeal(target, targetSide, effect.Amount, self, selfSide, events, step);
                    break;

                case EffectType.Shield:
                    target.Shield += effect.Amount;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.Shield, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;

                case EffectType.ApplyStatus:
                    ApplyStatus(target, targetSide, effect.Status, effect.Amount, self, selfSide, events, step);
                    break;

                case EffectType.ClearStatus:
                    ClearStatus(target, targetSide, self, selfSide, events, step);
                    break;

                case EffectType.BuffAttack:
                    target.CurrentStats.Attack += effect.Amount;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.BuffAttack, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;

                case EffectType.BuffSpeed:
                    target.CurrentStats.Speed += effect.Amount;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.BuffSpeed, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;

                case EffectType.ModifyChargeRate:
                    // Amount is a percentage delta (e.g. 50 => x1.5, -50 => x0.5), floored at 0.
                    target.ChargeRateMultiplier = System.Math.Max(0f, target.ChargeRateMultiplier + effect.Amount / 100f);
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.ChargeRateModified, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;

                case EffectType.DamageReduction:
                    target.DamageReductionFlat += effect.Amount;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.DamageReductionApplied, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;

                case EffectType.Lifesteal:
                    // Amount is a percentage (e.g. 30 => 0.3 of HP damage dealt heals back).
                    target.LifestealPercent += effect.Amount / 100f;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.Lifesteal, SourceSide = selfSide, SourceInstanceId = self.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = effect.Amount });
                    break;
            }
        }

        private static (PokemonInstance target, Side side) ResolveTarget(TargetSelector selector, PokemonInstance self, Side selfSide, BattleState state)
        {
            switch (selector)
            {
                case TargetSelector.Self:
                    return IsValidTarget(self) ? (self, selfSide) : (null, selfSide);

                case TargetSelector.Ally:
                    var ally = selfSide == Side.A
                        ? (self == state.LeadA ? state.SupportA : state.LeadA)
                        : (self == state.LeadB ? state.SupportB : state.LeadB);
                    return IsValidTarget(ally) ? (ally, selfSide) : (null, selfSide);

                case TargetSelector.EnemyLead:
                    var enemySideLead = selfSide == Side.A ? Side.B : Side.A;
                    var enemyLead = selfSide == Side.A ? state.LeadB : state.LeadA;
                    return IsValidTarget(enemyLead) ? (enemyLead, enemySideLead) : (null, enemySideLead);

                case TargetSelector.EnemySupport:
                    var enemySideSupport = selfSide == Side.A ? Side.B : Side.A;
                    var enemySupport = selfSide == Side.A ? state.SupportB : state.SupportA;
                    return IsValidTarget(enemySupport) ? (enemySupport, enemySideSupport) : (null, enemySideSupport);

                default:
                    return (null, selfSide);
            }
        }

        private static bool IsValidTarget(PokemonInstance instance)
        {
            return instance != null && instance.CurrentHP > 0;
        }

        private static void ApplyDamage(PokemonInstance target, Side targetSide, int rawAmount, PokemonInstance attacker, Side attackerSide, List<StepEvent> events, int step)
        {
            int afterReduction = System.Math.Max(0, rawAmount - target.DamageReductionFlat);
            int shieldAbsorbed = System.Math.Min(target.Shield, afterReduction);
            target.Shield -= shieldAbsorbed;
            int hpDamage = afterReduction - shieldAbsorbed;
            target.CurrentHP -= hpDamage;

            events.Add(new StepEvent { Step = step, Kind = StepEventKind.Damage, SourceSide = attackerSide, SourceInstanceId = attacker.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = hpDamage });

            if (shieldAbsorbed > 0)
            {
                events.Add(new StepEvent { Step = step, Kind = StepEventKind.ShieldAbsorbed, SourceSide = targetSide, SourceInstanceId = target.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = shieldAbsorbed });
            }

            if (attacker.LifestealPercent > 0f && hpDamage > 0)
            {
                int healAmount = (int)System.Math.Round(hpDamage * attacker.LifestealPercent, System.MidpointRounding.AwayFromZero);
                int actualHeal = System.Math.Min(healAmount, attacker.CurrentStats.Health - attacker.CurrentHP);
                if (actualHeal > 0)
                {
                    attacker.CurrentHP += actualHeal;
                    events.Add(new StepEvent { Step = step, Kind = StepEventKind.LifestealHeal, SourceSide = attackerSide, SourceInstanceId = attacker.InstanceId, TargetSide = attackerSide, TargetInstanceId = attacker.InstanceId, Amount = actualHeal });
                }
            }
        }

        private static void ApplyHeal(PokemonInstance target, Side targetSide, int amount, PokemonInstance source, Side sourceSide, List<StepEvent> events, int step)
        {
            int healAmount = System.Math.Min(amount, target.CurrentStats.Health - target.CurrentHP);
            if (healAmount <= 0)
            {
                return;
            }
            target.CurrentHP += healAmount;
            events.Add(new StepEvent { Step = step, Kind = StepEventKind.Heal, SourceSide = sourceSide, SourceInstanceId = source.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = healAmount });
        }

        private static void ApplyStatus(PokemonInstance target, Side targetSide, StatusType status, int amount, PokemonInstance source, Side sourceSide, List<StepEvent> events, int step)
        {
            target.Status = status;
            target.StatusTickDamage = status == StatusType.Poisoned || status == StatusType.Burned ? amount : 0;
            target.PoisonStacks = status == StatusType.Poisoned ? 1 : 0;

            events.Add(new StepEvent { Step = step, Kind = StepEventKind.StatusApplied, SourceSide = sourceSide, SourceInstanceId = source.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Status = status });
        }

        private static void ClearStatus(PokemonInstance target, Side targetSide, PokemonInstance source, Side sourceSide, List<StepEvent> events, int step)
        {
            if (target.Status == null)
            {
                return;
            }
            var cleared = target.Status.Value;
            target.Status = null;
            target.StatusTickDamage = 0;
            target.PoisonStacks = 0;

            events.Add(new StepEvent { Step = step, Kind = StepEventKind.StatusCleared, SourceSide = sourceSide, SourceInstanceId = source.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Status = cleared });
        }
    }
}
