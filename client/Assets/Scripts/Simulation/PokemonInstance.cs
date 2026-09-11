using System;

namespace Pets.Simulation
{
    /// <summary>Plain runtime shape of one Pokémon, mirroring design doc §9's PokemonInstance
    /// interface (content-schema.md §8). Built from a PokemonSpeciesDefinition + save data by a
    /// content-layer converter — never authored directly. Simulation/BattleRunner code operates on
    /// these, never on ScriptableObjects, so this type has zero Unity dependency.</summary>
    [Serializable]
    public sealed class PokemonInstance
    {
        public string InstanceId;
        public int SpeciesId;
        public string Nickname;
        public int Level = 1;
        public int Exp;
        public int ExpToNextLevel;

        /// <summary>Leveled base stats + synergy/item modifiers folded in at line-up assembly
        /// (battle-sim-spec.md §8). BuffAttack/BuffSpeed passive effects mutate this directly for
        /// the remainder of the battle.</summary>
        public Stats CurrentStats;

        public int CurrentHP;
        public StatusType? Status;

        /// <summary>Id of this instance's passive — can differ from the species default if an
        /// item overrides it (content-schema.md §7, not yet implemented).</summary>
        public string PassiveId;

        /// <summary>The actual passive behavior for this instance, already scaled by evolution
        /// stage (content-schema.md §3). Baked in by the content layer before battle assembly;
        /// the simulator never resolves PassiveId itself. Null means no passive.</summary>
        public PassiveDefinition ResolvedPassive;

        // --- Battle-only transient state below. None of this is part of the persisted
        // design-doc interface — it's meaningless outside an in-progress battle and should be
        // left at its default (or explicitly reset) whenever an instance enters a fresh battle.

        /// <summary>Fills toward BattleConfig.ChargeThreshold at speed * stepDuration per Step
        /// (battle-sim-spec.md §3-§4); irrelevant while dormant.</summary>
        public int Charge;

        /// <summary>Temporary absorb pool from a Shield effect; consumed by incoming damage
        /// before HP, for the remainder of the battle.</summary>
        public int Shield;

        /// <summary>Flat reduction applied to incoming damage for the remainder of the battle.</summary>
        public int DamageReductionFlat;

        /// <summary>Multiplicative modifier on charge accrual for the remainder of the battle
        /// (e.g. 1.5 = +50% rate). ModifyChargeRate effects multiply this in place.</summary>
        public float ChargeRateMultiplier = 1f;

        /// <summary>Fraction of HP damage dealt that heals this mon back, for the remainder of
        /// the battle (Lifesteal effect).</summary>
        public float LifestealPercent;

        /// <summary>Base per-Step tick damage for the current Poisoned/Burned status, set when
        /// ApplyStatus last fired. Unused for Paralyzed/Asleep/no status.</summary>
        public int StatusTickDamage;

        /// <summary>How many Poisoned ticks have applied since it was last (re)applied — poison
        /// damage is StatusTickDamage * PoisonStacks and grows each tick (battle-sim-spec.md §5);
        /// Burned does not stack.</summary>
        public int PoisonStacks;

        public bool IsAlive => CurrentHP > 0;
    }
}
