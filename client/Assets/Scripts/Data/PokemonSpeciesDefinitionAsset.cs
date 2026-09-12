using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>Authoring asset for one species. See content-schema.md §2 (there called
    /// "PokemonSpeciesDefinition" — named …Asset here purely to avoid colliding with
    /// Pets.Simulation.PassiveDefinition-style pure types when both are in scope in the same
    /// file, e.g. PokemonInstanceFactory).</summary>
    [CreateAssetMenu(fileName = "NewSpecies", menuName = "Pets/Pokemon Species Definition")]
    public sealed class PokemonSpeciesDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        public int Id;
        public string DisplayName;

        [Header("Typing")]
        public PokemonType Type1;
        public bool HasSecondType;
        public PokemonType Type2;

        [Header("Base stats (docs/pokemon_stats_unique.xlsx, base-form stage)")]
        public int BaseAttack;
        public int BaseHealth;
        public int BaseSpeed;

        [Header("Passive")]
        public PassiveDefinitionAsset Passive;

        [Header("Evolution (Phase 1+, unused while Phase 0 has no evolution)")]
        public PokemonSpeciesDefinitionAsset EvolvesInto;
        public int EvolutionExpThreshold;

        /// <summary>Direct reference to the species' artwork under Assets/Art/Pokemon (see
        /// content-schema.md §2). A reference rather than a Resources path string so that only the
        /// sprites curated species actually point at are pulled into a build, and so a typo is a
        /// compile-visible missing reference instead of a silent null at runtime. Read through
        /// Pets.Data.PokemonSprites.Load rather than directly.</summary>
        [Header("Presentation")]
        public Sprite Sprite;

        public bool IsLegendary;
    }
}
