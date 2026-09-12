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

        /// <summary>The ceiling for BaseSpeed. Speed bars (Character Select's cards) are drawn as a
        /// fraction of it, so a species above it would silently pin a full bar —
        /// ContentIntegrityTests fails any asset that exceeds it.</summary>
        public const int MaxBaseSpeed = 200;

        [Header("Base stats (docs/pokemon_stats_unique.xlsx, base-form stage)")]
        public int BaseAttack;
        public int BaseHealth;
        public int BaseSpeed;

        /// <summary>The three base stats added up — the roster's one rough "how strong is this
        /// species" number, with no weighting, since the sim treats all three as first-draft
        /// placeholders anyway (PLAN.md §8). Character Select uses it to keep a run from opening on
        /// a fully-evolved form, and the Pokédex to mark which species that leaves startable.</summary>
        public int BaseStatTotal => BaseAttack + BaseHealth + BaseSpeed;

        [Header("Passive")]
        public PassiveDefinitionAsset Passive;

        [Header("Evolution")]

        /// <summary>What this species becomes when a mon carrying it crosses its evolution
        /// threshold (Pets.Meta.ExperienceResolver). Null for a final form — and also for the
        /// three roster species whose line branches (Eevee, Tyrogue, Nincada), since one reference
        /// can't express "becomes one of seven"; picking a branch is its own feature. Set by the
        /// roster importer from the cached PokeAPI chains, restricted to the roster.
        ///
        /// There is deliberately no per-species EXP threshold field: how much EXP an evolution
        /// costs is a run-layer rule (ExperienceResolver.ExpPerEvolution), and nothing in the
        /// roster sheet or PokeAPI gives a per-species number to put here.</summary>
        public PokemonSpeciesDefinitionAsset EvolvesInto;

        /// <summary>How many evolution steps deep this species sits in its real Pokémon chain —
        /// 0 for a base form, 1 for a first evolution, and so on. Counted against the true chain
        /// even when the earlier form isn't in the roster, so Pikachu is stage 1 (Pichu exists, it
        /// just isn't curated).
        ///
        /// This is the index into a passive's MagnitudeByStage table (content-schema.md §1, §3) —
        /// the "same passive, bigger numbers" rule — and nothing else. It is *not* what decides
        /// when a mon evolves: that's counted per instance, from how many times that particular mon
        /// has already evolved, so a curated base form like Pikachu still evolves on its first
        /// threshold rather than starting a step behind.</summary>
        public int EvolutionStage;

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
