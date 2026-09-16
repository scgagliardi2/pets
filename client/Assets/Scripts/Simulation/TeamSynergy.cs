using System;
using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>Team type synergies (design doc §11, battle-sim-spec.md §8, ADR 0015): for every type
    /// present in a side's line-up, a small bonus that scales with how many of that side's mons carry
    /// it. Applied once, when a battle opens, by both runners — never inside AdvanceStep.
    ///
    /// **A mon counts toward each of its types**, so a Water/Ground mon adds one to both. The count is
    /// the whole line-up, dormant mons included — that's the team the player built — and it's taken
    /// once: a mon that faints or is caught doesn't switch its side's synergy off mid-fight.
    ///
    /// **Deliberately small.** Stats are single digits (Pets.Data.SpeciesTier) and Speed runs 1–3
    /// against a charge threshold of three, so a synergy is worth roughly one EXP point per mon of the
    /// type, and Speed — where +1 can double how often a passive fires — is only granted per two or
    /// three mons. Every number is a constant below; tune them there.
    ///
    /// Combatants with no <see cref="BattleCombatant.Types"/> contribute nothing, so a line-up built
    /// straight from PokemonInstances (the golden fixtures, the dev random battle) plays exactly as it
    /// did before synergies existed.</summary>
    public static class TeamSynergy
    {
        /// <summary>Normal — Steady Growth: +Health to every mon, per Normal-type.</summary>
        public const int NormalHealthPerType = 1;

        /// <summary>Fire — Ember Burst: opening damage to the enemy Lead, per Fire-type. Goes through
        /// DamageReduction and Shield like an attack.</summary>
        public const int FireDamagePerType = 1;

        /// <summary>Water — Shell Guard: this many mons from the front, per Water-type, each start with
        /// this much Shield per Water-type.</summary>
        public const int WaterShieldPerType = 1;

        /// <summary>Electric — Static Shock: starting charge on the Lead, per Electric-type (never past
        /// the threshold, so at most one early trigger).</summary>
        public const int ElectricLeadChargePerType = 2;

        /// <summary>Grass — Vine Drain: Lifesteal percent for every mon, per Grass-type (still capped at
        /// BattleConfig.MaxLifestealPercent).</summary>
        public const int GrassLifestealPercentPerType = 10;

        /// <summary>Ice — Permafrost: charge taken off the enemy Lead and Support, per Ice-type. Charge
        /// can go below zero, which is what delays their first trigger.
        ///
        /// A starting deficit rather than a charge-rate percentage: accrual is an int of Speed times the
        /// multiplier, so at Speed 1 any slowdown at all truncates to no charge whatsoever.</summary>
        public const int IceChargePenaltyPerType = 1;

        /// <summary>Fighting — Power Surge: +Attack to every mon, per Fighting-type.</summary>
        public const int FightingAttackPerType = 1;

        /// <summary>Poison — Poison Sting: the enemy Lead opens Poisoned, with this tick damage per
        /// Poison-type (and poison's usual stacking from there).</summary>
        public const int PoisonTickPerType = 1;

        /// <summary>Ground — Sand Tomb: charge taken off the enemy Lead alone, per Ground-type — narrower
        /// than Ice, and deeper.</summary>
        public const int GroundLeadChargePenaltyPerType = 2;

        /// <summary>Flying — Tailwind: +1 Speed to every mon per this many Flying-types, never taking a
        /// mon past <see cref="FlyingSpeedCap"/>.</summary>
        public const int FlyingTypesPerSpeed = 2;
        public const int FlyingSpeedCap = 3;

        /// <summary>Psychic — Quick Focus: starting charge on the Lead and Support, per Psychic-type.</summary>
        public const int PsychicChargePerType = 1;

        /// <summary>Bug — Swarm Scurry: +1 Speed to every mon per this many Bug-types, uncapped.</summary>
        public const int BugTypesPerSpeed = 3;

        /// <summary>Rock — Stone Guard: the Lead gains this percent of its Health, per Rock-type — and
        /// always at least one point per Rock-type, since 10% of a tier-1 mon rounds to nothing.</summary>
        public const int RockLeadHealthPercentPerType = 10;

        /// <summary>Ghost — Curse: the Lead gives up this much HP per Ghost-type (never below 1), and
        /// every other mon gains <see cref="GhostTeamBoostPerType"/> Attack and Health per Ghost-type.
        /// Skipped when the Lead has nobody to give it to.</summary>
        public const int GhostLeadHpCostPerType = 2;
        public const int GhostTeamBoostPerType = 1;

        /// <summary>Dragon — Intimidate: Attack taken off every enemy mon, per Dragon-type, never below 1.</summary>
        public const int DragonEnemyAttackPerType = 1;

        /// <summary>Dark — Night Ambush: opening true damage to the enemy Lead, per Dark-type. Ignores
        /// Shield and DamageReduction.</summary>
        public const int DarkDamagePerType = 1;

        /// <summary>Steel — Iron Hide: DamageReduction on the Lead, per Steel-type.</summary>
        public const int SteelLeadReductionPerType = 1;

        /// <summary>Fairy — Moonlight Glow: this many mons from the back, per Fairy-type, each block the
        /// next status applied to them.</summary>
        public const int FairyWardedMonsPerType = 1;

        private static readonly int TypeCount = Enum.GetValues(typeof(PokemonType)).Length;

        /// <summary>How many mons in <paramref name="lineUp"/> carry <paramref name="type"/>.</summary>
        public static int CountOf(IReadOnlyList<BattleCombatant> lineUp, PokemonType type) => Count(lineUp)[(int)type];

        /// <summary>One synergy a side actually has, and how many mons are behind it.</summary>
        public readonly struct Active
        {
            public readonly PokemonType Type;
            public readonly int Count;

            public Active(PokemonType type, int count)
            {
                Type = type;
                Count = count;
            }

            /// <summary>"Ember Burst x2".</summary>
            public string Title => $"{DisplayName(Type)} x{Count}";

            /// <summary>What it is worth at this count, in full: "2 damage to the foe's Lead".</summary>
            public string Effect => EffectAtCount(Type, Count);
        }

        /// <summary>Every synergy a line-up has, in type order — what a screen lists. Empty for a
        /// line-up whose combatants carry no types.</summary>
        public static List<Active> ActiveFor(IReadOnlyList<BattleCombatant> lineUp)
        {
            var counts = Count(lineUp);
            var active = new List<Active>();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    active.Add(new Active((PokemonType)i, counts[i]));
                }
            }
            return active;
        }

        /// <summary>The synergy's name, from the type-passive table: Normal is Steady Growth, Fire
        /// is Ember Burst, and so on.</summary>
        public static string DisplayName(PokemonType type)
        {
            switch (type)
            {
                case PokemonType.Normal: return "Steady Growth";
                case PokemonType.Fire: return "Ember Burst";
                case PokemonType.Water: return "Shell Guard";
                case PokemonType.Electric: return "Static Shock";
                case PokemonType.Grass: return "Vine Drain";
                case PokemonType.Ice: return "Permafrost";
                case PokemonType.Fighting: return "Power Surge";
                case PokemonType.Poison: return "Poison Sting";
                case PokemonType.Ground: return "Sand Tomb";
                case PokemonType.Flying: return "Tailwind";
                case PokemonType.Psychic: return "Quick Focus";
                case PokemonType.Bug: return "Swarm Scurry";
                case PokemonType.Rock: return "Stone Guard";
                case PokemonType.Ghost: return "Curse";
                case PokemonType.Dragon: return "Intimidate";
                case PokemonType.Dark: return "Night Ambush";
                case PokemonType.Steel: return "Iron Hide";
                default: return "Moonlight Glow";
            }
        }

        /// <summary>Exactly what this synergy does to this side at <paramref name="count"/> mons of
        /// the type — the resolved numbers, not the per-mon rule, so a screen can show a player what
        /// their own team is getting. The single source of those numbers is the constants above, so
        /// this can't drift from what Apply does.</summary>
        public static string EffectAtCount(PokemonType type, int count)
        {
            switch (type)
            {
                case PokemonType.Normal:
                    return $"+{count * NormalHealthPerType} Health to every mon";
                case PokemonType.Fire:
                    return $"{count * FireDamagePerType} damage to the foe's Lead as the fight opens";
                case PokemonType.Water:
                    return $"+{count * WaterShieldPerType} shield to the front {count}";
                case PokemonType.Electric:
                    return $"+{count * ElectricLeadChargePerType} starting charge on the Lead (max {BattleConfig.ChargeThreshold})";
                case PokemonType.Grass:
                    return $"+{count * GrassLifestealPercentPerType}% lifesteal to every mon";
                case PokemonType.Ice:
                    return $"-{count * IceChargePenaltyPerType} starting charge on the foe's Lead and Support";
                case PokemonType.Fighting:
                    return $"+{count * FightingAttackPerType} Attack to every mon";
                case PokemonType.Poison:
                    return $"the foe's Lead opens Poisoned, {count * PoisonTickPerType} a tick";
                case PokemonType.Ground:
                    return $"-{count * GroundLeadChargePenaltyPerType} starting charge on the foe's Lead";
                case PokemonType.Flying:
                    return count / FlyingTypesPerSpeed > 0
                        ? $"+{count / FlyingTypesPerSpeed} Speed to every mon (max {FlyingSpeedCap})"
                        : $"+1 Speed to every mon at {FlyingTypesPerSpeed} Flying";
                case PokemonType.Psychic:
                    return $"+{count * PsychicChargePerType} starting charge on the Lead and Support";
                case PokemonType.Bug:
                    return count / BugTypesPerSpeed > 0
                        ? $"+{count / BugTypesPerSpeed} Speed to every mon"
                        : $"+1 Speed to every mon at {BugTypesPerSpeed} Bug";
                case PokemonType.Rock:
                    return $"+{count * RockLeadHealthPercentPerType}% Health on the Lead";
                case PokemonType.Ghost:
                    return $"Lead pays {count * GhostLeadHpCostPerType} HP; the rest +{count * GhostTeamBoostPerType} Attack and Health";
                case PokemonType.Dragon:
                    return $"-{count * DragonEnemyAttackPerType} Attack to every foe";
                case PokemonType.Dark:
                    return $"{count * DarkDamagePerType} true damage to the foe's Lead as the fight opens";
                case PokemonType.Steel:
                    return $"the Lead blocks {count * SteelLeadReductionPerType} damage a hit";
                default:
                    return $"the back {count * FairyWardedMonsPerType} shrug off one status each";
            }
        }

        /// <summary>The same rule in a few words, per mon of the type — what fits on a Pokemon card,
        /// where there is no team to count yet.</summary>
        public static string Summary(PokemonType type)
        {
            switch (type)
            {
                case PokemonType.Normal: return "+1 HP team";
                case PokemonType.Fire: return "1 dmg foe";
                case PokemonType.Water: return "1 shield";
                case PokemonType.Electric: return "+2 charge";
                case PokemonType.Grass: return "+10% drain";
                case PokemonType.Ice: return "slows foe";
                case PokemonType.Fighting: return "+1 ATK team";
                case PokemonType.Poison: return "poisons foe";
                case PokemonType.Ground: return "slows foe Lead";
                case PokemonType.Flying: return "+1 SPD /2";
                case PokemonType.Psychic: return "+1 charge x2";
                case PokemonType.Bug: return "+1 SPD /3";
                case PokemonType.Rock: return "+10% HP Lead";
                case PokemonType.Ghost: return "HP for team";
                case PokemonType.Dragon: return "-1 foe ATK";
                case PokemonType.Dark: return "1 true dmg";
                case PokemonType.Steel: return "blocks 1";
                default: return "blocks status";
            }
        }

        /// <summary>Applies both sides' synergies to a battle that hasn't started, and returns what
        /// happened, stamped Step 0. Order is fixed so the result is deterministic and readable: each
        /// side's own stat bonuses, then Intimidate on the other side, then defenses, then charge,
        /// then the opening blows — and a faint check, so an opening that KOs a Lead promotes the next
        /// mon before Step 1's exchange rather than leaving a fainted Lead to swing.</summary>
        public static List<StepEvent> Apply(BattleState state)
        {
            var events = new List<StepEvent>();
            var countsA = Count(state.LineUpA);
            var countsB = Count(state.LineUpB);

            AnnounceSynergies(state.LineUpA, Side.A, countsA, events);
            AnnounceSynergies(state.LineUpB, Side.B, countsB, events);

            ApplyOwnStats(state.LineUpA, countsA);
            ApplyOwnStats(state.LineUpB, countsB);
            ApplyIntimidate(state.LineUpB, countsA[(int)PokemonType.Dragon]);
            ApplyIntimidate(state.LineUpA, countsB[(int)PokemonType.Dragon]);

            ApplyDefenses(state.LineUpA, countsA);
            ApplyDefenses(state.LineUpB, countsB);

            ApplyOwnCharge(state.LineUpA, countsA);
            ApplyOwnCharge(state.LineUpB, countsB);
            ApplyEnemyCharge(state.LineUpB, countsA);
            ApplyEnemyCharge(state.LineUpA, countsB);

            ApplyOpening(state, Side.A, countsA, events);
            ApplyOpening(state, Side.B, countsB, events);

            BattleSimulator.ResolveFaintsAndPromotions(state, Side.A, events, 0);
            BattleSimulator.ResolveFaintsAndPromotions(state, Side.B, events, 0);
            return events;
        }

        private static int[] Count(IReadOnlyList<BattleCombatant> lineUp)
        {
            var counts = new int[TypeCount];
            foreach (var mon in lineUp)
            {
                if (mon.Types == null)
                {
                    continue;
                }
                // A mon listing the same type twice still counts once toward it.
                var seen = new HashSet<PokemonType>();
                foreach (var type in mon.Types)
                {
                    if (seen.Add(type))
                    {
                        counts[(int)type]++;
                    }
                }
            }
            return counts;
        }

        private static void AnnounceSynergies(List<BattleCombatant> lineUp, Side side, int[] counts, List<StepEvent> events)
        {
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    events.Add(new StepEvent
                    {
                        Step = 0,
                        Kind = StepEventKind.TypeSynergy,
                        SourceSide = side,
                        SourceInstanceId = lineUp.Count > 0 ? lineUp[0].InstanceId : null,
                        Amount = counts[i],
                        SynergyType = (PokemonType)i
                    });
                }
            }
        }

        private static void ApplyOwnStats(List<BattleCombatant> lineUp, int[] counts)
        {
            if (lineUp.Count == 0)
            {
                return;
            }

            int normal = counts[(int)PokemonType.Normal];
            int fighting = counts[(int)PokemonType.Fighting];
            int flyingSpeed = counts[(int)PokemonType.Flying] / FlyingTypesPerSpeed;
            int bugSpeed = counts[(int)PokemonType.Bug] / BugTypesPerSpeed;
            foreach (var mon in lineUp)
            {
                AddHealth(mon, normal * NormalHealthPerType);
                mon.CurrentStats.Attack += fighting * FightingAttackPerType;
                if (flyingSpeed > 0)
                {
                    // Raises toward the cap, but never lowers a mon already past it.
                    mon.CurrentStats.Speed = Math.Max(mon.CurrentStats.Speed,
                        Math.Min(FlyingSpeedCap, mon.CurrentStats.Speed + flyingSpeed));
                }
                mon.CurrentStats.Speed += bugSpeed;
            }

            int rock = counts[(int)PokemonType.Rock];
            if (rock > 0)
            {
                var lead = lineUp[0];
                int percentBonus = (lead.CurrentStats.Health * RockLeadHealthPercentPerType * rock + 50) / 100;
                AddHealth(lead, Math.Max(rock, percentBonus));
            }

            int ghost = counts[(int)PokemonType.Ghost];
            if (ghost > 0 && lineUp.Count > 1)
            {
                var lead = lineUp[0];
                lead.CurrentHP = Math.Max(1, lead.CurrentHP - ghost * GhostLeadHpCostPerType);
                for (int i = 1; i < lineUp.Count; i++)
                {
                    lineUp[i].CurrentStats.Attack += ghost * GhostTeamBoostPerType;
                    AddHealth(lineUp[i], ghost * GhostTeamBoostPerType);
                }
            }
        }

        private static void ApplyIntimidate(List<BattleCombatant> enemies, int dragon)
        {
            if (dragon <= 0)
            {
                return;
            }
            foreach (var mon in enemies)
            {
                int attack = mon.CurrentStats.Attack;
                // Floored at 1, but a 0-Attack mon isn't lifted to 1 by being intimidated.
                mon.CurrentStats.Attack = Math.Max(Math.Min(attack, 1), attack - dragon * DragonEnemyAttackPerType);
            }
        }

        private static void ApplyDefenses(List<BattleCombatant> lineUp, int[] counts)
        {
            if (lineUp.Count == 0)
            {
                return;
            }

            int water = counts[(int)PokemonType.Water];
            for (int i = 0; i < Math.Min(lineUp.Count, water * WaterShieldPerType); i++)
            {
                lineUp[i].Shield += water * WaterShieldPerType;
            }

            lineUp[0].DamageReductionFlat += counts[(int)PokemonType.Steel] * SteelLeadReductionPerType;

            int grass = counts[(int)PokemonType.Grass];
            if (grass > 0)
            {
                foreach (var mon in lineUp)
                {
                    mon.LifestealPercent = Math.Min(BattleConfig.MaxLifestealPercent,
                        mon.LifestealPercent + grass * GrassLifestealPercentPerType / 100f);
                }
            }

            int warded = counts[(int)PokemonType.Fairy] * FairyWardedMonsPerType;
            for (int i = lineUp.Count - 1; i >= 0 && i >= lineUp.Count - warded; i--)
            {
                lineUp[i].StatusWards++;
            }
        }

        private static void ApplyOwnCharge(List<BattleCombatant> lineUp, int[] counts)
        {
            if (lineUp.Count == 0)
            {
                return;
            }
            AddCharge(lineUp[0], counts[(int)PokemonType.Electric] * ElectricLeadChargePerType);
            int psychic = counts[(int)PokemonType.Psychic] * PsychicChargePerType;
            AddCharge(lineUp[0], psychic);
            if (lineUp.Count > 1)
            {
                AddCharge(lineUp[1], psychic);
            }
        }

        private static void ApplyEnemyCharge(List<BattleCombatant> enemies, int[] counts)
        {
            if (enemies.Count == 0)
            {
                return;
            }
            int ice = counts[(int)PokemonType.Ice] * IceChargePenaltyPerType;
            enemies[0].Charge -= ice + counts[(int)PokemonType.Ground] * GroundLeadChargePenaltyPerType;
            if (enemies.Count > 1)
            {
                enemies[1].Charge -= ice;
            }
        }

        private static void ApplyOpening(BattleState state, Side side, int[] counts, List<StepEvent> events)
        {
            var own = state.LineUp(side);
            var enemySide = side == Side.A ? Side.B : Side.A;
            var enemies = state.LineUp(enemySide);
            if (own.Count == 0 || enemies.Count == 0)
            {
                return;
            }
            var source = own[0];
            var enemyLead = enemies[0];

            int fire = counts[(int)PokemonType.Fire] * FireDamagePerType;
            if (fire > 0 && enemyLead.IsAlive)
            {
                // An attack in all but name: reduction and shield blunt it. Never Lifesteal, though —
                // the opening isn't the Lead's blow.
                var (hpDamage, absorbed) = BattleSimulator.TakeHit(enemyLead, fire);
                AddDamageEvents(events, side, source, enemySide, enemyLead, hpDamage, absorbed);
            }

            int dark = counts[(int)PokemonType.Dark] * DarkDamagePerType;
            if (dark > 0 && enemyLead.IsAlive)
            {
                enemyLead.CurrentHP -= dark;
                AddDamageEvents(events, side, source, enemySide, enemyLead, dark, 0);
            }

            int poison = counts[(int)PokemonType.Poison] * PoisonTickPerType;
            if (poison > 0 && enemyLead.IsAlive)
            {
                BattleSimulator.ApplyStatus(enemyLead, enemySide, StatusType.Poisoned, poison, source, side, events, 0);
            }
        }

        private static void AddDamageEvents(List<StepEvent> events, Side side, BattleCombatant source, Side targetSide, BattleCombatant target, int hpDamage, int absorbed)
        {
            events.Add(new StepEvent { Step = 0, Kind = StepEventKind.Damage, SourceSide = side, SourceInstanceId = source.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = hpDamage });
            if (absorbed > 0)
            {
                events.Add(new StepEvent { Step = 0, Kind = StepEventKind.ShieldAbsorbed, SourceSide = targetSide, SourceInstanceId = target.InstanceId, TargetSide = targetSide, TargetInstanceId = target.InstanceId, Amount = absorbed });
            }
        }

        private static void AddHealth(BattleCombatant mon, int amount)
        {
            mon.CurrentStats.Health += amount;
            mon.CurrentHP += amount;
        }

        private static void AddCharge(BattleCombatant mon, int amount)
        {
            if (amount > 0)
            {
                mon.Charge = Math.Min(BattleConfig.ChargeThreshold, mon.Charge + amount);
            }
        }
    }
}
