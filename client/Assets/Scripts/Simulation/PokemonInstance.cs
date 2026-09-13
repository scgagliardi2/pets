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

        /// <summary>Total EXP this mon has ever earned — one point per fight won. It never resets;
        /// what an evolution consumes is accounted for by <see cref="TimesEvolved"/>, so this stays
        /// the mon's whole history in one number. Together with the species it is the only growth
        /// state stored: stats are read off it by Pets.Data.StatGrowth — see ADR 0008.</summary>
        public int Exp;

        /// <summary>How many times this particular mon has evolved. Each evolution consumed
        /// Pets.Meta.ExperienceResolver.ExpPerEvolution of <see cref="Exp"/>, so this is what turns
        /// a lifetime total into the EXP the current species has actually grown on
        /// (ExperienceResolver.ExpSinceEvolution).
        ///
        /// Counted per instance rather than read off the species' chain depth, because the two
        /// disagree for a curated base form whose real pre-evolution isn't in the roster — see
        /// PokemonSpeciesDefinitionAsset.EvolutionStage.</summary>
        public int TimesEvolved;

        /// <summary>The mon's stats as the run has them. **Derived, not accumulated**:
        /// Pets.Meta.ExperienceResolver.Recompute rebuilds this from the current species' tier line and
        /// the EXP earned on it, and will overwrite anything written here that doesn't follow from
        /// those two.
        /// A permanent modifier (an item, say) therefore needs to become an input to that
        /// calculation rather than a one-off addition to this field. Battle-only buffs are applied
        /// to the combatant's copy and never reach here at all.</summary>
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
