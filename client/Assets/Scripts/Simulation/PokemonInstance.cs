using System;

namespace Pets.Simulation
{
    /// <summary>Plain runtime shape of one Pokémon as the *run* holds it, mirroring design doc
    /// §9's PokemonInstance interface (content-schema.md §8). Built from a
    /// PokemonSpeciesDefinition + save data by a content-layer converter — never authored
    /// directly. Zero Unity dependency.
    ///
    /// Persistent state only. A battle never touches one of these: the simulator works on
    /// <see cref="BattleCombatant"/>, copied from this at battle start, so shields, stat buffs,
    /// poison stacks and the rest can't leak out of the fight they happened in. See
    /// BattleCombatant for why that split exists.</summary>
    [Serializable]
    public sealed class PokemonInstance
    {
        public string InstanceId;
        public int SpeciesId;
        public string Nickname;
        public int Level = 1;
        public int Exp;
        public int ExpToNextLevel;

        /// <summary>Leveled base stats, plus any permanent modifiers the run has applied.
        /// Battle-only buffs are applied to the combatant's copy, not here.</summary>
        public Stats CurrentStats;

        /// <summary>HP the mon carries at the start of its next battle. Whether a fight's damage is
        /// written back here afterwards is the run layer's call — the simulator has no opinion and
        /// no longer makes the decision by accident.</summary>
        public int CurrentHP;

        /// <summary>Id of this instance's passive — can differ from the species default if an
        /// item overrides it (content-schema.md §7, not yet implemented).</summary>
        public string PassiveId;

        /// <summary>The actual passive behavior for this instance, already scaled by evolution
        /// stage (content-schema.md §3). Baked in by the content layer; copied onto the combatant
        /// at battle start.</summary>
        public PassiveDefinition ResolvedPassive;

        public bool IsAlive => CurrentHP > 0;
    }
}
