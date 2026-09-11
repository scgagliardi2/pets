namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// A PokemonInstance's state for the duration of one battle — the object the Step loop
    /// actually mutates (docs/battle-sim-spec.md §2-§4). Discarded when the battle ends; nothing
    /// here persists back to PokemonInstance except (conceptually) a status carried out of a
    /// PvE fight, which the meta-layer decides how to handle — out of scope for the sim itself.
    ///
    /// Team-synergy bonuses (design doc §11, content-schema.md §6) are folded into Attack/Health/
    /// Speed once, at construction time (see <see cref="Create"/>), before the mon ever enters
    /// the Step loop — this sandbox pass doesn't compute synergy automatically (there's no
    /// line-up-assembly step yet), so callers apply any synergy bonus themselves before battle
    /// starts if they want to exercise it.
    /// </summary>
    public sealed class BattleMon
    {
        public string InstanceId;
        public string DisplayName;
        public PokemonType Type1;
        public PokemonType? Type2;

        public int Attack;
        public int Speed;
        public int MaxHP;
        public int CurrentHP;

        public StatusType? Status;
        public float Charge;
        public int ShieldAmount;
        public int DamageReductionFlat;
        public int LifestealPercent;

        /// <summary>Additive percentage; e.g. +20 charges 20% faster, -30 charges 30% slower.</summary>
        public int ChargeRateModifierPercent;

        public PassiveDefinition Passive;

        public bool IsAlive => CurrentHP > 0;

        /// <summary>Builds a fresh battle-scoped wrapper from persistent instance state.</summary>
        public static BattleMon Create(PokemonInstance instance, PassiveDefinition passive)
        {
            return new BattleMon
            {
                InstanceId = instance.InstanceId,
                DisplayName = string.IsNullOrEmpty(instance.Nickname) ? instance.DisplayName : instance.Nickname,
                Type1 = instance.Type1,
                Type2 = instance.Type2,
                Attack = instance.CurrentStats.Attack,
                Speed = instance.CurrentStats.Speed,
                MaxHP = instance.CurrentStats.Health,
                CurrentHP = instance.CurrentHP > 0 ? instance.CurrentHP : instance.CurrentStats.Health,
                Status = instance.Status,
                Passive = passive,
            };
        }
    }
}
