using System.Collections.Generic;

namespace Pets.Simulation
{
    /// <summary>One Pokémon's state *inside a single battle*. The simulator operates on these and
    /// never on <see cref="PokemonInstance"/>, which is the persistent, run-level record.
    ///
    /// The split exists because these two things have different lifetimes and the simulator
    /// mutates its subject in place. When both were one type, running a battle wrote shields,
    /// poison stacks, stat buffs and damage permanently onto the player's roster objects: buffs
    /// leaked into the next fight, a "precomputed Step log for playback" couldn't be played back
    /// because the state was already at the end of the fight by the time the animation started,
    /// and the same line-up couldn't be simulated twice — which a fight preview, and Phase 3's
    /// server-side re-verification of a PvP result, both need to do.
    ///
    /// So a battle now starts by copying: <see cref="FromLineUp"/> builds a fresh set of
    /// combatants, and nothing the simulator does is visible outside the battle. Writing a result
    /// back to the run (damage carried between nodes, EXP, a caught mon joining the Box) is the
    /// caller's decision, made explicitly against <see cref="Source"/> afterwards — not a side
    /// effect of having simulated.</summary>
    public sealed class BattleCombatant
    {
        /// <summary>Copied from the source instance, and what every StepEvent identifies a
        /// combatant by, so a caller can match an event back to the mon it came from.</summary>
        public string InstanceId;

        /// <summary>The persistent record this was built from. The simulator never reads or writes
        /// through this — it's here so the layers around a battle (the catch interaction adding a
        /// defeated enemy to the Box, an EXP award, carrying damage back to the run) can get from
        /// a combatant to the mon it represents without keeping a side table. Null is fine: a
        /// combatant built directly in a test or a fixture has no run-level counterpart.</summary>
        public PokemonInstance Source;

        /// <summary>Leveled base stats plus synergy/item modifiers, folded in at line-up assembly
        /// (battle-sim-spec.md §8). BuffAttack/BuffSpeed effects mutate this for the remainder of
        /// the battle — which is exactly why it's a copy.</summary>
        public Stats CurrentStats;

        public int CurrentHP;
        public StatusType? Status;

        /// <summary>The passive behavior for this combatant, already scaled by evolution stage
        /// (content-schema.md §3). Baked in by the content layer before battle assembly; the
        /// simulator never resolves a passive id itself. Null means no passive.</summary>
        public PassiveDefinition ResolvedPassive;

        /// <summary>Fills toward BattleConfig.ChargeThreshold at speed * stepDuration per Step
        /// (battle-sim-spec.md §3-§4).</summary>
        public int Charge;

        /// <summary>Temporary absorb pool from a Shield effect; consumed by incoming damage before
        /// HP, for the remainder of the battle.</summary>
        public int Shield;

        /// <summary>Flat reduction applied to incoming damage for the remainder of the battle.</summary>
        public int DamageReductionFlat;

        /// <summary>Multiplicative modifier on charge accrual for the remainder of the battle
        /// (e.g. 1.5 = +50% rate). ModifyChargeRate effects multiply this in place.</summary>
        public float ChargeRateMultiplier = 1f;

        /// <summary>Fraction of HP damage dealt that heals this combatant back, for the remainder
        /// of the battle (Lifesteal effect).</summary>
        public float LifestealPercent;

        /// <summary>Base per-Step tick damage for the current Poisoned/Burned status, set when
        /// ApplyStatus last fired. Unused for Paralyzed/Asleep/no status.</summary>
        public int StatusTickDamage;

        /// <summary>How many Poisoned ticks have applied since it was last (re)applied — poison
        /// damage is StatusTickDamage * PoisonStacks and grows each tick (battle-sim-spec.md §5);
        /// Burned does not stack.</summary>
        public int PoisonStacks;

        public bool IsAlive => CurrentHP > 0;

        /// <summary>Fresh battle state for one mon: its stats and current HP as the run has them,
        /// every transient field at its default. Deliberately not a general-purpose clone — the
        /// point is that a battle starts from a known-clean slate, which is what the old shared
        /// type could never guarantee.</summary>
        public static BattleCombatant FromInstance(PokemonInstance instance)
        {
            return new BattleCombatant
            {
                InstanceId = instance.InstanceId,
                Source = instance,
                CurrentStats = instance.CurrentStats,
                CurrentHP = instance.CurrentHP,
                ResolvedPassive = instance.ResolvedPassive,
            };
        }

        /// <summary>Combatants for a whole line-up, in the same order — position 0 is the Lead,
        /// 1 the Support, the rest dormant (battle-sim-spec.md §2).</summary>
        public static List<BattleCombatant> FromLineUp(IReadOnlyList<PokemonInstance> lineUp)
        {
            var combatants = new List<BattleCombatant>(lineUp.Count);
            for (int i = 0; i < lineUp.Count; i++)
            {
                combatants.Add(FromInstance(lineUp[i]));
            }
            return combatants;
        }
    }
}
